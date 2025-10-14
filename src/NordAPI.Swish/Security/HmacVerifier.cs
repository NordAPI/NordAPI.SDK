#nullable enable
using System.Security.Cryptography;
using System.Text;

namespace NordAPI.Security;

/// <summary>
/// Computes and validates HMAC signatures with timestamp + nonce
/// to prevent tampering and replay attacks.
/// </summary>
public static class HmacVerifier
{
    /// <summary>
    /// Computes the HMAC-SHA256 signature bytes for a canonical string:
    /// <c>"{timestamp}\n{nonce}\n{payload}"</c>.
    /// </summary>
    /// <param name="payload">The raw payload (body) to include in the signature.</param>
    /// <param name="secret">The shared HMAC secret as a UTF-8 string.</param>
    /// <param name="timestamp">The UTC timestamp of the message.</param>
    /// <param name="nonce">A unique nonce for replay protection.</param>
    /// <returns>The computed HMAC-SHA256 signature bytes.</returns>
    public static byte[] ComputeSignatureBytes(
        string payload,
        string secret,
        DateTimeOffset timestamp,
        string nonce)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);

        var canonical = $"{timestamp.UtcDateTime:O}\n{nonce}\n{payload}";
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(canonical);

        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    /// <summary>
    /// Computes the HMAC-SHA256 signature and returns it as a lowercase hexadecimal string.
    /// </summary>
    /// <param name="payload">The raw payload (body) to include in the signature.</param>
    /// <param name="secret">The shared HMAC secret as a UTF-8 string.</param>
    /// <param name="timestamp">The UTC timestamp of the message.</param>
    /// <param name="nonce">A unique nonce for replay protection.</param>
    /// <returns>The computed HMAC-SHA256 signature as a lowercase hex string.</returns>
    public static string ComputeSignature(
        string payload,
        string secret,
        DateTimeOffset timestamp,
        string nonce)
        => ToHex(ComputeSignatureBytes(payload, secret, timestamp, nonce));

    /// <summary>
    /// Validates the provided HMAC signature and enforces timestamp tolerance
    /// and nonce uniqueness to prevent replay attacks.
    /// </summary>
    /// <param name="payload">The raw request body.</param>
    /// <param name="providedSignature">The provided HMAC signature (expected lowercase hex).</param>
    /// <param name="secret">The shared HMAC secret used to compute the signature.</param>
    /// <param name="nonce">A unique nonce that must not have been used before.</param>
    /// <param name="timestamp">The UTC timestamp of the message.</param>
    /// <param name="clock">Abstraction for obtaining current UTC time (for testability).</param>
    /// <param name="nonceStore">Store for remembering used nonces (anti-replay).</param>
    /// <param name="tolerance">Maximum allowed timestamp skew (e.g. ±5 minutes).</param>
    /// <param name="nonceTtl">How long a used nonce should remain stored to prevent reuse.</param>
    /// <param name="ct">A cancellation token for async operations.</param>
    /// <returns><c>true</c> if the signature and nonce are valid and within time tolerance; otherwise <c>false</c>.</returns>
    public static async ValueTask<bool> IsValidAsync(
        string payload,
        string providedSignature,
        string secret,
        string nonce,
        DateTimeOffset timestamp,
        IClock clock,
        INonceStore nonceStore,
        TimeSpan tolerance,
        TimeSpan nonceTtl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providedSignature)) return false;
        if (clock is null) throw new ArgumentNullException(nameof(clock));
        if (nonceStore is null) throw new ArgumentNullException(nameof(nonceStore));

        // 1) Check timestamp window
        var now = clock.UtcNow;
        var delta = now - timestamp;
        if (delta < -tolerance || delta > tolerance)
            return false;

        // 2) Ensure nonce is unique (anti-replay)
        var added = await nonceStore.TryAddAsync(nonce, nonceTtl, ct).ConfigureAwait(false);
        if (!added)
            return false;

        // 3) Verify HMAC signature
        var expected = ComputeSignatureBytes(payload, secret, timestamp, nonce);
        return ConstantTimeEqualsHex(providedSignature, expected);
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static bool ConstantTimeEqualsHex(string providedHex, ReadOnlySpan<byte> expectedBytes)
    {
        // Normalize provided hex → bytes; any invalid hex fails.
        if (string.IsNullOrWhiteSpace(providedHex) || providedHex.Length != expectedBytes.Length * 2)
            return false;

        Span<byte> providedBytes = stackalloc byte[expectedBytes.Length];
        for (int i = 0; i < expectedBytes.Length; i++)
        {
            var hi = HexNibble(providedHex[2 * i]);
            var lo = HexNibble(providedHex[2 * i + 1]);
            if (hi < 0 || lo < 0)
                return false;
            providedBytes[i] = (byte)((hi << 4) | lo);
        }

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    private static int HexNibble(char c)
    {
        if ((uint)(c - '0') <= 9) return c - '0';
        c |= (char)0x20; // to lower
        if ((uint)(c - 'a') <= 5) return c - 'a' + 10;
        return -1;
    }
}

