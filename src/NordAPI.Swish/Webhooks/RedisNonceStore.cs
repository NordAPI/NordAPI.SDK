using System;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace NordAPI.Swish.Webhooks;

public sealed class RedisNonceStore : ISwishNonceStore, IDisposable
{
    private readonly IConnectionMultiplexer _mux;
    private readonly string _prefix;

    // connectionString exempel: "localhost:6379"
    public RedisNonceStore(string connectionString, string prefix = "swish:nonce:")
    {
        _mux = ConnectionMultiplexer.Connect(connectionString);
        _prefix = prefix;
    }

    // Om du vill DI:a in multiplexer utifrån
    public RedisNonceStore(IConnectionMultiplexer mux, string prefix = "swish:nonce:")
    {
        _mux = mux;
        _prefix = prefix;
    }

    public async Task<bool> TryRememberAsync(string nonce, DateTimeOffset expiresAtUtc, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nonce))
            return false;

        var ttl = expiresAtUtc - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
            return false;

        var db = _mux.GetDatabase();
        var key = _prefix + nonce;

        // Sätt nyckeln endast om den inte finns (NX) och med expiry = ttl.
        // true => ny (OK). false => fanns redan (replay).
        return await db.StringSetAsync(key, "1", ttl, When.NotExists);
    }

    public void Dispose()
    {
        if (_mux is ConnectionMultiplexer cm) cm.Dispose();
    }
}
