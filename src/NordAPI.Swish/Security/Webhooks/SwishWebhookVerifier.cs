using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace NordAPI.Swish.Security.Webhooks;

public sealed class SwishWebhookVerifier
{
    private readonly SwishWebhookVerifierOptions _opt;
    private readonly ISwishNonceStore _nonceStore;

    public SwishWebhookVerifier(SwishWebhookVerifierOptions options, ISwishNonceStore nonceStore)
    {
        _opt = options ?? throw new ArgumentNullException(nameof(options));
        _nonceStore = nonceStore ?? throw new ArgumentNullException(nameof(nonceStore));
        if (string.IsNullOrWhiteSpace(_opt.SharedSecret))
            throw new ArgumentException("SharedSecret måste vara satt.", nameof(options));
    }

    public VerifyResult Verify(string body, IReadOnlyDictionary<string, string> headers, DateTimeOffset nowUtc)
    {
        if (!TryGet(headers, _opt.SignatureHeaderName, out var sigB64))
            return VerifyResult.Fail("saknar signaturheader");
        if (!TryGet(headers, _opt.TimestampHeaderName, out var tsStr))
            return VerifyResult.Fail("saknar timestampheader");
        if (!TryGet(headers, _opt.NonceHeaderName, out var nonce))
            return VerifyResult.Fail("saknar nonceheader");

        if (!DateTimeOffset.TryParse(tsStr, out var ts))
            return VerifyResult.Fail("ogiltig timestamp");

        // Klockskew/ålder
        var diff = (nowUtc - ts).Duration();
        if (diff > _opt.AllowedClockSkew)
            return VerifyResult.Fail("timestamp utanför tolerated clock skew");

        if (nowUtc - ts > _opt.MaxMessageAge)
            return VerifyResult.Fail("meddelandet för gammalt");

        // Anti-replay
        var expires = nowUtc.Add(_opt.MaxMessageAge);
        if (!_nonceStore.TryRemember(nonce, expires))
            return VerifyResult.Fail("replay upptäckt (nonce sedd tidigare)");

        // Canonical string: "{timestamp}\n{nonce}\n{body}"
        var canonical = $"{tsStr}\n{nonce}\n{body}";
        var key = Encoding.UTF8.GetBytes(_opt.SharedSecret);
        var data = Encoding.UTF8.GetBytes(canonical);
        using var hmac = new HMACSHA256(key);
        var mac = hmac.ComputeHash(data);
        var expectedB64 = Convert.ToBase64String(mac);

        if (!ConstantTimeEquals(expectedB64, sigB64))
            return VerifyResult.Fail("signatur mismatch");

        return VerifyResult.Ok();
    }

    private static bool TryGet(IReadOnlyDictionary<string, string> headers, string name, out string value)
    {
        foreach (var kv in headers)
        {
            if (kv.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }
        value = "";
        return false;
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        // jämför Base64-strängar i konstant tid
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;

        var diff = 0;
        for (int i = 0; i < ba.Length; i++)
            diff |= ba[i] ^ bb[i];

        return diff == 0;
    }

    public readonly record struct VerifyResult(bool Success, string? Reason)
    {
        public static VerifyResult Ok() => new(true, null);
        public static VerifyResult Fail(string reason) => new(false, reason);
    }
}
