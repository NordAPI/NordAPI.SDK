using NordAPI.Swish.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using FluentAssertions;
using NordAPI.Swish.Webhooks;
using Xunit;

namespace NordAPI.Swish.Tests.Webhooks;

public class SwishWebhookVerifierTests
{
    private static string Sign(string secret, string canonical)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToBase64String(sig);
    }

    private static SwishWebhookVerifier CreateVerifier(string secret)
    {
        var opts = new SwishWebhookVerifierOptions { SharedSecret = secret };
        var nonces = new InMemoryNonceStore(TimeSpan.FromMinutes(10));
        return new SwishWebhookVerifier(opts, nonces);
    }

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

        // Replay med samma nonce
        var second = verifier.Verify(body, headers, DateTimeOffset.UtcNow);
        second.Success.Should().BeFalse();
    }

    [Fact]
    public void Verify_Fails_When_Canonical_Is_Wrong()
    {
        var secret = "dev_secret";
        var body   = "{\"id\":\"t3\",\"amount\":50}";
        var ts     = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce  = Guid.NewGuid().ToString("N");

        // Fel canonical (byter plats pÃ¥ nonce/body)
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

        // FÃ¶rvÃ¤ntar oss att verifieraren nekar pga tidsfÃ¶nster (Â±5 min i sample)
        result.Success.Should().BeFalse();
    }
}


