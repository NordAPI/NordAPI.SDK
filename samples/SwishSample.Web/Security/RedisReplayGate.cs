using StackExchange.Redis;

namespace SwishSample.Web.Security;

public static class RedisReplayGate
{
    /// <summary>
    /// Försöker registrera en nonce i Redis med NX + TTL.
    /// Returnerar true om noncen var NY (acceptera request), false om replay (blockera).
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
        // SET key value NX EX seconds  => endast om nyckeln inte finns, med utgångstid
        var ok = await db.StringSetAsync(
            key, "1",
            expiry: ttl,
            when: When.NotExists);

        return ok; // true = sattes (ny), false = fanns redan (replay)
    }
}
