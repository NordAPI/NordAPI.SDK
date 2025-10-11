using NordAPI.Swish.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using FluentAssertions;
using NordAPI.Swish.Webhooks;
using Xunit;

namespace NordAPI.Swish.Tests.Webhooks
{
    // Unit tests for SwishWebhookVerifier
    public class SwishWebhookVerifierTests
    {
        // Compute a Base64-encoded HMAC-SHA256 over the canonical string
        private static string Sign(string secret, string canonical)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToBase64String(sig);
        }

        // Create a verifier instance with an in-memory nonce store
        private static SwishWebhookVerifier CreateVerifier(string secret)
        {
            var opts = new SwishWebhookVerifierOptions { SharedSecret = secret };
            var nonces = new InMemoryNonceStore(TimeSpan.FromMinutes(10));
            return new SwishWebhookVerifier(opts, nonces);
        }

        // Valid timestamp, nonce, and signature should pass verification
        [Fact]
        public void Verify_Succeeds_With_Valid_Timestamp_Nonce_Signature()
        {
            var secret = "dev_secret";
            var body   = "{\"id\":\"t1\",\"amount\":50}";
            var ts     = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var nonce  = Guid.NewGuid().ToString("N");

            var canonical = $"{ts}\n{nonce}\n{body}";
            var sigB64    = Sign(secret, canonical);

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Swish-Timestamp"] = ts,
                ["X-Swish-Nonce"]     = nonce,
                ["X-Swish-Signature"] = sigB64
            };

            var verifier = CreateVerifier(secret);
            var result   = verifier.Verify(body, headers, DateTimeOffset.UtcNow);

            result.Success.Should().BeTrue(result.Reason);
        }

        // Reusing the same nonce should fail (prevent replay attacks)
        [Fact]
        public void Verify_Fails_On_Replay_Nonce()
        {
            var secret = "dev_secret";
            var body   = "{\"id\":\"t2\",\"amount\":50}";
            var ts     = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var nonce  = Guid.NewGuid().ToString("N");
            var canonical = $"{ts}\n{nonce}\n{body}";
            var sigB64 = Sign(secret, canonical);

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Swish-Timestamp"] = ts,
                ["X-Swish-Nonce"]     = nonce,
                ["X-Swish-Signature"] = sigB64
            };

            var verifier = CreateVerifier(secret);

            var first = verifier.Verify(body, headers, DateTimeOffset.UtcNow);
            first.Success.Should().BeTrue(first.Reason);

            var second = verifier.Verify(body, headers, DateTimeOffset.UtcNow);
            second.Success.Should().BeFalse();
        }

        // Wrong canonical string order should cause verification to fail
        [Fact]
        public void Verify_Fails_When_Canonical_Is_Wrong()
        {
            var secret = "dev_secret";
            var body   = "{\"id\":\"t3\",\"amount\":50}";
            var ts     = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var nonce  = Guid.NewGuid().ToString("N");

            // Swap nonce and body in the canonical string
            var wrongCanonical = $"{ts}\n{body}\n{nonce}";
            var sigB64 = Sign(secret, wrongCanonical);

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Swish-Timestamp"] = ts,
                ["X-Swish-Nonce"]     = nonce,
                ["X-Swish-Signature"] = sigB64
            };

            var verifier = CreateVerifier(secret);
            var result   = verifier.Verify(body, headers, DateTimeOffset.UtcNow);

            result.Success.Should().BeFalse();
        }

        // Timestamps outside the allowed window should be rejected
        [Fact]
        public void Verify_Fails_When_Timestamp_Too_Old()
        {
            var secret = "dev_secret";
            var body   = "{\"id\":\"t4\",\"amount\":50}";
            var oldTs  = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
            var nonce  = Guid.NewGuid().ToString("N");

            var canonical = $"{oldTs}\n{nonce}\n{body}";
            var sigB64    = Sign(secret, canonical);

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Swish-Timestamp"] = oldTs,
                ["X-Swish-Nonce"]     = nonce,
                ["X-Swish-Signature"] = sigB64
            };

            var verifier = CreateVerifier(secret);
            var result   = verifier.Verify(body, headers, DateTimeOffset.UtcNow);

            result.Success.Should().BeFalse();
        }
    }
}



