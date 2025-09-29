using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using NordAPI.Swish.Webhooks;
using Xunit;

namespace NordAPI.Swish.Tests
{
    public class WebhookVerifier_NegativeTests
    {
        private const string Secret = "dev_secret";

        private SwishWebhookVerifier CreateVerifier() =>
            new SwishWebhookVerifier(
                new SwishWebhookVerifierOptions
                {
                    SharedSecret = Secret,
                    AllowedClockSkew = TimeSpan.FromMinutes(5),
                    MaxMessageAge   = TimeSpan.FromMinutes(10),
                    SignatureHeaderName = "X-Swish-Signature",
                    TimestampHeaderName = "X-Swish-Timestamp",
                    NonceHeaderName     = "X-Swish-Nonce",
                },
                new InMemoryNonceStore()
            );

        private static Dictionary<string, string> MakeHeaders(
            string secret,
            string body,
            DateTimeOffset ts,
            string? nonce = null)
        {
            var tsStr      = ts.ToUniversalTime().ToString("o");
            var finalNonce = nonce ?? Guid.NewGuid().ToString("N");
            var message    = $"{tsStr}\n{finalNonce}\n{body}";
            var key        = Encoding.UTF8.GetBytes(secret);
            using var hmac = new System.Security.Cryptography.HMACSHA256(key);
            var sig        = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));

            // Lägger både primära och alias för att kunna testa borttagning av båda
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Swish-Timestamp"] = tsStr,
                ["X-Swish-Nonce"]     = finalNonce,
                ["X-Swish-Signature"] = sig,

                ["X-Timestamp"] = tsStr,
                ["X-Nonce"]     = finalNonce,
                ["X-Signature"] = sig
            };
        }

        [Fact]
        public void Verify_ShouldFail_WhenSignatureDoesNotMatch_Secret()
        {
            var body = "{\"id\":\"abc123\",\"amount\":100}";
            var ts   = DateTimeOffset.UtcNow;

            var hdrs = MakeHeaders("WRONG_SECRET", body, ts); // fel hemlighet
            var verifier = CreateVerifier();

            var result = verifier.Verify(body, hdrs, ts);

            result.Success.Should().BeFalse();
            result.Reason!.ToLowerInvariant().Should().ContainAny("signatur", "signature", "mismatch", "invalid");
        }

        [Fact]
        public void Verify_ShouldFail_WhenBodyIsAltered_AfterSigning()
        {
            var bodyOriginal = "{\"id\":\"abc123\",\"amount\":100}";
            var bodyAltered  = "{\"id\":\"abc123\",\"amount\":999}";
            var ts = DateTimeOffset.UtcNow;

            var hdrs = MakeHeaders(Secret, bodyOriginal, ts);
            var verifier = CreateVerifier();

            var result = verifier.Verify(bodyAltered, hdrs, ts);

            result.Success.Should().BeFalse();
            result.Reason!.ToLowerInvariant().Should().ContainAny("signatur", "signature", "mismatch", "invalid");
        }

        [Fact]
        public void Verify_ShouldFail_WhenRequiredHeaderMissing()
        {
            var body = "{\"id\":\"abc123\",\"amount\":100}";
            var ts   = DateTimeOffset.UtcNow;

            var hdrs = MakeHeaders(Secret, body, ts);

            // Ta bort signatur helt (både primär och alias)
            hdrs.Remove("X-Swish-Signature");
            hdrs.Remove("X-Signature");

            var verifier = CreateVerifier();
            var result = verifier.Verify(body, hdrs, ts);

            result.Success.Should().BeFalse();
            result.Reason!.ToLowerInvariant().Should().ContainAny("saknar", "missing", "signatur", "signature");
        }
    }
}

