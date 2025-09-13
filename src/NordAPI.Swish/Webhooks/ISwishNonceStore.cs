using System;
using System.Threading;
using System.Threading.Tasks;

namespace NordAPI.Swish.Webhooks;

public interface ISwishNonceStore
{
    /// <summary>
    /// Returnerar true om noncen redan setts inom "window", annars false och markerar den som sedd.
    /// </summary>
    Task<bool> SeenRecentlyAsync(
        string nonce,
        DateTimeOffset timestamp,
        TimeSpan window,
        CancellationToken ct = default);
}
