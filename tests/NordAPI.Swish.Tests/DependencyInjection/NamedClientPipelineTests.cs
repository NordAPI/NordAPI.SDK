using System;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;

namespace NordAPI.Swish.Tests.DependencyInjection
{
    public class NamedClientPipelineTests
    {
        [Fact]
        public void AddSwishClient_WiresTypedClient_ToNamedClient_Swish()
        {
            var services = new ServiceCollection();

            // Opt-in named client (utan cert → vanlig handler, men named pipeline finns)
            services.AddSwishMtlsTransport();

            services.AddSwishClient(opts =>
            {
                opts.BaseAddress = new Uri("https://example.invalid");
                opts.ApiKey = "dev-key";
                opts.Secret = "dev-secret";
            });

            var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISwishClient>();

            Assert.NotNull(client);
            // Mer avancerad verifikation kräver introspektion eller e2e – här räcker det att DI lyckas.
        }
    }
}
