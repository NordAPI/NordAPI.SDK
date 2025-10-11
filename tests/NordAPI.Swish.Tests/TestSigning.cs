using System;
using System.Collections.Generic;
using System.Text;

namespace NordAPI.Swish.Tests
{
    /// <summary>
    /// Provides helper methods for generating valid Swish HMAC headers for test and integration purposes.
    /// </summary>
    internal static class TestSigning
    {
        /// <summary>
        /// Creates a complete set of Swish webhook headers (timestamp, nonce, signature) for testing purposes.
        /// </summary>
        /// <param name="secret">The shared HMAC secret used for signature computation.</param>
        /// <param name="body">The raw request body to be signed.</param>
        /// <param name="ts">The timestamp to include in the header.</param>
        /// <param name="nonce">Optional unique identifier. If omitted, a new GUID (N-format) will be generated.</param>
        /// <param name="useIsoTimestamp">
        /// Determines the timestamp format. 
        /// True → ISO 8601 (default). False → Unix seconds.
        /// </param>
        /// <returns>
        /// A tuple containing the generated headers and the canonical message used for signature computation.
        /// </returns>
        public static (Dictionary<string, string> Headers, string Message) MakeHeaders(
            string secret,
            string body,
            DateTimeOffset ts,
            string? nonce = null,
            bool useIsoTimestamp = true)
        {
            var tsStr = useIsoTimestamp
                ? ts.ToUniversalTime().ToString("o")   // ISO 8601
                : ts.ToUnixTimeSeconds().ToString();    // Unix seconds

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

                // Fallback aliases accepted by the sample server:
                ["X-Timestamp"] = tsStr,
                ["X-Nonce"]     = finalNonce,
                ["X-Signature"] = sig
            };

            return (headers, message);
        }
    }
}



