using System.Linq;
using System.Reflection;
using Xunit;

namespace NordAPI.Swish.Tests.PublicApi;

public sealed class PublicTypeSanityTests
{
    [Fact]
    public void Public_types_should_match_expected_contract()
    {
        var assembly = typeof(NordAPI.Swish.ISwishClient).Assembly;

        var publicTypes = assembly
            .GetExportedTypes()
            .Select(t => t.FullName!)
            .OrderBy(n => n)
            .ToArray();

        var expected = new[]
        {
            "NordAPI.Swish.ISwishClient",
            "NordAPI.Swish.SwishClient",
            "NordAPI.Swish.SwishOptions",
            "NordAPI.Swish.SwishCertificateOptions",

            "NordAPI.Swish.Security.Http.SwishMtlsHandlerFactory",

            "NordAPI.Swish.Errors.SwishException",
            "NordAPI.Swish.Errors.SwishAuthException",
            "NordAPI.Swish.Errors.SwishValidationException",
            "NordAPI.Swish.Errors.SwishConflictException",
            "NordAPI.Swish.Errors.SwishTransientException",
            "NordAPI.Swish.Errors.SwishApiError",

            "NordAPI.Swish.CreatePaymentRequest",
            "NordAPI.Swish.CreatePaymentResponse",
            "NordAPI.Swish.CreateRefundRequest",
            "NordAPI.Swish.CreateRefundResponse",

            "NordAPI.Swish.DependencyInjection.SwishServiceCollectionExtensions",
            "NordAPI.Swish.DependencyInjection.SwishHttpClientRegistration",
            "NordAPI.Swish.DependencyInjection.SwishWebhookServiceCollectionExtensions",

            "NordAPI.Swish.Webhooks.ISwishNonceStore",
            "NordAPI.Swish.Webhooks.ISwishWebhookBuilder",
            "NordAPI.Swish.Webhooks.InMemoryNonceStore",
            "NordAPI.Swish.Webhooks.RedisNonceStore",
            "NordAPI.Swish.Webhooks.NonceStoreExtensions",
            "NordAPI.Swish.Webhooks.SwishWebhookVerifier",
            "NordAPI.Swish.Webhooks.SwishWebhookVerifierOptions",
            "NordAPI.Swish.Webhooks.SwishWebhookEndpointFilter",
            "NordAPI.Swish.Webhooks.SwishWebhookVerifier+VerifyResult",
        }
        .OrderBy(n => n)
        .ToArray();

        Assert.Equal(expected, publicTypes);
    }
}
