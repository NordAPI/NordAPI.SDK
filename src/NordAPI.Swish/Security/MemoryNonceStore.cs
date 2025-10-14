#nullable enable
using System.Collections.Concurrent;

namespace NordAPI.Security;

/// <summary>
/// In-memory nonce store for single-process apps. Suitable for demos/tests.
/// Use a distributed store (e.g., Redis) in production for multi-instance.
/// </summary>
public sealed class MemoryNonceStore : INonceStore, IDisposable
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _expirations = new();
    private readonly TimeSpan _sweepInterval;
    private readonly Timer _sweeper;

    public MemoryNonceStore(TimeSpan? sweepInterval = null)
    {
        _sweepInterval = sweepInterval ?? TimeSpan.FromMinutes(2);
        _sweeper = new Timer(_ => Sweep(), null, _sweepInterval, _sweepInterval);
    }

    public ValueTask<bool> TryAddAsync(string nonce, TimeSpan ttl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nonce)) throw new ArgumentException("Nonce is required.", nameof(nonce));
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);

        // If already exists and not yet expired => replay
        if (_expirations.TryGetValue(nonce, out var existing) && existing > DateTimeOffset.UtcNow)
            return ValueTask.FromResult(false);

        _expirations[nonce] = expiresAt;
        return ValueTask.FromResult(true);
    }

    private void Sweep()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _expirations)
        {
            if (kvp.Value <= now)
                _expirations.TryRemove(kvp.Key, out _);
        }
    }

    public void Dispose()
    {
        _sweeper.Dispose();
        _expirations.Clear();
    }
}
