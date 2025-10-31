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
    /// Negative test cases for SwishWebhookVerifier to ensure invalid requests are rejected.
    /// </summary>
    public class WebhookVerifier_NegativeTests
    {
        private const string Secret = "dev_secret";

        /// <summary>
        /// Creates a SwishWebhookVerifier with default options for testing.
        /// </summary>
        private SwishWebhookVerifier CreateVerifier() =>
            new SwishWebhookVerifier(
                new SwishWebhookVerifierOptions
                {
                    SharedSecret        = Secret,
                    AllowedClockSkew    = TimeSpan.FromMinutes(5),
                    MaxMessageAge       = TimeSpan.FromMinutes(10),
                    SignatureHeaderName = "X-Swish-Signature",
                    TimestampHeaderName = "X-Swish-Timestamp",
                    NonceHeaderName     = "X-Swish-Nonce"
                },
                new InMemoryNonceStore()
            );

        /// <summary>
        /// Builds a headers dictionary containing:
        ///  - ISO-8601 timestamp
        ///  - nonce
        ///  - Base64-encoded HMAC-SHA256 signature
        /// Includes both primary and alias header names to support removal tests.
        /// </summary>
        private static Dictionary<string, string> MakeHeaders(
            string secret,
            string body,
            DateTimeOffset ts,
            string? nonce = null)
        {
            var timestamp  = ts.ToUniversalTime().ToString("o");
            var finalNonce = nonce ?? Guid.NewGuid().ToString("N");
            var message    = $"{timestamp}\n{finalNonce}\n{body}";
            var key        = Encoding.UTF8.GetBytes(secret);

            using var hmac = new System.Security.Cryptography.HMACSHA256(key);
            var signature  = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Swish-Timestamp"] = timestamp,
                ["X-Swish-Nonce"]     = finalNonce,
                ["X-Swish-Signature"] = signature,

                ["X-Timestamp"]       = timestamp,
                ["X-Nonce"]           = finalNonce,
                ["X-Signature"]       = signature
            };
        }

        [Fact]
        public void Verify_ShouldFail_WhenSignatureDoesNotMatch_Secret()
        {
            // Arrange: headers signed with wrong secret
            var body      = "{\"id\":\"abc123\",\"amount\":100}";
            var timestamp = DateTimeOffset.UtcNow;
            var headers   = MakeHeaders("WRONG_SECRET", body, timestamp);

            var verifier = CreateVerifier();

            // Act
            var result   = verifier.Verify(body, headers, timestamp);

            // Assert: signature validation must fail
            result.Success
                  .Should().BeFalse();
            result.Reason!
                  .ToLowerInvariant()
                  .Should().ContainAny("signature", "mismatch", "invalid");
        }

        [Fact]
        public void Verify_ShouldFail_WhenBodyIsAltered_AfterSigning()
        {
            // Arrange: sign original body, then verify with altered body
            var originalBody = "{\"id\":\"abc123\",\"amount\":100}";
            var alteredBody  = "{\"id\":\"abc123\",\"amount\":999}";
            var timestamp    = DateTimeOffset.UtcNow;
            var headers      = MakeHeaders(Secret, originalBody, timestamp);

            var verifier = CreateVerifier();

            // Act
            var result   = verifier.Verify(alteredBody, headers, timestamp);

            // Assert: HMAC mismatch must be detected
            result.Success
                  .Should().BeFalse();
            result.Reason!
                  .ToLowerInvariant()
                  .Should().ContainAny("signature", "mismatch", "invalid");
        }

        [Fact]
        public void Verify_ShouldFail_WhenRequiredHeaderMissing()
        {
            // Arrange: remove signature headers from an otherwise valid request
            var body      = "{\"id\":\"abc123\",\"amount\":100}";
            var timestamp = DateTimeOffset.UtcNow;
            var headers   = MakeHeaders(Secret, body, timestamp);

            headers.Remove("X-Swish-Signature");
            headers.Remove("X-Signature");

            var verifier = CreateVerifier();

            // Act
            var result   = verifier.Verify(body, headers, timestamp);

            // Assert: missing header should cause failure
            result.Success
                  .Should().BeFalse();
            result.Reason!
                  .ToLowerInvariant()
                  .Should().ContainAny("missing", "signature");

        }
    }
}




