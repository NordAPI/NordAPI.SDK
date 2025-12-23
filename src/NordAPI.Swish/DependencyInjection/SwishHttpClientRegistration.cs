using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using NordAPI.Swish.Security.Http;
using Polly;
using Polly.Extensions.Http;

namespace NordAPI.Swish.DependencyInjection
{
    /// <summary>
    /// Registers named HttpClient transports with mTLS support and Polly retry.
    /// Names:
    /// - "NordAPI.Swish.Http" (canonical)
    /// - "Swish"             (compat alias used by tests/samples)
    ///
    /// Design:
    /// - OUTERMOST retry (Polly) so it observes all inner handlers.
    /// - Primary is provided via a preserving filter: if tests/host set Primary we DO NOT override it;
    ///   otherwise we set an mTLS-capable Primary (env or explicit cert).
    /// - Debug: relaxed server validation ONLY when using our mTLS path.
    /// - Release: strict chain.
    /// </summary>
    public static class SwishHttpClientRegistration
    {
        /// <summary>
        /// The canonical <see cref="HttpClient"/> name used by NordAPI.Swish.
        /// </summary>
        public const string CanonicalClient = "NordAPI.Swish.Http";

        /// <summary>
        /// The alias <see cref="HttpClient"/> name used by NordAPI.Swish.
        /// </summary>
        public const string AliasClient = "Swish";

        /// <summary>
        /// Registers the Swish mTLS transport for outbound HTTP calls.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="clientCertificate">
        /// The client certificate used for mutual TLS (mTLS). Provide a real Swish certificate in production.
        /// </param>
        /// <returns>The updated <paramref name="services"/> instance.</returns>
        public static IServiceCollection AddSwishMtlsTransport(
            this IServiceCollection services,
            X509Certificate2? clientCertificate = null)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));

            // OUTERMOST retry
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError() // 5xx, 408, HttpRequestException
                .WaitAndRetryAsync(new[]
                {
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(200),
                    TimeSpan.FromMilliseconds(400),
                });

            // Primary factory (explicit cert wins; else env mTLS; else plain)
            Func<HttpMessageHandler> primaryFactory = () =>
            {
                if (clientCertificate is not null)
                {
                    var h = new SocketsHttpHandler();
                    h.SslOptions.ClientCertificates = new X509CertificateCollection { clientCertificate };
#if DEBUG
                    h.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
#endif
                    return h;
                }
                return SwishMtlsHandlerFactory.Create();
            };

            // Preserve test/host Primary; only set ours if none exists.
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter>(
                new SwishPrimaryPreservingFilter(primaryFactory)));

            // Helper to wire identical inner pipeline for both names
            void Wire(string name)
            {
                services
                    .AddHttpClient(name)
                    // Inner transport-level handlers
                    .AddHttpMessageHandler(() =>
                        new RateLimitingHandler(
                            maxConcurrency: 4,
                            minDelayBetweenCalls: TimeSpan.FromMilliseconds(100)))
                    // OUTERMOST: retry (added last so it wraps everything below)
                    .AddPolicyHandler(retryPolicy);
            }

            Wire(CanonicalClient);
            Wire(AliasClient);

            // NOTE: No HMAC here (transport-only named clients).
            return services;
        }
    }
}
