using System;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace NordAPI.Swish.Webhooks;

/// <summary>
/// Redis-backed implementation of <see cref="ISwishNonceStore"/> for distributed replay protection.
/// </summary>
/// <remarks>
/// Stores nonces in Redis using string keys with TTL (time-to-live) to detect replay attempts across multiple instances.
/// Thread-safe and suitable for multi-server deployments.
/// </remarks>
public sealed class RedisNonceStore : ISwishNonceStore, IDisposable
{
    private readonly IConnectionMultiplexer _mux;
    private readonly string _prefix;
    private readonly bool _ownsMux;

    /// <summary>
    /// Creates a new Redis nonce store using a connection string.
    /// </summary>
    /// <param name="connectionString">Redis connection string, e.g. "localhost:6379".</param>
    /// <param name="prefix">Optional key prefix, defaults to <c>"swish:nonce:"</c>.</param>
    public RedisNonceStore(string connectionString, string prefix = "swish:nonce:")
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        _mux = ConnectionMultiplexer.Connect(connectionString);
        _prefix = string.IsNullOrWhiteSpace(prefix) ? "swish:nonce:" : prefix;
        _ownsMux = true; // this instance created the multiplexer
    }

    /// <summary>
    /// Creates a new Redis nonce store using an existing connection multiplexer.
    /// </summary>
    /// <param name="mux">An existing <see cref="IConnectionMultiplexer"/> instance.</param>
    /// <param name="prefix">Optional key prefix, defaults to <c>"swish:nonce:"</c>.</param>
    /// <param name="ownsMux">Whether this instance owns the multiplexer and should dispose it.</param>
    public RedisNonceStore(IConnectionMultiplexer mux, string prefix = "swish:nonce:", bool ownsMux = false)
    {
        _mux = mux ?? throw new ArgumentNullException(nameof(mux));
        _prefix = string.IsNullOrWhiteSpace(prefix) ? "swish:nonce:" : prefix;
        _ownsMux = ownsMux;
    }

    /// <inheritdoc />
    public async Task<bool> TryRememberAsync(string nonce, DateTimeOffset expiresAtUtc, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nonce))
            return false;

        var ttl = expiresAtUtc - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
            return false;

        var db = _mux.GetDatabase();
        var key = _prefix + nonce;

        // Only set the key if it does not already exist (NX) with a TTL.
        // true => new (OK). false => already exists (replay).
        return await db.StringSetAsync(key, "1", ttl, When.NotExists).ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes the underlying Redis connection if owned by this instance.
    /// </summary>
    public void Dispose()
    {
        if (_ownsMux)
        {
            try { _mux?.Dispose(); } catch { /* ignore */ }
        }
    }
}
