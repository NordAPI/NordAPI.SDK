using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using NordAPI.Swish.Errors;

namespace NordAPI.Swish.Security.Http;

/// <summary>
/// Factory for creating the primary HTTP handler used by the Swish client.
/// </summary>
public static class SwishMtlsHandlerFactory
{
    /// <summary>
    /// Creates a configured <see cref="HttpMessageHandler"/> for Swish HTTP calls.
    /// If a client certificate can be resolved, mTLS is enabled with the certificate.
    /// </summary>
    /// <exception cref="SwishConfigurationException">
    /// Thrown when <see cref="SwishOptions.RequireMtls"/> is true but no client certificate is configured.
    /// </exception>
    public static HttpMessageHandler Create(SwishOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // Primary names used across README and tests; accept legacy PASS as fallback.
        var pfxPath = Environment.GetEnvironmentVariable("SWISH_PFX_PATH");
        var pfxPassword = Environment.GetEnvironmentVariable("SWISH_PFX_PASSWORD")
            ?? Environment.GetEnvironmentVariable("SWISH_PFX_PASS"); // legacy fallback

        var handler = new SocketsHttpHandler();

#if DEBUG
#pragma warning disable S4830 // DEV ONLY: relaxed server validation in Debug; NEVER in Release.
        // Dev convenience ONLY in Debug: accept any server certificate chain.
        // Never ship this in Release.
        handler.SslOptions.RemoteCertificateValidationCallback =
            static (_, __, ___, ____) => true;
#pragma warning restore S4830
#endif

        var hasPfx = !string.IsNullOrWhiteSpace(pfxPath) && !string.IsNullOrWhiteSpace(pfxPassword);

        // Fail-closed by default: if mTLS is required but no certificate is configured, throw a clear config error.
        if (!hasPfx)
        {
            if (options.RequireMtls)
            {
                throw new SwishConfigurationException(
                    "mTLS is required but no client certificate was configured. " +
                    "Configure SWISH_PFX_PATH and SWISH_PFX_PASSWORD.");
            }

            // Explicit opt-out: return a handler without a client certificate.
            return handler;
        }

        // Load PFX from disk. Do not commit certificates to source control.
        // EphemeralKeySet avoids persisting private keys on disk; MachineKeySet helps on CI agents.
        var clientCert = new X509Certificate2(
            pfxPath!,
            pfxPassword!,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);

        handler.SslOptions.ClientCertificates ??= new X509CertificateCollection();
        handler.SslOptions.ClientCertificates.Add(clientCert);

        return handler;
    }
}

