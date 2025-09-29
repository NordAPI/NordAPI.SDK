using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NordAPI.Swish.Webhooks;

/// <summary>
/// In-memory nonce store with TTL + periodic scavenging. Thread-safe.
/// For multi-instance production, replace with Redis/DB.
/// </summary>
public sealed class InMemoryNonceStore : ISwishNonceStore, IDisposable
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new();
    private readonly Timer _timer;

    public InMemoryNonceStore(TimeSpan? scavengeInterval = null)
    {
        var interval = scavengeInterval ?? TimeSpan.FromMinutes(5);
        _timer = new Timer(_ => Scavenge(), null, interval, interval);
    }

    public Task<bool> TryRememberAsync(string nonce, DateTimeOffset expiresAtUtc, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nonce))
            return Task.FromResult(false);

        var now = DateTimeOffset.UtcNow;

        // exists and not expired -> replay
        if (_seen.TryGetValue(nonce, out var existing) && existing > now)
            return Task.FromResult(false);

        _seen[nonce] = expiresAtUtc;
        return Task.FromResult(true);
    }

    private void Scavenge()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _seen)
        {
            if (kv.Value <= now)
                _seen.TryRemove(kv.Key, out _);
        }
    }

    public void Dispose() => _timer.Dispose();
}
