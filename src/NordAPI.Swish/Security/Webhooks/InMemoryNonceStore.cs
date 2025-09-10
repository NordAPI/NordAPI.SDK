using System;
using System.Collections.Concurrent;
using System.Threading;

namespace NordAPI.Swish.Security.Webhooks;

/// <summary>
/// Enkel in-memory nonce-store med TTL. Trådsäker. För produktion kan man byta till Redis/DB.
/// </summary>
public sealed class InMemoryNonceStore : ISwishNonceStore, IDisposable
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new();
    private readonly Timer _timer;

    public InMemoryNonceStore(TimeSpan? scavengeInterval = null)
    {
        _timer = new Timer(_ => Scavenge(), null,
            scavengeInterval ?? TimeSpan.FromMinutes(5),
            scavengeInterval ?? TimeSpan.FromMinutes(5));
    }

    public bool TryRemember(string nonce, DateTimeOffset expiresAtUtc)
    {
        var now = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(nonce))
            return false;

        // Finns redan och inte utgången? => replay
        if (_seen.TryGetValue(nonce, out var existing) && existing > now)
            return false;

        _seen[nonce] = expiresAtUtc;
        return true;
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
