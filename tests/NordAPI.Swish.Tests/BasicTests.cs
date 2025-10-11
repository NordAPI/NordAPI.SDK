using NordAPI.Swish.DependencyInjection;
using Xunit;
using NordAPI.Swish;
using System.Net.Http;

namespace NordAPI.Swish.Tests
{
    // Unit tests for SwishClient basic instantiation
    public class BasicTests
    {
        // Verify that SwishClient can be created with a default HttpClient
        [Fact]
        public void CanConstructClient()
        {
            // Arrange & Act
            var client = new SwishClient(new HttpClient());

            // Assert
            Assert.NotNull(client);
        }
    }
}


