using System;
using Microsoft.Extensions.DependencyInjection;

namespace NordAPI.Swish.Webhooks
{
    /// <summary>DI registration for webhook verification (HMAC + timestamp + nonce).</summary>
    public static class SwishWebhookServiceCollectionExtensions
    {
        public static ISwishWebhookBuilder AddSwishWebhookVerification(
            this IServiceCollection services,
            Action<SwishWebhookVerifierOptions> configure)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var opts = new SwishWebhookVerifierOptions();
            configure(opts);
            if (string.IsNullOrWhiteSpace(opts.SharedSecret))
                throw new ArgumentException("SharedSecret must be configured.", nameof(configure));

            services.AddSingleton(opts);
            services.AddSingleton<SwishWebhookVerifier>(); // needs ISwishNonceStore

            return new SwishWebhookBuilder(services);
        }

        public static ISwishWebhookBuilder AddInMemoryNonceStore(this ISwishWebhookBuilder builder)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            builder.Services.AddSingleton<ISwishNonceStore, InMemoryNonceStore>();
            return builder;
        }

        public static ISwishWebhookBuilder AddRedisNonceStore(
            this ISwishWebhookBuilder builder,
            Func<IServiceProvider, ISwishNonceStore> factory)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            if (factory is null) throw new ArgumentNullException(nameof(factory));
            builder.Services.AddSingleton(factory);
            return builder;
        }
    }
}

