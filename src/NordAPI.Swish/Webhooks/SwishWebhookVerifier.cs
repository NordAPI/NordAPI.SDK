using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace NordAPI.Swish.Webhooks;

/// <summary>
/// Verifies incoming Swish webhook requests by checking HMAC signature,
/// timestamp validity, and nonce replay protection.
/// </summary>
public sealed class SwishWebhookVerifier
{
    private static readonly StringComparison Ci = StringComparison.OrdinalIgnoreCase;

    private const string MissingSignature = "missing signature header";
    private const string MissingTimestamp = "missing timestamp header";
    private const string MissingNonce = "missing nonce header";
    private const string InvalidTimestamp = "invalid timestamp";
    private const string TimestampSkew = "timestamp outside tolerated clock skew";
    private const string MessageTooOld = "message too old";
    private const string ReplayDetected = "replay detected";
    private const string SignatureMismatch = "signature mismatch";

    private readonly SwishWebhookVerifierOptions _opt;
    private readonly ISwishNonceStore _nonceStore;
    /// <summary>
    /// Creates a verifier for incoming Swish webhook requests.
    /// </summary>
    /// <param name="options">Verifier options (shared secret, header names, timestamp tolerances).</param>
    /// <param name="nonceStore">Nonce store used for replay protection.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="nonceStore"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="SwishWebhookVerifierOptions.SharedSecret"/> is not configured.</exception>
    public SwishWebhookVerifier(SwishWebhookVerifierOptions options, ISwishNonceStore nonceStore)
    {
        _opt = options ?? throw new ArgumentNullException(nameof(options));
        _nonceStore = nonceStore ?? throw new ArgumentNullException(nameof(nonceStore));

        if (string.IsNullOrWhiteSpace(_opt.SharedSecret))
            throw new ArgumentException("SharedSecret must be configured.", nameof(options));
    }
    /// <summary>
    /// Verifies an incoming webhook using HMAC signature, timestamp validation, and nonce replay protection.
    /// </summary>
    /// <param name="body">Raw request body (byte-for-byte as received).</param>
    /// <param name="headers">Request headers.</param>
    /// <param name="nowUtc">Current UTC time used for deterministic validation.</param>
    /// <returns>The verification result.</returns>
    public VerifyResult Verify(string body, IReadOnlyDictionary<string, string> headers, DateTimeOffset nowUtc)
    {
        // 1) Signature
        if (!TryGetAny(headers, out var sigB64, _opt.SignatureHeaderName, "X-Swish-Signature", "X-Signature"))
            return VerifyResult.Fail(MissingSignature);

        sigB64 = sigB64.Trim();
        if (sigB64.Length == 0)
            return VerifyResult.Fail(MissingSignature);

        // 2) Timestamp
        if (!TryGetAny(headers, out var tsStr, _opt.TimestampHeaderName, "X-Swish-Timestamp", "X-Timestamp"))
            return VerifyResult.Fail(MissingTimestamp);

        tsStr = tsStr.Trim();
        if (tsStr.Length == 0)
            return VerifyResult.Fail(MissingTimestamp);

        // 3) Nonce
        if (!TryGetAny(headers, out var nonce, _opt.NonceHeaderName, "X-Swish-Nonce", "X-Nonce"))
            return VerifyResult.Fail(MissingNonce);

        nonce = nonce.Trim();
        if (nonce.Length == 0)
            return VerifyResult.Fail(MissingNonce);

        // 4) Parse timestamp (STRICT: Unix seconds only)
        if (!TryParseTimestamp(tsStr, out var tsUtc))
            return VerifyResult.Fail(InvalidTimestamp);

        // 5) Clock skew + age validation (deterministic reasons)
        var delta = nowUtc - tsUtc;

        // Too old: only past timestamps can be "too old"
        if (delta > _opt.MaxMessageAge)
            return VerifyResult.Fail(MessageTooOld);

        // Skew: both future and past drift outside tolerance
        if (delta.Duration() > _opt.AllowedClockSkew)
            return VerifyResult.Fail(TimestampSkew);

        // 6) Replay protection
        var expires = nowUtc.Add(_opt.MaxMessageAge);
        if (!_nonceStore.TryRemember(nonce, expires))
            return VerifyResult.Fail(ReplayDetected);

        // 7) Signature verification
        var canonical = $"{tsStr}\n{nonce}\n{body}";
        var key = Encoding.UTF8.GetBytes(_opt.SharedSecret);
        var data = Encoding.UTF8.GetBytes(canonical);

        using var hmac = new HMACSHA256(key);
        var mac = hmac.ComputeHash(data);
        var expectedB64 = Convert.ToBase64String(mac);

        if (!ConstantTimeEquals(expectedB64, sigB64))
            return VerifyResult.Fail(SignatureMismatch);

        return VerifyResult.Ok();
    }

#pragma warning disable S3267
    private static bool TryGetAny(IReadOnlyDictionary<string, string> headers, out string value, params string[] names)
    {
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            foreach (var kv in headers)
            {
                if (kv.Key.Equals(name, Ci))
                {
                    value = kv.Value ?? string.Empty;
                    return true;
                }
            }
        }

        value = string.Empty;
        return false;
    }
#pragma warning restore S3267

    private static bool TryParseTimestamp(string tsHeader, out DateTimeOffset tsUtc)
    {
        if (!long.TryParse(tsHeader, out var seconds))
        {
            tsUtc = default;
            return false;
        }

        try
        {
            tsUtc = DateTimeOffset.FromUnixTimeSeconds(seconds).ToUniversalTime();
            return true;
        }
        catch
        {
            tsUtc = default;
            return false;
        }
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);

        if (ba.Length != bb.Length)
            return false;

        var diff = 0;
        for (int i = 0; i < ba.Length; i++)
            diff |= ba[i] ^ bb[i];

        return diff == 0;
    }
    /// <summary>
    /// Result of a webhook verification attempt.
    /// </summary>
    /// <param name="Success">Indicates whether the verification was successful.</param>
    /// <param name="Reason">If verification failed, contains a deterministic reason for the failure.</param>
    public readonly record struct VerifyResult(bool Success, string? Reason)
    {
        /// <summary>
        /// Creates a successful verification result.
        /// </summary>
        /// <returns>A successful <see cref="VerifyResult"/>.</returns>
        public static VerifyResult Ok() => new(true, null);

        /// <summary>
        /// Creates a failed verification result with a deterministic reason.
        /// </summary>
        /// <param name="reason">The failure reason constant.</param>
        /// <returns>A failed <see cref="VerifyResult"/>.</returns>
        public static VerifyResult Fail(string reason) => new(false, reason);
    }
}
