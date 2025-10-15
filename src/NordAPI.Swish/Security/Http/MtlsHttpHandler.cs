using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace NordAPI.Swish.Security.Http;

/// <summary>
/// HttpClientHandler that attaches a client certificate (mTLS).
/// NOTE: Dev mode (skip server certificate validation) is allowed only in DEBUG.
/// </summary>
public sealed class MtlsHttpHandler : HttpClientHandler
{
    public MtlsHttpHandler(X509Certificate2 certificate, bool allowInvalidChainForDev = false)
    {
        if (certificate is null) throw new ArgumentNullException(nameof(certificate));

#if !DEBUG
        // Extra safety: prohibit enabling dev mode in Release builds
        if (allowInvalidChainForDev)
            throw new InvalidOperationException("AllowInvalidChainForDev is not allowed in Release builds.");
#endif

        ClientCertificates.Add(certificate);

        // Only in local/dev: accept any server certificate (e.g. self-signed/stubs)
        if (allowInvalidChainForDev)
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
    }
}
