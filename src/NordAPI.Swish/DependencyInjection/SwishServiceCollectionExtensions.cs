using System;
using System.Net.Http;
using System.Net.Security; // for SslPolicyErrors
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using NordAPI.Swish.Security.Http;
using Polly;
using Polly.Extensions.Http;

namespace NordAPI.Swish.DependencyInjection;

public static class SwishServiceCollectionExtensions
{
    public static IServiceCollection AddSwishClient(
        this IServiceCollection services,
        Action<SwishOptions> configure,
        X509Certificate2? clientCertificate = null)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var opts = new SwishOptions();
        configure(opts);

        if (opts.BaseAddress is null)
            throw new InvalidOperationException("SwishOptions.BaseAddress must be set.");
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
            throw new InvalidOperationException("SwishOptions.ApiKey must be set.");
        if (string.IsNullOrWhiteSpace(opts.Secret))
            throw new InvalidOperationException("SwishOptions.Secret must be set.");

        // Expose options to SwishClient ctor
        services.AddSingleton(opts);

        // Polly: retry on 5xx/408/HttpRequestException
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(new[]
            {
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(200),
                TimeSpan.FromMilliseconds(400),
            });

        services
            .AddHttpClient<ISwishClient, SwishClient>("Swish", (_, client) =>
            {
                client.BaseAddress = opts.BaseAddress!;
            })
            // Inner delegating handlers
            .AddHttpMessageHandler(() =>
                new RateLimitingHandler(maxConcurrency: 4, minDelayBetweenCalls: TimeSpan.FromMilliseconds(100)))
            .AddHttpMessageHandler(() =>
                new HmacSigningHandler(opts.ApiKey!, opts.Secret!))
            // Outermost: Polly retry
            .AddPolicyHandler(retryPolicy)
#pragma warning disable CS0618
            // Respect a primary set by tests/host; otherwise set our own (mTLS or fallback)
            .ConfigureHttpMessageHandlerBuilder(builder =>
            {
                if (builder.PrimaryHandler is not null)
                    return; // do not override test/host-provided primary

                if (clientCertificate is not null)
                {
                    var h = new SocketsHttpHandler();
                    h.SslOptions.ClientCertificates = new X509CertificateCollection { clientCertificate };
#if DEBUG
                    // DEV ONLY: relaxed validation in Debug; never in Release.
                    h.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#endif
                    builder.PrimaryHandler = h;
                    return;
                }

                // Env-controlled factory (mTLS if SWISH_PFX_PATH + SWISH_PFX_PASS; fallback otherwise)
                builder.PrimaryHandler = SwishMtlsHandlerFactory.Create();
            })
#pragma warning restore CS0618
            ;

        return services;
    }
}








