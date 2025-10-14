#nullable enable
using System.Security.Cryptography;
using System.Text;

namespace NordAPI.Security;

/// <summary>
/// Computes and validates HMAC signatures with timestamp + nonce to prevent tamper and replay.
/// </summary>
public static class HmacVerifier
{
    /// <summary>
    /// Compute signature bytes using HMAC-SHA256 over canonical string:
    /// "{timestamp}\n{nonce}\n{payload}"
    /// </summary>
    public static byte[] ComputeSignatureBytes(
        string payload, string secret, DateTimeOffset timestamp, string nonce)
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
    /// Compute signature as lowercase hex string.
    /// </summary>
    public static string ComputeSignature(
        string payload, string secret, DateTimeOffset timestamp, string nonce)
        => ToHex(ComputeSignatureBytes(payload, secret, timestamp, nonce));

    /// <summary>
    /// Validates HMAC signature and enforces timestamp tolerance and nonce uniqueness.
    /// </summary>
    /// <param name="providedSignature">Expected to be lowercase hex.</param>
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

        // 1) Timestamp window
        var now = clock.UtcNow;
        var delta = now - timestamp;
        if (delta < -tolerance || delta > tolerance) return false;

        // 2) Nonce uniqueness
        var added = await nonceStore.TryAddAsync(nonce, nonceTtl, ct).ConfigureAwait(false);
        if (!added) return false; // replay

        // 3) HMAC
        var expected = ComputeSignatureBytes(payload, secret, timestamp, nonce);
        return ConstantTimeEqualsHex(providedSignature, expected);
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
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
            if (hi < 0 || lo < 0) return false;
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
