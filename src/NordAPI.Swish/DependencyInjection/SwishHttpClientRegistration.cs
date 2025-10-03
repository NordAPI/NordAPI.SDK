using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using NordAPI.Swish.Security.Http;
using Polly;
using Polly.Extensions.Http;

namespace NordAPI.Swish.DependencyInjection
{
    /// <summary>
    /// Registers named HttpClients with mTLS support and Polly retry.
    /// Names covered: "Swish" and "NordAPI.Swish.Http".
    /// </summary>
    public static class SwishHttpClientRegistration
    {
        public const string PrimaryName = "Swish";
        public const string AliasName   = "NordAPI.Swish.Http";

        public static IServiceCollection AddSwishMtlsTransport(this IServiceCollection services, X509Certificate2? clientCertificate = null)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));

            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError() // 5xx, 408, HttpRequestException
                .WaitAndRetryAsync(new[]
                {
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(200),
                    TimeSpan.FromMilliseconds(400),
                    TimeSpan.FromMilliseconds(800),
                });

            void ConfigureNamed(string name)
            {
                services
                    .AddHttpClient(name)
                    // OUTERMOST: Polly retry
                    .AddPolicyHandler(retryPolicy)
#pragma warning disable CS0618 // We need this API to respect a test/host-set Primary
                    .ConfigureHttpMessageHandlerBuilder(builder =>
                    {
                        // If tests/host already set Primary, do not override it
                        if (builder.PrimaryHandler is not null)
                            return;

                        if (clientCertificate is not null)
                        {
                            var h = new SocketsHttpHandler();
                            h.SslOptions.ClientCertificates = new X509CertificateCollection { clientCertificate };
#if DEBUG
                            h.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
#endif
                            builder.PrimaryHandler = h;
                            return;
                        }

                        // Default: env-driven factory (mTLS if PFX present; fallback otherwise)
                        builder.PrimaryHandler = SwishMtlsHandlerFactory.Create();
                    })
#pragma warning restore CS0618
                    ;
            }

            // Register both names so tests using "Swish" and apps using the alias work the same
            ConfigureNamed(PrimaryName);
            ConfigureNamed(AliasName);

            return services;
        }
    }
}
