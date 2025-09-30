using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using NordAPI.Swish.Security.Http;

namespace NordAPI.Swish.DependencyInjection;

public static class SwishServiceCollectionExtensions
{
    /// <summary>
    /// Registrerar ISwishClient med HttpClientFactory.
    /// Använd <paramref name="configure"/> för att fylla SwishOptions.
    /// Valfritt: ange <paramref name="clientCertificate"/> för mTLS.
    /// </summary>
    public static IServiceCollection AddSwishClient(
        this IServiceCollection services,
        Action<SwishOptions> configure,
        X509Certificate2? clientCertificate = null)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var opts = new SwishOptions();
        configure(opts);

        if (opts.BaseAddress is null) throw new InvalidOperationException("SwishOptions.BaseAddress must be set.");
        if (string.IsNullOrWhiteSpace(opts.ApiKey)) throw new InvalidOperationException("SwishOptions.ApiKey must be set.");
        if (string.IsNullOrWhiteSpace(opts.Secret)) throw new InvalidOperationException("SwishOptions.Secret must be set.");

        // Gör options tillgängliga för SwishClient-konstruktorn
        services.AddSingleton(opts);

        services.AddHttpClient<ISwishClient, SwishClient>("Swish", (sp, client) =>
        {
            client.BaseAddress = opts.BaseAddress;
        })
        // HMAC-signering
        .AddHttpMessageHandler(() => new HmacSigningHandler(opts.ApiKey!, opts.Secret!))
        // Enkel rate limiting
        .AddHttpMessageHandler(() => new RateLimitingHandler(maxConcurrency: 4, minDelayBetweenCalls: TimeSpan.FromMilliseconds(100)))
        // Primär handler (mTLS om cert finns)
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            if (clientCertificate is not null)
            {
                return new MtlsHttpHandler(clientCertificate);
            }
            return new HttpClientHandler();
        });

        return services;
    }
}
