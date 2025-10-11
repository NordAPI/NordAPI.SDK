using System;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;

namespace NordAPI.Swish.Tests.DependencyInjection
{
    // Ensures AddSwishClient wires ISwishClient into the "Swish" HTTP pipeline
    public class NamedClientPipelineTests
    {
        [Fact]
        public void AddSwishClient_RegistersTypedClientWithSwishPipeline()
        {
            // Arrange: configure DI with mTLS transport pipeline
            var services = new ServiceCollection();
            services.AddSwishMtlsTransport();

            // Act: register the typed SwishClient with API options
            services.AddSwishClient(opts =>
            {
                opts.BaseAddress = new Uri("https://example.invalid");
                opts.ApiKey      = "dev-key";
                opts.Secret      = "dev-secret";
            });

            // Build the provider and resolve the client
            using var provider = services.BuildServiceProvider();
            var client = provider.GetRequiredService<ISwishClient>();

            // Assert: DI successfully provides a non-null SwishClient
            Assert.NotNull(client);
        }
    }
}

