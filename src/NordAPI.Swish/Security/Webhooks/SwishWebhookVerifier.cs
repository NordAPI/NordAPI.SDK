using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NordAPI.Swish.Security.Webhooks;

/// <summary>
/// Verifierar Swish-webhookar.
/// <para>
/// Stöder följande header-namn (alias accepteras automagiskt):
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <c>X-Swish-Timestamp</c>, <c>X-Swish-Signature</c>, <c>X-Swish-Nonce</c>
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>X-Timestamp</c>, <c>X-Signature</c>, <c>X-Nonce</c>
///     </description>
///   </item>
/// </list>
///
/// <para>
/// Canonical-strängen som signeras är:
/// </para>
/// <code>
/// {timestamp}\n{nonce}\n{body}
/// </code>
/// <para>där:</para>
/// <list type="bullet">
///   <item><description><c>timestamp</c> är exakt texten från headern (utan omformatering).</description></item>
///   <item><description><c>nonce</c> är samma värde som i headern.</description></item>
///   <item><description><c>body</c> är rå request-body oförändrad.</description></item>
/// </list>
///
/// <para>
/// Timestamp-format som accepteras:
/// </para>
/// <list type="bullet">
///   <item><description>Unix sekunder (t.ex. <c>1757962690</c>)</description></item>
///   <item><description>Unix millisekunder (t.ex. <c>1757960508877</c>)</description></item>
///   <item><description>ISO-8601 (t.ex. <c>2025-09-15T18:21:01Z</c>)</description></item>
/// </list>
/// </summary>
public sealed class SwishWebhookVerifier
{
    private static readonly StringComparison Ci = StringComparison.OrdinalIgnoreCase;

    private readonly SwishWebhookVerifierOptions _opt;
    private readonly ISwishNonceStore _nonceStore;

    /// <summary>
    /// Skapar en verifierare.
    /// </summary>
    /// <param name="options">Verifieringsinställningar (måste innehålla <see cref="SwishWebhookVerifierOptions.SharedSecret"/>).</param>
    /// <param name="nonceStore">Lagring för anti-replay (nonces).</param>
    /// <exception cref="ArgumentNullException">Om <paramref name="options"/> eller <paramref name="nonceStore"/> är null.</exception>
    /// <exception cref="ArgumentException">Om <see cref="SwishWebhookVerifierOptions.SharedSecret"/> saknas.</exception>
    public SwishWebhookVerifier(SwishWebhookVerifierOptions options, ISwishNonceStore nonceStore)
    {
        _opt = options ?? throw new ArgumentNullException(nameof(options));
        _nonceStore = nonceStore ?? throw new ArgumentNullException(nameof(nonceStore));
        if (string.IsNullOrWhiteSpace(_opt.SharedSecret))
            throw new ArgumentException("SharedSecret måste vara satt.", nameof(options));
    }

    /// <summary>
    /// Verifierar signatur, tidsfönster och replay-skydd.
    /// </summary>
    /// <param name="body">Rå request-body (oförändrad).</param>
    /// <param name="headers">Inkommande headers.</param>
    /// <param name="nowUtc">Nuvarande tid i UTC (för testbarhet).</param>
    /// <returns><see cref="VerifyResult"/> med <c>Success=true</c> om allt stämmer.</returns>
    public VerifyResult Verify(string body, IReadOnlyDictionary<string, string> headers, DateTimeOffset nowUtc)
    {
        // 1) Läs headers (tillåt alias)
        if (!TryGetAny(headers, out var sigB64, _opt.SignatureHeaderName, "X-Swish-Signature", "X-Signature"))
            return VerifyResult.Fail("saknar signaturheader");

        if (!TryGetAny(headers, out var tsStr, _opt.TimestampHeaderName, "X-Swish-Timestamp", "X-Timestamp"))
            return VerifyResult.Fail("saknar timestampheader");

        if (!TryGetAny(headers, out var nonce, _opt.NonceHeaderName, "X-Swish-Nonce", "X-Nonce"))
            return VerifyResult.Fail("saknar nonceheader");

        // 2) Tolka timestamp
        if (!TryParseTimestamp(tsStr, out var tsUtc))
            return VerifyResult.Fail("ogiltig timestamp");

        // 3) Klockskew och ålder
        var skew = (nowUtc - tsUtc).Duration();
        if (skew > _opt.AllowedClockSkew)
            return VerifyResult.Fail("timestamp utanför tolerated clock skew");

        if (nowUtc - tsUtc > _opt.MaxMessageAge)
            return VerifyResult.Fail("meddelandet för gammalt");

        // 4) Anti-replay (nonce måste vara ny)
        var expires = nowUtc.Add(_opt.MaxMessageAge);
        if (!_nonceStore.TryRemember(nonce, expires))
            return VerifyResult.Fail("replay upptäckt (nonce sedd tidigare)");

        // 5) Signaturkontroll (Base64(HMACSHA256(secret, "{ts}\n{nonce}\n{body}")))
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

    /// <summary>
    /// Hämtar ett headervärde med stöd för flera aliasnamn.
    /// </summary>
    private static bool TryGetAny(IReadOnlyDictionary<string, string> headers, out string value, params string[] names)
    {
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            foreach (var kv in headers)
            {
                if (kv.Key.Equals(name, Ci))
                {
                    value = kv.Value;
                    return true;
                }
            }
        }
        value = "";
        return false;
    }

    /// <summary>
    /// Accepterar Unix sekunder, Unix millisekunder och ISO-8601 (UTC).
    /// </summary>
    private static bool TryParseTimestamp(string tsHeader, out DateTimeOffset tsUtc)
    {
        // Unix (sekunder/millis)
        if (long.TryParse(tsHeader, out var num))
        {
            if (tsHeader.Length >= 13) // millis
            {
                tsUtc = DateTimeOffset.FromUnixTimeMilliseconds(num).ToUniversalTime();
                return true;
            }
            tsUtc = DateTimeOffset.FromUnixTimeSeconds(num).ToUniversalTime();
            return true;
        }

        // ISO-8601
        if (DateTimeOffset.TryParse(
                tsHeader,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var iso))
        {
            tsUtc = iso.ToUniversalTime();
            return true;
        }

        tsUtc = default;
        return false;
    }

    /// <summary>
    /// Konstanttid-jämförelse av Base64-strängar.
    /// </summary>
    private static bool ConstantTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;

        var diff = 0;
        for (int i = 0; i < ba.Length; i++)
            diff |= ba[i] ^ bb[i];

        return diff == 0;
    }

    /// <summary>
    /// Resultat från verifiering.
    /// </summary>
    public readonly record struct VerifyResult(bool Success, string? Reason)
    {
        /// <summary>OK.</summary>
        public static VerifyResult Ok() => new(true, null);
        /// <summary>Misslyckades med angiven orsak.</summary>
        public static VerifyResult Fail(string reason) => new(false, reason);
    }
}
