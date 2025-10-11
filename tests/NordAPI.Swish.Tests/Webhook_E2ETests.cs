using NordAPI.Swish.DependencyInjection;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NordAPI.Swish.Tests
{
    /// <summary>
    /// End-to-end tests for the Swish webhook endpoint.
    /// Verifies that valid requests are accepted and replayed requests are rejected.
    /// </summary>
    public class Webhook_E2ETests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        /// <summary>
        /// Sets required environment variables and configures the test server.
        /// </summary>
        public Webhook_E2ETests(WebApplicationFactory<Program> factory)
        {
            // Configure environment for the sample application
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            Environment.SetEnvironmentVariable("SWISH_WEBHOOK_SECRET",    "dev_secret");
            Environment.SetEnvironmentVariable("SWISH_DEBUG",             "1");
            Environment.SetEnvironmentVariable("SWISH_ALLOW_OLD_TS",      "1");
            Environment.SetEnvironmentVariable("SWISH_REQUIRE_NONCE",     "0");

            // Initialize the in-memory test server with these settings
            _factory = factory.WithWebHostBuilder(builder => { /* no additional setup needed */ });
        }

        /// <summary>
        /// Sends two requests using the same nonce:
        /// - First request has a valid signature and should return 200 OK.
        /// - Second request is a replay and should return 401 Unauthorized.
        /// </summary>
        [Fact]
        public async Task Webhook_Should_Accept_Valid_Signature_And_Reject_Replay()
        {
            using var client = _factory.CreateClient(); // In-memory HTTP client

            // Arrange: prepare payload, timestamp, and nonce
            var secret   = "dev_secret";
            var bodyJson = "{\"id\":\"e2e-1\",\"amount\":100}";
            var ts       = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var nonce    = Guid.NewGuid().ToString("N");
            var canonical = $"{ts}\n{nonce}\n{bodyJson}";
            var signature = Sign(secret, canonical);

            // First request: valid signature
            using var req1 = new HttpRequestMessage(HttpMethod.Post, "/webhook/swish");
            req1.Headers.Add("X-Swish-Timestamp", ts);
            req1.Headers.Add("X-Swish-Nonce",     nonce);
            req1.Headers.Add("X-Swish-Signature", signature);
            req1.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            var res1 = await client.SendAsync(req1);
            res1.StatusCode
                .Should().Be(HttpStatusCode.OK, await Dump(res1));

            // Second request: replay with the same nonce
            using var req2 = new HttpRequestMessage(HttpMethod.Post, "/webhook/swish");
            req2.Headers.Add("X-Swish-Timestamp", ts);
            req2.Headers.Add("X-Swish-Nonce",     nonce);
            req2.Headers.Add("X-Swish-Signature", signature);
            req2.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            var res2 = await client.SendAsync(req2);
            res2.StatusCode
                .Should().Be(HttpStatusCode.Unauthorized, await Dump(res2));

            var reason = await res2.Content.ReadAsStringAsync();
            reason
                .ToLowerInvariant()
                .Should().ContainAny("replay", "nonce");
        }

        /// <summary>
        /// Computes a Base64-encoded HMAC-SHA256 signature over the canonical string.
        /// </summary>
        private static string Sign(string secret, string canonical)
        {
            var key = Encoding.UTF8.GetBytes(secret);
            using var hmac = new HMACSHA256(key);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Reads and formats the response content for diagnostic assertions.
        /// </summary>
        private static async Task<string> Dump(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            return $"STATUS: {(int)response.StatusCode} {response.StatusCode}; BODY: {body}";
        }
    }
}



