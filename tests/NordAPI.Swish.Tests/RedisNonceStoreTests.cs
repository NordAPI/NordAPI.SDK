using System;
using System.Threading.Tasks;
using Xunit;
using NordAPI.Swish.Webhooks;

namespace NordAPI.Swish.Tests
{
    /// <summary>
    /// Integration-like check for RedisNonceStore.
    /// If Redis is not configured via env vars, the test exits early (treated as pass).
    /// </summary>
    public class RedisNonceStoreTests
    {
        [Fact]
        public async Task TryRememberAsync_Works_When_Redis_Configured()
        {
            // Prefer SWISH_REDIS; accept common aliases used by the sample
            var conn =
                Environment.GetEnvironmentVariable("SWISH_REDIS")
                ?? Environment.GetEnvironmentVariable("REDIS_URL")
                ?? Environment.GetEnvironmentVariable("SWISH_REDIS_CONN");

            if (string.IsNullOrWhiteSpace(conn))
            {
                // No Redis configured -> quietly skip by returning (keeps CI green).
                Console.WriteLine("Redis not configured. Set SWISH_REDIS (or REDIS_URL / SWISH_REDIS_CONN) to run this test.");
                return;
            }

            using var store = new RedisNonceStore(conn!, "test:nonce:");
            var nonce = Guid.NewGuid().ToString("N");
            var exp = DateTimeOffset.UtcNow.AddSeconds(5);

            var first = await store.TryRememberAsync(nonce, exp);
            var second = await store.TryRememberAsync(nonce, exp);

            Assert.True(first, "first attempt should store the nonce");
            Assert.False(second, "second attempt should be detected as replay");
        }
    }
}







