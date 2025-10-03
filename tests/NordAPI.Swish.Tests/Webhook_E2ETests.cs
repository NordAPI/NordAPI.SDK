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
    public class Webhook_E2ETests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public Webhook_E2ETests(WebApplicationFactory<Program> factory)
        {
            // SÃ¤tt env vars som sample-lÃ¤ses via Environment.GetEnvironmentVariable
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            Environment.SetEnvironmentVariable("SWISH_WEBHOOK_SECRET", "dev_secret");
            Environment.SetEnvironmentVariable("SWISH_DEBUG", "1");
            Environment.SetEnvironmentVariable("SWISH_ALLOW_OLD_TS", "1");
            Environment.SetEnvironmentVariable("SWISH_REQUIRE_NONCE", "0");

            _factory = factory.WithWebHostBuilder(_ => { /* no-op, env rÃ¤cker */ });
        }

        [Fact]
        public async Task Webhook_Should_Accept_Valid_Signature_And_Reject_Replay()
        {
            using var client = _factory.CreateClient(); // In-memory TestServer client

            var secret = "dev_secret";
            var bodyJson = "{\"id\":\"e2e-1\",\"amount\":100}";
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var nonce = Guid.NewGuid().ToString("N");

            // Canonical: <ts>\n<nonce>\n<body>
            var canonical = $"{ts}\n{nonce}\n{bodyJson}";
            var sigB64 = Sign(secret, canonical);

            using var req1 = new HttpRequestMessage(HttpMethod.Post, "/webhook/swish");
            req1.Headers.Add("X-Swish-Timestamp", ts);
            req1.Headers.Add("X-Swish-Nonce", nonce);
            req1.Headers.Add("X-Swish-Signature", sigB64);
            req1.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            var res1 = await client.SendAsync(req1);
            res1.StatusCode.Should().Be(HttpStatusCode.OK, await Dump(res1));

            // Re-POST samma (ska trigga replay)
            using var req2 = new HttpRequestMessage(HttpMethod.Post, "/webhook/swish");
            req2.Headers.Add("X-Swish-Timestamp", ts);
            req2.Headers.Add("X-Swish-Nonce", nonce);
            req2.Headers.Add("X-Swish-Signature", sigB64);
            req2.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            var res2 = await client.SendAsync(req2);
            // I debug-lÃ¤ge svarar sample med 401 + JSON { reason = "...replay..." }
            res2.StatusCode.Should().Be(HttpStatusCode.Unauthorized, await Dump(res2));
            var reason = await res2.Content.ReadAsStringAsync();
            reason.ToLowerInvariant().Should().ContainAny("replay", "nonce");
        }

        private static string Sign(string secret, string canonical)
        {
            var key = Encoding.UTF8.GetBytes(secret);
            using var hmac = new HMACSHA256(key);
            var mac = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToBase64String(mac);
        }

        private static async Task<string> Dump(HttpResponseMessage res)
        {
            var body = await res.Content.ReadAsStringAsync();
            return $"STATUS: {(int)res.StatusCode} {res.StatusCode}; BODY: {body}";
        }
    }
}


