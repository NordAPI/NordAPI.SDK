using System;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace NordAPI.Swish.Security.Http
{
    /// <summary>
    /// Creates the primary HttpMessageHandler used for Swish calls.
    /// If SWISH_PFX_PATH + SWISH_PFX_PASS are set, attaches the client certificate (mTLS).
    /// Otherwise returns a plain SocketsHttpHandler (no mTLS).
    /// In DEBUG only, a permissive server validation is used. Never in Release.
    /// </summary>
    internal static class SwishMtlsHandlerFactory
    {
        public static HttpMessageHandler Create()
        {
            var pfxPath = Environment.GetEnvironmentVariable("SWISH_PFX_PATH");
            var pfxPass = Environment.GetEnvironmentVariable("SWISH_PFX_PASS");

            var handler = new SocketsHttpHandler();

            if (!string.IsNullOrWhiteSpace(pfxPath) &&
                !string.IsNullOrWhiteSpace(pfxPass) &&
                File.Exists(pfxPath))
            {
                var cert = new X509Certificate2(pfxPath, pfxPass, X509KeyStorageFlags.EphemeralKeySet);
                handler.SslOptions.ClientCertificates = new X509CertificateCollection { cert };
            }

#if DEBUG
            handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
#endif
            return handler;
        }
    }
}
