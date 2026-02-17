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

    /// <summary>
    /// When true (default), the SDK requires a client certificate to be configured for mTLS.
    /// If no certificate can be resolved, the SDK will throw a <c>SwishConfigurationException</c>.
    /// Set to false only for controlled development/testing scenarios where mTLS is intentionally not used.
    /// </summary>
    public bool RequireMtls { get; set; } = true;
}

