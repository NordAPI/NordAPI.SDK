using System;
using System.Collections.Generic;
using System.Text;

namespace NordAPI.Swish.Tests;

/// <summary>
/// Helper for constructing signed webhook headers used by tests.
/// Produces canonical message: "{timestamp}\n{nonce}\n{body}" and Base64(HMACSHA256(...)).
/// Supports both ISO-8601 and Unix seconds for the timestamp to match accepted input.
/// </summary>
internal static class TestSigning
{
    public static (Dictionary<string, string> Headers, string Message) MakeHeaders(
        string secret,
        string body,
        DateTimeOffset ts,
        string? nonce = null,
        bool useIsoTimestamp = true)
    {
        var tsStr = useIsoTimestamp
            ? ts.ToUniversalTime().ToString("o")  // ISO 8601
            : ts.ToUnixTimeSeconds().ToString();  // Unix seconds

        var finalNonce = nonce ?? Guid.NewGuid().ToString("N");
        var message = $"{tsStr}\n{finalNonce}\n{body}";

        var key = Encoding.UTF8.GetBytes(secret);
        using var hmac = new System.Security.Cryptography.HMACSHA256(key);
        var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Swish-Timestamp"] = tsStr,
            ["X-Swish-Nonce"]     = finalNonce,
            ["X-Swish-Signature"] = sig,

            // Fallback aliases accepted by the sample:
            ["X-Timestamp"] = tsStr,
            ["X-Nonce"]     = finalNonce,
            ["X-Signature"] = sig
        };

        return (headers, message);
    }
}




