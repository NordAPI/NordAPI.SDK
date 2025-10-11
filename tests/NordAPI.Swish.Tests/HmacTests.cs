using NordAPI.Swish.DependencyInjection;
using NordAPI.Swish.Security.Http;
using Xunit;

namespace NordAPI.Swish.Tests
{
    // Unit tests for HMAC signing behavior
    public class HmacTests
    {
        // Verify ComputeHmac produces a stable uppercase hex string for the same inputs
        [Fact]
        public void ComputeHmac_ReturnsStableHex()
        {
            // Compute HMAC twice with identical key and payload
            var s1 = HmacSigningHandler.ComputeHmac("secret", "data");
            var s2 = HmacSigningHandler.ComputeHmac("secret", "data");

            // The two results must match exactly
            Assert.Equal(s1, s2);

            // The output should be an uppercase hex string
            Assert.Matches("^[A-F0-9]+$", s1);
        }
    }
}



