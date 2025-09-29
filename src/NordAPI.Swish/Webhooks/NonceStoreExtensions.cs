using System;
using System.Threading;
using System.Threading.Tasks;

namespace NordAPI.Swish.Webhooks;

/// <summary>
/// Backwards-compatible helpers for older nonce store method shapes.
/// </summary>
public static class NonceStoreExtensions
{
    // Old shape: returns true if already seen recently (replay)
    public static async Task<bool> SeenRecentlyAsync(
        this ISwishNonceStore store,
        string nonce,
        DateTimeOffset timestamp,
        TimeSpan window,
        CancellationToken ct = default)
    {
        var ok = await store.TryRememberAsync(nonce, timestamp.Add(window), ct);
        return !ok; // false => already exists (replay)
    }

    // Old sync API
    public static bool TryRemember(
        this ISwishNonceStore store,
        string nonce,
        DateTimeOffset expiresAtUtc,
        CancellationToken ct = default)
    {
        return store.TryRememberAsync(nonce, expiresAtUtc, ct).GetAwaiter().GetResult();
    }
}
