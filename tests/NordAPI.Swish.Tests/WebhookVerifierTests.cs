using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using NordAPI.Swish.Security.Webhooks;
using Xunit;

namespace NordAPI.Swish.Tests;

public class WebhookVerifierTests
{
    private static string MakeSig(string secret, string ts, string nonce, string body)
    {
        var canonical = $"{ts}\n{nonce}\n{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var mac = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToBase64String(mac);
    }

    [Fact]
    public void Verify_Succeeds_With_ValidHeaders_And_Body()
    {
        var secret = "dev-secret";
        var now = DateTimeOffset.UtcNow;
        var ts = now.ToUniversalTime().ToString("O");
        var nonce = Guid.NewGuid().ToString("N");
        var body = "{\"ping\":true}";

        var sig = MakeSig(secret, ts, nonce, body);

        var headers = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Swish-Signature"] = sig,
            ["X-Swish-Timestamp"] = ts,
            ["X-Swish-Nonce"] = nonce,
        };

        var opt = new SwishWebhookVerifierOptions
        {
            SharedSecret = secret,
            AllowedClockSkew = TimeSpan.FromMinutes(5),
            MaxMessageAge = TimeSpan.FromMinutes(10),
        };
        using var store = new InMemoryNonceStore();
        var verifier = new SwishWebhookVerifier(opt, store);

        var res = verifier.Verify(body, headers, now);
        Assert.True(res.Success);
        Assert.Null(res.Reason);
    }

    [Fact]
    public void Verify_Fails_On_Replay()
    {
        var secret = "dev-secret";
        var now = DateTimeOffset.UtcNow;
        var ts = now.ToUniversalTime().ToString("O");
        var nonce = Guid.NewGuid().ToString("N");
        var body = "{\"ping\":true}";
        var sig = MakeSig(secret, ts, nonce, body);

        var headers = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Swish-Signature"] = sig,
            ["X-Swish-Timestamp"] = ts,
            ["X-Swish-Nonce"] = nonce,
        };

        var opt = new SwishWebhookVerifierOptions { SharedSecret = secret };
        using var store = new InMemoryNonceStore();
        var verifier = new SwishWebhookVerifier(opt, store);

        var first = verifier.Verify(body, headers, now);
        var second = verifier.Verify(body, headers, now.AddSeconds(5));

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Contains("replay", second.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_Fails_On_TamperedBody()
    {
        var secret = "dev-secret";
        var now = DateTimeOffset.UtcNow;
        var ts = now.ToUniversalTime().ToString("O");
        var nonce = Guid.NewGuid().ToString("N");
        var body = "{\"amount\":100}";
        var sig = MakeSig(secret, ts, nonce, body);

        var headers = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Swish-Signature"] = sig,
            ["X-Swish-Timestamp"] = ts,
            ["X-Swish-Nonce"] = nonce,
        };

        var opt = new SwishWebhookVerifierOptions { SharedSecret = secret };
        using var store = new InMemoryNonceStore();
        var verifier = new SwishWebhookVerifier(opt, store);

        var tampered = "{\"amount\":999}";
        var res = verifier.Verify(tampered, headers, now);
        Assert.False(res.Success);
    }

    [Fact]
    public void Verify_Fails_When_TooOld()
    {
        var secret = "dev-secret";
        var now = DateTimeOffset.UtcNow;
        var tsOld = now.AddMinutes(-30).ToUniversalTime().ToString("O");
        var nonce = Guid.NewGuid().ToString("N");
        var body = "{}";
        var sig = MakeSig(secret, tsOld, nonce, body);

        var headers = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Swish-Signature"] = sig,
            ["X-Swish-Timestamp"] = tsOld,
            ["X-Swish-Nonce"] = nonce,
        };

        var opt = new SwishWebhookVerifierOptions
        {
            SharedSecret = secret,
            MaxMessageAge = TimeSpan.FromMinutes(10),
            AllowedClockSkew = TimeSpan.FromHours(1)
        };
        using var store = new InMemoryNonceStore();
        var verifier = new SwishWebhookVerifier(opt, store);

        var res = verifier.Verify(body, headers, now);
        Assert.False(res.Success);
        Assert.Contains("för gammalt", res.Reason!, StringComparison.OrdinalIgnoreCase);
    }
}
