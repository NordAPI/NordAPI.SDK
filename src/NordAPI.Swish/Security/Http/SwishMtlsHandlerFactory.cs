using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace NordAPI.Swish.Security.Http
{
    /// <summary>
    /// Factory for creating the primary HTTP handler used by the Swish client.
    /// - If both SWISH_PFX_PATH and SWISH_PFX_PASSWORD (or legacy SWISH_PFX_PASS) are set -> enable mTLS with that client certificate.
    /// - Otherwise -> return a plain SocketsHttpHandler without a client certificate.
    ///
    /// DEBUG: relaxed server-certificate validation (local/dev only).
    /// RELEASE: strict default validation (no bypass).
    /// </summary>
    public static class SwishMtlsHandlerFactory
    {
        /// <summary>
        /// Creates a configured <see cref="HttpMessageHandler"/> for Swish HTTP calls.
        /// </summary>
        /// <returns>A configured <see cref="SocketsHttpHandler"/> (with or without client certificate).</returns>
        public static HttpMessageHandler Create()
        {
            // Primary names used across README and tests; accept legacy PASS as fallback.
            var pfxPath = Environment.GetEnvironmentVariable("SWISH_PFX_PATH");
            var pfxPassword = Environment.GetEnvironmentVariable("SWISH_PFX_PASSWORD")
                               ?? Environment.GetEnvironmentVariable("SWISH_PFX_PASS"); // legacy fallback

            var handler = new SocketsHttpHandler();

#if DEBUG
            // Dev convenience ONLY in Debug: accept any server certificate chain.
            // Never ship this in Release.
            handler.SslOptions.RemoteCertificateValidationCallback = static (_, __, ___, ____) => true;
#endif

            // Enable mTLS only if both path and password are provided.
            if (!string.IsNullOrWhiteSpace(pfxPath) && !string.IsNullOrWhiteSpace(pfxPassword))
            {
                // Load PFX from disk. Do not commit certificates to source control.
                // EphemeralKeySet avoids persisting private keys on disk; MachineKeySet helps on CI agents.
                var clientCert = new X509Certificate2(
                    pfxPath,
                    pfxPassword,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);

                handler.SslOptions.ClientCertificates ??= new X509CertificateCollection();
                handler.SslOptions.ClientCertificates.Add(clientCert);
            }

            return handler;
        }
    }
}


