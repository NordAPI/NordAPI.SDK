#nullable enable
namespace NordAPI.Security;

/// <summary>Nonce storage to prevent replay. Returns true if the nonce was added (not seen before).</summary>
public interface INonceStore
{
    /// <summary>
    /// Try to add a nonce with a given TTL. Returns false if it already existed (replay).
    /// </summary>
    ValueTask<bool> TryAddAsync(string nonce, TimeSpan ttl, CancellationToken ct = default);
}
