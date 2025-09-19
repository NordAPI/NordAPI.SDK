using System;
using FluentAssertions;
using NordAPI.Swish.Security.Webhooks;
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
                    SharedSecret     = Secret,
                    AllowedClockSkew = TimeSpan.FromMinutes(5),
                    MaxMessageAge    = TimeSpan.FromMinutes(10)
                },
                new InMemoryNonceStore()
            );

        [Fact]
        public void Verify_ShouldFail_WhenBodyIsAltered_AfterSigning()
        {
            var bodyOriginal = "{\"id\":\"abc123\",\"amount\":100}";
            var bodyAltered  = "{\"id\":\"abc123\",\"amount\":999}";
            var ts = DateTimeOffset.UtcNow;

            var (headers, _) = TestSigning.MakeHeaders(Secret, bodyOriginal, ts);
            var verifier = CreateVerifier();

            var result = verifier.Verify(bodyAltered, headers, ts);

            result.Success.Should().BeFalse();
            result.Reason.Should().NotBeNull();

            // Tillåt både svenska och engelska felmeddelanden
            result.Reason!
                .ToLowerInvariant()
                .Should()
                .ContainAny("signatur", "mismatch", "signature", "invalid");
        }

        [Fact]
        public void Verify_ShouldFail_WhenSignatureDoesNotMatch_Secret()
        {
            var body = "{\"id\":\"abc123\",\"amount\":100}";
            var ts   = DateTimeOffset.UtcNow;

            var (wrongHeaders, _) = TestSigning.MakeHeaders("WRONG_SECRET", body, ts);
            var verifier = CreateVerifier();

            var result = verifier.Verify(body, wrongHeaders, ts);

            result.Success.Should().BeFalse();
            result.Reason.Should().NotBeNull();

            // Tillåt både svenska och engelska felmeddelanden
            result.Reason!
                .ToLowerInvariant()
                .Should()
                .ContainAny("signatur", "mismatch", "signature", "invalid");
        }

        [Fact]
        public void Verify_ShouldFail_WhenRequiredHeaderMissing()
        {
            var body = "{\"id\":\"abc123\",\"amount\":100}";
            var ts   = DateTimeOffset.UtcNow;

            var (headers, _) = TestSigning.MakeHeaders(Secret, body, ts);
            headers.Remove("X-Swish-Signature");

            var verifier = CreateVerifier();

            var result = verifier.Verify(body, headers, ts);

            result.Success.Should().BeFalse();
            result.Reason.Should().NotBeNull();

            // Tillåt både svenska och engelska felmeddelanden
            result.Reason!
                .ToLowerInvariant()
                .Should()
                .ContainAny("header", "saknas", "missing", "signature");
        }
    }
}
