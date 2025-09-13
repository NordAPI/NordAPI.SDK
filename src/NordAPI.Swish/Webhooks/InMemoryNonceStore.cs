using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NordAPI.Swish.Webhooks;

public sealed class InMemoryNonceStore : ISwishNonceStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new();

    public Task<bool> SeenRecentlyAsync(
        string nonce,
        DateTimeOffset timestamp,
        TimeSpan window,
        CancellationToken ct = default)
    {
        // Städa gamla nycklar
        var cutoff = DateTimeOffset.UtcNow - window;
        foreach (var kv in _seen)
        {
            if (kv.Value < cutoff)
            {
                _seen.TryRemove(kv.Key, out _);
            }
        }

        // Har vi redan sett noncen?
        var exists = _seen.ContainsKey(nonce);
        if (!exists)
        {
            _seen[nonce] = timestamp;
        }

        return Task.FromResult(exists);
    }
}
