using NordAPI.Swish.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using NordAPI.Swish.Webhooks;
using Xunit;

namespace NordAPI.Swish.Tests
{
    /// <summary>
    /// Ensures the verifier accepts alias header names for timestamp/signature/nonce.
    /// </summary>
    public class WebhookVerifier_HeaderAliasTests
    {
        private const string Secret = "dev_secret";

        /// <summary>
        /// Builds a signed payload tuple containing:
        /// - Body JSON string
        /// - ISO-8601 timestamp
        /// - Nonce
        /// - Base64-encoded HMAC-SHA256 signature
        /// </summary>
        private static (string Body, string TimestampIso, string Nonce, string SignatureB64)
            BuildSignedPayload()
        {
            var body   = "{\"id\":\"t-123\",\"amount\":100}";
            var tsIso  = DateTimeOffset.UtcNow
                            .ToUniversalTime()
                            .ToString("o");
            var nonce  = Guid.NewGuid().ToString("N");

            // Canonical message format: timestamp\nnonce\nbody
            var message = $"{tsIso}\n{nonce}\n{body}";
            var key     = Encoding.UTF8.GetBytes(Secret);
            using var hmac = new System.Security.Cryptography.HMACSHA256(key);
            var sigB64  = Convert.ToBase64String(
                            hmac.ComputeHash(
                              Encoding.UTF8.GetBytes(message)));

            return (body, tsIso, nonce, sigB64);
        }

        [Fact]
        public void Verify_ShouldPass_WithAliasHeaders()
        {
            // Arrange: create a signed payload
            var (body, tsIso, nonce, sigB64) = BuildSignedPayload();

            // Combine alias headers (used by some clients)
            // with the official Swish header names.
            var headers = new Dictionary<string, string>(
                              StringComparer.OrdinalIgnoreCase)
            {
                // Alias header names
                ["X-Timestamp"] = tsIso,
                ["X-Nonce"]     = nonce,
                ["X-Signature"] = sigB64,

                // Official Swish header names
                ["X-Swish-Timestamp"] = tsIso,
                ["X-Swish-Nonce"]     = nonce,
                ["X-Swish-Signature"] = sigB64,
            };

            // Use default verifier options
            var verifier = new SwishWebhookVerifier(
                new SwishWebhookVerifierOptions
                {
                    SharedSecret     = Secret,
                    AllowedClockSkew = TimeSpan.FromMinutes(5),
                    MaxMessageAge    = TimeSpan.FromMinutes(10)
                },
                new InMemoryNonceStore()
            );

            // Act: verify the request at the current time
            var now    = DateTimeOffset.UtcNow;
            var result = verifier.Verify(body, headers, now);

            // Assert: verification should succeed when alias headers are present
            result.Success
                  .Should().BeTrue(result.Reason ?? "ok");
        }
    }
}




