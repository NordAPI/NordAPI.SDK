using NordAPI.Swish.DependencyInjection;
using NordAPI.Swish.Security.Http;
using Xunit;

namespace NordAPI.Swish.Tests
{
    public class HmacTests
    {
        [Fact]
        public void ComputeHmac_ReturnsStableHex()
        {
            var s1 = HmacSigningHandler.ComputeHmac("secret", "data");
            var s2 = HmacSigningHandler.ComputeHmac("secret", "data");
            Assert.Equal(s1, s2);
            Assert.Matches("^[A-F0-9]+$", s1);
        }
    }
}


