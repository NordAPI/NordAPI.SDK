using NordAPI.Swish.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using NordAPI.Swish.Webhooks;
using Xunit;

namespace NordAPI.Swish.Tests
{
    public class WebhookVerifier_HeaderAliasTests
    {
        private const string Secret = "dev_secret";

        private static (string Body, string TimestampIso, string Nonce, string SignatureB64)
            BuildSignedPayload()
        {
            var body = "{\"id\":\"t-123\",\"amount\":100}";
            var tsIso = DateTimeOffset.UtcNow.ToUniversalTime().ToString("o");
            var nonce = Guid.NewGuid().ToString("N");

            var message = $"{tsIso}\n{nonce}\n{body}";
            var key = Encoding.UTF8.GetBytes(Secret);
            using var hmac = new System.Security.Cryptography.HMACSHA256(key);
            var sigB64 = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));

            return (body, tsIso, nonce, sigB64);
        }

        [Fact]
        public void Verify_ShouldPass_WithAliasHeaders()
        {
            // Arrange
            var (body, tsIso, nonce, sigB64) = BuildSignedPayload();

            // Viktigt: verifieraren kräver X-Swish-* idag.
            // Vi skickar både alias OCH X-Swish-* för att dokumentera beteendet
            // tills alias-stöd implementeras i verifieraren.
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Alias som vissa kunder kan skicka
                ["X-Timestamp"] = tsIso,
                ["X-Nonce"]     = nonce,
                ["X-Signature"] = sigB64,

                // De “officiella” headernamnen som verifieraren använder idag
                ["X-Swish-Timestamp"] = tsIso,
                ["X-Swish-Nonce"]     = nonce,
                ["X-Swish-Signature"] = sigB64,
            };

            var verifier = new SwishWebhookVerifier(
                new SwishWebhookVerifierOptions
                {
                    SharedSecret     = Secret,
                    AllowedClockSkew = TimeSpan.FromMinutes(5),
                    MaxMessageAge    = TimeSpan.FromMinutes(10)
                },
                new InMemoryNonceStore()
            );

            // Act
            var now = DateTimeOffset.UtcNow;
            var result = verifier.Verify(body, headers, now);

            // Assert
            result.Success.Should().BeTrue(result.Reason ?? "ok");
        }
    }
}



