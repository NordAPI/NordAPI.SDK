using System;

namespace NordAPI.Swish;

/// <summary>
/// Options for configuring client certificate (mTLS) loading for Swish HTTP calls.
/// When both <see cref="PfxPath"/> and <see cref="PfxPassword"/> are provided, the SDK
/// attempts to load a client certificate for mutual TLS.
/// </summary>
public sealed class SwishCertificateOptions
{
    /// <summary>
    /// Absolute or relative file path to a .pfx client certificate. If null or empty,
    /// mTLS is not enabled and the SDK falls back to a non-mTLS HttpClient.
    /// </summary>
    public string? PfxPath { get; set; }

    /// <summary>
    /// Password used to decrypt the PFX. Required if <see cref="PfxPath"/> is set.
    /// </summary>
    public string? PfxPassword { get; set; }

    /// <summary>
    /// For local development only: allows relaxed server certificate validation in DEBUG.
    /// Never enable this in production.
    /// </summary>
    public bool AllowInvalidChainForDev { get; set; } = false;
}
