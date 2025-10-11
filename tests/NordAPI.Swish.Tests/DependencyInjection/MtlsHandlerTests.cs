using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using NordAPI.Swish.Security.Http;
using Xunit;

/// <summary>
/// Unit tests for SwishMtlsHandlerFactory.
/// Ensures correct handler creation based on environment variables.
/// </summary>
public class MtlsHandlerTests
{
    /// <summary>
    /// Verifies that the factory returns a SocketsHttpHandler without client certificates
    /// when the SWISH_PFX_PATH / SWISH_PFX_PASSWORD environment variables are not set.
    /// </summary>
    [Fact]
    public void Create_Returns_SocketsHttpHandler_Without_ClientCert_When_No_Env()
    {
        var oldPath = Environment.GetEnvironmentVariable("SWISH_PFX_PATH");
        var oldPass = Environment.GetEnvironmentVariable("SWISH_PFX_PASSWORD");
        Environment.SetEnvironmentVariable("SWISH_PFX_PATH", null);
        Environment.SetEnvironmentVariable("SWISH_PFX_PASSWORD", null);

        try
        {
            var handler = SwishMtlsHandlerFactory.Create();
            handler.Should().NotBeNull();
            handler.Should().BeOfType<SocketsHttpHandler>();
            var sockets = (SocketsHttpHandler)handler;

            (sockets.SslOptions.ClientCertificates is null || sockets.SslOptions.ClientCertificates.Count == 0)
                .Should().BeTrue("no env -> no client certificate");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SWISH_PFX_PATH", oldPath);
            Environment.SetEnvironmentVariable("SWISH_PFX_PASSWORD", oldPass);
        }
    }

    /// <summary>
    /// Deterministic test for loading a client certificate:
    /// 1) programmatically generate a self-signed certificate,
    /// 2) export to a temporary PFX with password,
    /// 3) set SWISH_PFX_PATH and SWISH_PFX_PASSWORD,
    /// 4) assert that the factory loads the client certificate,
    /// 5) cleanup.
    /// This makes the behavior reproducible in CI without external dependencies.
    /// </summary>
    [Fact]
    public void Create_Loads_ClientCert_When_Temporary_Pfx_Is_Provided()
    {
        var oldPath = Environment.GetEnvironmentVariable("SWISH_PFX_PATH");
        var oldPass = Environment.GetEnvironmentVariable("SWISH_PFX_PASSWORD");

        // Create a temporary self-signed cert and export to PFX
        var tempPfx = Path.Combine(Path.GetTempPath(), $"swish-test-{Guid.NewGuid():N}.pfx");
        var password = Guid.NewGuid().ToString("N");

        try
        {
            using (var rsa = RSA.Create(2048))
            {
                var req = new CertificateRequest(
                    "CN=swish-test",
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                // Minimal extensions for compatibility
                req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
                req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

                using (var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1)))
                {
                    var pfxBytes = cert.Export(X509ContentType.Pfx, password);
                    File.WriteAllBytes(tempPfx, pfxBytes);
                }
            }

            Environment.SetEnvironmentVariable("SWISH_PFX_PATH", tempPfx);
            Environment.SetEnvironmentVariable("SWISH_PFX_PASSWORD", password);

            var handler = SwishMtlsHandlerFactory.Create();
            handler.Should().BeOfType<SocketsHttpHandler>();
            var sockets = (SocketsHttpHandler)handler;

            sockets.SslOptions.ClientCertificates.Should().NotBeNull();
            sockets.SslOptions.ClientCertificates.Count.Should().BeGreaterThan(0, "temporary PFX should be loaded as client certificate");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SWISH_PFX_PATH", oldPath);
            Environment.SetEnvironmentVariable("SWISH_PFX_PASSWORD", oldPass);
            try { if (File.Exists(tempPfx)) File.Delete(tempPfx); } catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>
    /// A lightweight guard test that documents existing behavior when CI or local environment
    /// already provides SWISH_PFX_PATH / SWISH_PFX_PASSWORD. It does not fail CI if env is absent.
    /// </summary>
    [Fact]
    public void Create_If_Env_Present_Loads_Cert_Else_Skips()
    {
        var path = Environment.GetEnvironmentVariable("SWISH_PFX_PATH");
        var password = Environment.GetEnvironmentVariable("SWISH_PFX_PASSWORD");

        var handler = SwishMtlsHandlerFactory.Create();
        handler.Should().BeOfType<SocketsHttpHandler>();
        var sockets = (SocketsHttpHandler)handler;

        if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(password))
        {
            sockets.SslOptions.ClientCertificates.Should().NotBeNull();
            sockets.SslOptions.ClientCertificates.Count.Should().BeGreaterThan(0, "when both env vars are present, the client certificate should be loaded");
        }
        else
        {
            (sockets.SslOptions.ClientCertificates is null || sockets.SslOptions.ClientCertificates.Count == 0)
                .Should().BeTrue("no env -> no client certificate");
        }
    }
}
