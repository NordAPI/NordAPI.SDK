using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NordAPI.Swish.Security.Http;

namespace NordAPI.Swish.DependencyInjection
{
    /// <summary>
    /// Opt-in HttpClientFactory-registrering:
    /// - Named client "Swish".
    /// - mTLS via env (PATH/BASE64 + PASSWORD/PASS) → MtlsHttpHandler(cert, allowInvalid).
    /// - Timeout 30s och enkel retry-policy (408/429/5xx + timeout/transportfel).
    /// - DEBUG: relaxed chain; Release: strikt.
    /// </summary>
    public static class SwishHttpClientRegistration
    {
        public static IHttpClientBuilder AddSwishHttpClient(this IServiceCollection services)
        {
            return services.AddHttpClient("Swish")
                // Timeout för hela requesten
                .ConfigureHttpClient(c =>
                {
                    c.Timeout = TimeSpan.FromSeconds(30);
                })
                // Primär handler (mTLS eller vanlig)
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
                })
                // Retry-handler överst i pipen
                .AddHttpMessageHandler(sp =>
                {
                    var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Swish.Retry");
                    return new RetryHandler(logger, maxRetries: 3, baseDelayMs: 250, maxDelayMs: 2000);
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

                if (string.IsNullOrWhiteSpace(pfxPath) && string.IsNullOrWhiteSpace(pfxBase64))
                {
                    logger?.LogInformation("No Swish client certificate configured (SWISH_PFX_PATH or SWISH_PFX_BASE64 missing).");
                    return null;
                }

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
                    logger?.LogInformation(
                        "Loaded Swish client certificate from path {Path}, subject {Subject}, expires {NotAfter:u}.",
                        pfxPath, cert.Subject, cert.NotAfter);
                }
                else
                {
                    var raw = Convert.FromBase64String(pfxBase64!);
                    cert = new X509Certificate2(raw, pfxPass, X509KeyStorageFlags.EphemeralKeySet);
                    logger?.LogInformation(
                        "Loaded Swish client certificate from Base64, subject {Subject}, expires {NotAfter:u}.",
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

        /// <summary>
        /// Minimal retry-handler för transienta fel:
        /// - HTTP 408, 429, 5xx
        /// - HttpRequestException / TaskCanceledException (t.ex. timeout)
        /// Max 'maxRetries' försök, exponential backoff + jitter.
        /// </summary>
        private sealed class RetryHandler : DelegatingHandler
        {
            private readonly ILogger? _logger;
            private readonly int _maxRetries;
            private readonly int _baseDelayMs;
            private readonly int _maxDelayMs;
            private static readonly Random _rng = new();

            public RetryHandler(ILogger? logger, int maxRetries, int baseDelayMs, int maxDelayMs)
            {
                _logger = logger;
                _maxRetries = Math.Max(0, maxRetries);
                _baseDelayMs = Math.Max(1, baseDelayMs);
                _maxDelayMs = Math.Max(_baseDelayMs, maxDelayMs);
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                int attempt = 0;

                while (true)
                {
                    attempt++;

                    try
                    {
                        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                        if (!ShouldRetry(response.StatusCode) || attempt > _maxRetries)
                            return response;

                        // Viktigt: frigör svaret innan retry så anslutningen kan återanvändas
                        response.Dispose();
                    }
                    catch (HttpRequestException ex) when (attempt <= _maxRetries)
                    {
                        _logger?.LogWarning(ex, "Transient transport error on attempt {Attempt} {Method} {Uri}", attempt, request.Method, request.RequestUri);
                    }
                    catch (TaskCanceledException ex) when (attempt <= _maxRetries && !cancellationToken.IsCancellationRequested)
                    {
                        _logger?.LogWarning(ex, "Request timed out on attempt {Attempt} {Method} {Uri}", attempt, request.Method, request.RequestUri);
                    }

                    var delay = NextBackoff(attempt);
                    _logger?.LogInformation("Retrying ({Attempt}/{Max}) {Method} {Uri} after {Delay}ms", attempt, _maxRetries, request.Method, request.RequestUri, delay);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }

            private static bool ShouldRetry(HttpStatusCode statusCode)
            {
                int code = (int)statusCode;
                if (code == 408 || code == 429) return true;       // Request Timeout / Too Many Requests
                if (code >= 500 && code <= 599) return true;        // 5xx
                return false;
            }

            private int NextBackoff(int attempt)
            {
                // Exponential backoff: base * 2^(attempt-1), bounded + jitter (0-250ms)
                var exp = _baseDelayMs * (int)Math.Pow(2, Math.Max(0, attempt - 1));
                var jitter = _rng.Next(0, 250);
                return Math.Min(exp + jitter, _maxDelayMs);
            }
        }
    }
}



