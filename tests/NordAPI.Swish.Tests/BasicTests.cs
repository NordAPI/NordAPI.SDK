using NordAPI.Swish.DependencyInjection;
using Xunit;
using NordAPI.Swish;
using System.Net.Http;

namespace NordAPI.Swish.Tests;

public class BasicTests
{
    [Fact]
    public void CanConstructClient()
    {
        // Arrange & Act
        var client = new SwishClient(new HttpClient());

        // Assert
        Assert.NotNull(client);
    }
}


