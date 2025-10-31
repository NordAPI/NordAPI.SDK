using System;
using NordAPI.Swish.Webhooks;
using Xunit;

namespace NordAPI.Swish.Tests.Webhooks
{
    public class RedisNonceStore_SmokeTests
    {
        [Fact]
        public void Dispose_Does_Not_Throw_For_InternallyOwned_Mux()
        {
            // abortConnect=false => Connect() does not throw if Redis is missing in CI.
            using var store = new RedisNonceStore("localhost:6379,abortConnect=false", prefix: "test:");
            store.Dispose();
            Assert.True(true);
        }
    }
}

