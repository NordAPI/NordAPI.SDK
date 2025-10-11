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
    /// Tests for Swish webhook security:
    /// - Rejects requests with excessive timestamp skew.
    /// - Enforces HTTPS in non-development environments.
    /// </summary>
    public class WebhookSecurityTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public WebhookSecurityTests(WebApplicationFactory<Program> factory)
        {
            // Test host runs in Development by default (HTTP allowed).
            _factory = factory.WithWebHostBuilder(_ => { });
        }

        /// <summary>
        /// Ensures that a request with a timestamp older than allowed skew returns 401 Unauthorized.
        /// </summary>
        [Fact]
        public async Task RejectsRequestWhenTimestampIsTooOld()
        {
            // Arrange
            var client         = _factory.CreateClient();
            var tooOld         = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
            var nonce          = Guid.NewGuid().ToString("N");
            var body           = "{\"id\":\"skew-test\",\"amount\":10}";
            var messagePayload = $"{tooOld}\n{nonce}\n{body}";
            var secret         = "dev_secret";
            var signature      = Convert.ToBase64String(
                new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret))
                    .ComputeHash(Encoding.UTF8.GetBytes(messagePayload))
            );

            var request = new HttpRequestMessage(HttpMethod.Post, "/webhook/swish")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("X-Swish-Timestamp", tooOld);
            request.Headers.TryAddWithoutValidation("X-Swish-Nonce", nonce);
            request.Headers.TryAddWithoutValidation("X-Swish-Signature", signature);

            Environment.SetEnvironmentVariable("SWISH_WEBHOOK_SECRET", secret);
            Environment.SetEnvironmentVariable("SWISH_ALLOW_OLD_TS", null);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        /// <summary>
        /// Ensures that HTTP requests are rejected with 400 BadRequest in Production (HTTPS required).
        /// </summary>
        [Fact]
        public async Task RejectsHttpWhenEnvironmentIsProduction()
        {
            // Arrange: simulate Production environment
            var prodFactory = _factory.WithWebHostBuilder(builder =>
                builder.UseSetting("environment", "Production"));
            var client = prodFactory.CreateClient();

            var now       = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var nonce     = Guid.NewGuid().ToString("N");
            var body      = "{\"id\":\"https-test\",\"amount\":10}";
            var payload   = $"{now}\n{nonce}\n{body}";
            var secret    = "dev_secret";
            var signature = Convert.ToBase64String(
                new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret))
                    .ComputeHash(Encoding.UTF8.GetBytes(payload))
            );

            var request = new HttpRequestMessage(HttpMethod.Post, "/webhook/swish")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("X-Swish-Timestamp", now);
            request.Headers.TryAddWithoutValidation("X-Swish-Nonce", nonce);
            request.Headers.TryAddWithoutValidation("X-Swish-Signature", signature);

            Environment.SetEnvironmentVariable("SWISH_WEBHOOK_SECRET", secret);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}

