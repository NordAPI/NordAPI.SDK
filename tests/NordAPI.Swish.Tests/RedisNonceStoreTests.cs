using NordAPI.Swish.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;
using NordAPI.Swish.Webhooks;

public class RedisNonceStoreTests
{
    [Fact]
    public async Task TryRememberAsync_Works_When_Redis_Configured()
    {
        var conn = Environment.GetEnvironmentVariable("REDIS_URL");
        if (string.IsNullOrWhiteSpace(conn))
        {
            // Ingen Redis konfigurerad – hoppa över testet
            return;
        }

        var store = new RedisNonceStore(conn, "test:nonce:");
        var nonce = Guid.NewGuid().ToString("N");
        var exp = DateTimeOffset.UtcNow.AddSeconds(5);

        var first = await store.TryRememberAsync(nonce, exp);
        var second = await store.TryRememberAsync(nonce, exp);

        Assert.True(first);
        Assert.False(second);
    }
}


