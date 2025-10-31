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
            using var store = new RedisNonceStore("localhost:6379", prefix: "test:");
            store.Dispose();
            Assert.True(true);
        }
    }
}