using System;
using System.Threading;
using System.Threading.Tasks;

namespace NordAPI.Swish.Webhooks;

/// <summary>
/// Stores nonces to prevent replay. Implementations must be thread-safe.
/// Returns false if the nonce already exists and is not expired.
/// </summary>
public interface ISwishNonceStore
{
    Task<bool> TryRememberAsync(string nonce, DateTimeOffset expiresAtUtc, CancellationToken ct = default);
}
