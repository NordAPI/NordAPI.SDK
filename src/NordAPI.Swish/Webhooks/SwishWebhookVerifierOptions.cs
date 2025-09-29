using System;

namespace NordAPI.Swish.Webhooks;

public sealed class SwishWebhookVerifierOptions
{
    /// <summary>Delad hemlighet för HMAC-signaturen.</summary>
    public string SharedSecret { get; set; } = string.Empty;

    /// <summary>Maximal klock-avvikelse som tolereras.</summary>
    public TimeSpan AllowedClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Maximal ålder på meddelandet.</summary>
    public TimeSpan MaxMessageAge { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Namn på header som innehåller signaturen (Base64).</summary>
    public string SignatureHeaderName { get; set; } = "X-Swish-Signature";

    /// <summary>Namn på header med UTC-timestamp (RFC3339/ISO8601).</summary>
    public string TimestampHeaderName { get; set; } = "X-Swish-Timestamp";

    /// <summary>Namn på header med unik nonce (GUID/sträng).</summary>
    public string NonceHeaderName { get; set; } = "X-Swish-Nonce";
}

