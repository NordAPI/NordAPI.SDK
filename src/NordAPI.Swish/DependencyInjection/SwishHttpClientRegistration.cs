using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace NordAPI.Swish.DependencyInjection
{
    /// <summary>
    /// Registers a named HttpClient ("NordAPI.Swish.Http") that uses the SDK's mTLS handler factory.
    /// Safe fallback when no cert is configured.
    /// </summary>
    public static class SwishHttpClientRegistration
    {
        public const string NamedClient = "NordAPI.Swish.Http";

        public static IServiceCollection AddSwishMtlsTransport(this IServiceCollection services)
        {
            services.AddHttpClient(NamedClient)
                    .ConfigurePrimaryHttpMessageHandler(
                        () => NordAPI.Swish.Security.Http.SwishMtlsHandlerFactory.Create());
            return services;
        }
    }
}
