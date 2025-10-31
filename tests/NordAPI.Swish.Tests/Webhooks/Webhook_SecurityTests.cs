using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NordAPI.Swish.Tests;

/// <summary>
/// Security-related minimal E2E tests for the sample webhook.
/// Each test sets SWISH_WEBHOOK_SECRET to ensure startup succeeds locally and on CI.
/// </summary>
public class WebhookSecurityTests
{
    [Fact]
    public async Task RejectsHttpWhenEnvironmentIsProduction()
    {
        const string secret = "dev_secret";
        Environment.SetEnvironmentVariable("SWISH_WEBHOOK_SECRET", secret);
        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();

            // Hitting HTTP should be rejected in non-Development (the endpoint enforces HTTPS in prod)
            var resp = await client.PostAsync("http://localhost/webhook/swish", new StringContent("{}"));
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SWISH_WEBHOOK_SECRET", null);
        }
    }

    [Fact]
    public async Task RejectsRequestWhenTimestampIsTooOld()
    {
        const string secret = "dev_secret";
        Environment.SetEnvironmentVariable("SWISH_WEBHOOK_SECRET", secret);
        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();

            // Too old timestamp → verifier should reject
            var req = new HttpRequestMessage(HttpMethod.Post, "/webhook/swish");
            req.Headers.Add("X-Swish-Timestamp", "0"); // epoch too old
            req.Headers.Add("X-Swish-Signature", "invalid");
            req.Headers.Add("X-Swish-Nonce", "n/a");
            req.Content = new StringContent("{}");

            var resp = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SWISH_WEBHOOK_SECRET", null);
        }
    }
}


