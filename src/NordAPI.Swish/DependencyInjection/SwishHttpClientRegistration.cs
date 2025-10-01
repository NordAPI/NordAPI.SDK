using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NordAPI.Swish.Security.Http;
using System.Security.Cryptography; // <-- behövs för CryptographicException


namespace NordAPI.Swish.DependencyInjection
{
    /// <summary>
    /// Opt-in HttpClientFactory-registrering:
    /// - Named client "Swish".
    /// - Om env innehåller PFX (PATH eller BASE64) + PASSWORD/PASS → använd MtlsHttpHandler(cert, allowInvalid).
    /// - Annars fallback till vanlig HttpClientHandler (ingen mTLS).
    /// - I DEBUG tillåts relaxed chain (endast dev).
    /// </summary>
    public static class SwishHttpClientRegistration
    {
        public static IHttpClientBuilder AddSwishHttpClient(this IServiceCollection services)
        {
            return services.AddHttpClient("Swish")
                .ConfigurePrimaryHttpMessageHandler(sp =>
                {
#if DEBUG
                    var allowInvalid = true;   // dev-only
#else
                    var allowInvalid = false;  // strict in Release
#endif
                    var cert = TryLoadCertificateFromEnv(sp, allowInvalid);
                    return cert is null
                        ? new HttpClientHandler()
                        : new MtlsHttpHandler(cert, allowInvalid);
                });
        }

        private static X509Certificate2? TryLoadCertificateFromEnv(IServiceProvider sp, bool allowInvalid)
        {
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Swish.mTLS");

            try
            {
                var pfxPath   = Environment.GetEnvironmentVariable("SWISH_PFX_PATH");
                var pfxBase64 = Environment.GetEnvironmentVariable("SWISH_PFX_BASE64");
                var pfxPass   = Environment.GetEnvironmentVariable("SWISH_PFX_PASSWORD")
                                ?? Environment.GetEnvironmentVariable("SWISH_PFX_PASS");

                // Inget cert angivet
                if (string.IsNullOrWhiteSpace(pfxPath) && string.IsNullOrWhiteSpace(pfxBase64))
                {
                    logger?.LogInformation("No Swish client certificate configured (SWISH_PFX_PATH or SWISH_PFX_BASE64 missing).");
                    return null;
                }

                // Saknat lösenord
                if (string.IsNullOrWhiteSpace(pfxPass))
                {
                    logger?.LogWarning("Swish client certificate password missing (SWISH_PFX_PASSWORD or SWISH_PFX_PASS).");
                    return null;
                }

                X509Certificate2 cert;

                if (!string.IsNullOrWhiteSpace(pfxPath))
                {
                    if (!File.Exists(pfxPath))
                    {
                        logger?.LogError("SWISH_PFX_PATH points to a non-existent file: {Path}", pfxPath);
                        return null;
                    }

                    var raw = File.ReadAllBytes(pfxPath);
                    cert = new X509Certificate2(raw, pfxPass, X509KeyStorageFlags.EphemeralKeySet);
                    logger?.LogInformation("Loaded Swish client certificate from path {Path}, subject {Subject}, expires {NotAfter:u}.",
                        pfxPath, cert.Subject, cert.NotAfter);
                }
                else
                {
                    var raw = Convert.FromBase64String(pfxBase64!);
                    cert = new X509Certificate2(raw, pfxPass, X509KeyStorageFlags.EphemeralKeySet);
                    logger?.LogInformation("Loaded Swish client certificate from Base64, subject {Subject}, expires {NotAfter:u}.",
                        cert.Subject, cert.NotAfter);
                }

#if DEBUG
                if (allowInvalid)
                    logger?.LogWarning("Allowing invalid certificate chain (DEBUG mode only).");
#endif
                return cert;
            }
            catch (FormatException fe)
            {
                logger?.LogError(fe, "Failed to load Swish client certificate: invalid Base64 content in SWISH_PFX_BASE64.");
                return null;
            }
            catch (CryptographicException ce)
            {
                logger?.LogError(ce, "Failed to load Swish client certificate: cryptographic error (possibly wrong password or corrupt PFX).");
                return null;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to load Swish client certificate from env.");
                return null;
            }
        }
    }
}

