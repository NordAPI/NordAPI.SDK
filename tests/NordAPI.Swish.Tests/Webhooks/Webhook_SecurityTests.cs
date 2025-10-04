using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NordAPI.Swish.Tests
{
    /// <summary>
    /// Minimal security tests for the webhook:
    /// - Rejects large timestamp skew when not explicitly allowed.
    /// - Requires HTTPS in non-Development environments.
    /// </summary>
    public class Webhook_SecurityTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public Webhook_SecurityTests(WebApplicationFactory<Program> factory)
        {
            // Default test host runs in Development (HTTP allowed).
            _factory = factory.WithWebHostBuilder(_ => { });
        }

        [Fact]
        public async Task Webhook_Rejects_Skew_Over_5_Min_When_Not_Allowed()
        {
            // Arrange: timestamp 10 minutes in the past
            var client = _factory.CreateClient();

            var tooOld = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
            var nonce = Guid.NewGuid().ToString("N");
            var body = "{\"id\":\"skew-test\",\"amount\":10}";
            var msg = $"{tooOld}\n{nonce}\n{body}";
            var secret = "dev_secret";
            var sig = Convert.ToBase64String(
                new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret))
                    .ComputeHash(Encoding.UTF8.GetBytes(msg)));

            var req = new HttpRequestMessage(HttpMethod.Post, "/webhook/swish");
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
            req.Headers.TryAddWithoutValidation("X-Swish-Timestamp", tooOld);
            req.Headers.TryAddWithoutValidation("X-Swish-Nonce", nonce);
            req.Headers.TryAddWithoutValidation("X-Swish-Signature", sig);

            Environment.SetEnvironmentVariable("SWISH_WEBHOOK_SECRET", secret);
            Environment.SetEnvironmentVariable("SWISH_ALLOW_OLD_TS", null); // not allowed

            // Act
            var res = await client.SendAsync(req);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        [Fact]
        public async Task Webhook_Requires_Https_In_Non_Development()
        {
            // Arrange: simulate Production (TestServer is HTTP-only, so endpoint must reject)
            var factory = _factory.WithWebHostBuilder(b => b.UseSetting("environment", "Production"));
            var client = factory.CreateClient();

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var nonce = Guid.NewGuid().ToString("N");
            var body = "{\"id\":\"https-test\",\"amount\":10}";
            var msg = $"{now}\n{nonce}\n{body}";
            var secret = "dev_secret";
            var sig = Convert.ToBase64String(
                new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret))
                    .ComputeHash(Encoding.UTF8.GetBytes(msg)));

            var req = new HttpRequestMessage(HttpMethod.Post, "/webhook/swish");
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
            req.Headers.TryAddWithoutValidation("X-Swish-Timestamp", now);
            req.Headers.TryAddWithoutValidation("X-Swish-Nonce", nonce);
            req.Headers.TryAddWithoutValidation("X-Swish-Signature", sig);

            Environment.SetEnvironmentVariable("SWISH_WEBHOOK_SECRET", secret);

            // Act
            var res = await client.SendAsync(req);

            // Assert: 400 because HTTPS is required outside Development
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }
    }
}
