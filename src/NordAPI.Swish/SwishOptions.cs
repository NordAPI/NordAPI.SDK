using System;

namespace NordAPI.Swish;

/// <summary>
/// Core configuration for the Swish client.
/// </summary>
public sealed class SwishOptions
{
    /// <summary>
    /// Base address of the Swish API (e.g., sandbox/test endpoint).
    /// </summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// Public API key identifier used for HMAC authentication.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Shared secret used to compute/verify HMAC signatures.
    /// </summary>
    public string? Secret { get; set; }

    /// <summary>
    /// Optional mTLS options (PFX path, password, dev relax setting).
    /// </summary>
    public SwishCertificateOptions? Certificate { get; set; }
}
