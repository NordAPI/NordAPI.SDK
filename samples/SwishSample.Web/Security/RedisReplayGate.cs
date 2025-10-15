using StackExchange.Redis;

namespace SwishSample.Web.Security;

public static class RedisReplayGate
{
    /// <summary>
    /// Attempts to register a nonce in Redis using NX + TTL.
    /// Returns true if the nonce was NEW (accept the request), false if replay (reject).
    /// </summary>
    public static async Task<bool> TryRegisterNonceAsync(
        IConnectionMultiplexer mux,
        string prefix,
        string nonce,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        var db = mux.GetDatabase();
        var key = $"{prefix}:{nonce}";
        // SET key value NX EX seconds  => only if the key does not exist, with expiry
        var ok = await db.StringSetAsync(
            key, "1",
            expiry: ttl,
            when: When.NotExists);

        return ok; // true = set (new), false = already existed (replay)
    }
}
