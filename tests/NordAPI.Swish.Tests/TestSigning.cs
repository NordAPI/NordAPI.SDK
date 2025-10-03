using NordAPI.Swish.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace NordAPI.Swish.Tests
{
    internal static class TestSigning
    {
        public static (Dictionary<string,string> Headers, string Message) MakeHeaders(
            string secret,
            string body,
            DateTimeOffset ts,
            string? nonce = null,
            bool useIsoTimestamp = true)
        {
            var tsStr = useIsoTimestamp
                ? ts.ToUniversalTime().ToString("o")   // ISO 8601
                : ts.ToUnixTimeSeconds().ToString();    // Unix sekunder

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

                // fallback-namn som sample-servern accepterar:
                ["X-Timestamp"] = tsStr,
                ["X-Nonce"]     = finalNonce,
                ["X-Signature"] = sig
            };

            return (headers, message);
        }
    }
}


