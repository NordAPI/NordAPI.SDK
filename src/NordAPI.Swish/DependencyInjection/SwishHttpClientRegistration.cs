using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using NordAPI.Swish.Security.Http;

namespace NordAPI.Swish.DependencyInjection
{
    /// <summary>
    /// Opt-in HttpClientFactory-registrering:
    /// - Named client "Swish".
    /// - Om env innehåller ett PFX (PATH eller BASE64) + PASSWORD/PASS → använd MtlsHttpHandler(cert, allowInvalid).
    /// - Annars fallback till vanlig HttpClientHandler (ingen mTLS).
    /// - I DEBUG tillåts relaxed chain (endast dev).
    /// </summary>
    public static class SwishHttpClientRegistration
    {
        public static IHttpClientBuilder AddSwishHttpClient(this IServiceCollection services)
        {
            return services.AddHttpClient("Swish")
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    var cert = TryLoadCertificateFromEnv();
#if DEBUG
                    var allowInvalid = true;  // dev-only
#else
                    var allowInvalid = false; // strict in Release
#endif
                    if (cert is null)
                        return new HttpClientHandler();

                    return new MtlsHttpHandler(cert, allowInvalid);
                });
        }

        private static X509Certificate2? TryLoadCertificateFromEnv()
        {
            var pfxPath   = Environment.GetEnvironmentVariable("SWISH_PFX_PATH");
            var pfxBase64 = Environment.GetEnvironmentVariable("SWISH_PFX_BASE64");
            var pfxPass   = Environment.GetEnvironmentVariable("SWISH_PFX_PASSWORD")
                            ?? Environment.GetEnvironmentVariable("SWISH_PFX_PASS");

            if (string.IsNullOrWhiteSpace(pfxPass))
                return null;

            try
            {
                if (!string.IsNullOrWhiteSpace(pfxPath) && File.Exists(pfxPath))
                {
                    var raw = File.ReadAllBytes(pfxPath);
                    return new X509Certificate2(raw, pfxPass, X509KeyStorageFlags.EphemeralKeySet);
                }
                if (!string.IsNullOrWhiteSpace(pfxBase64))
                {
                    var raw = Convert.FromBase64String(pfxBase64);
                    return new X509Certificate2(raw, pfxPass, X509KeyStorageFlags.EphemeralKeySet);
                }
            }
            catch
            {
                // Tyst i dev; vi loggar i SDK när vi kopplar på logging.
            }
            return null;
        }
    }
}
