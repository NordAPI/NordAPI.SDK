using System.Globalization;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;
using NordAPI.Swish.Webhooks;
// mTLS/HttpClient related (keep these when we wire the SDK in next PRs)
using System.Security.Cryptography.X509Certificates;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------
// Optional named client transport (env-toggle):
// If SWISH_USE_NAMED_CLIENT=1, register a named HttpClient "Swish"
// (and alias "NordAPI.Swish.Http") that uses env-driven mTLS when
// cert variables are present.
// -------------------------------------------------------------
var useNamed = string.Equals(
    Environment.GetEnvironmentVariable("SWISH_USE_NAMED_CLIENT"),
    "1", StringComparison.Ordinal);

if (useNamed)
{
    // Registers named clients "Swish" and "NordAPI.Swish.Http"
    // with Polly retry outermost and mTLS primary when available.
    builder.Services.AddSwishMtlsTransport();
}

// -------------------------------------------------------------
// Swish SDK client (sample defaults) — environment-driven BaseAddress
// Resolution order:
//  1) SWISH_BASE_URL (absolute override)
//  2) SWISH_BASE_URL_TEST / SWISH_BASE_URL_PROD when SWISH_ENV=TEST/PROD
//  3) fallback https://example.invalid
// -------------------------------------------------------------
var env = Environment.GetEnvironmentVariable("SWISH_ENV") ?? ""; // TEST | PROD (optional)
var baseUrl =
    Environment.GetEnvironmentVariable("SWISH_BASE_URL") // absolute override if set
    ?? (string.Equals(env, "TEST", StringComparison.OrdinalIgnoreCase)
        ? Environment.GetEnvironmentVariable("SWISH_BASE_URL_TEST")
        : string.Equals(env, "PROD", StringComparison.OrdinalIgnoreCase)
            ? Environment.GetEnvironmentVariable("SWISH_BASE_URL_PROD")
            : null)
    ?? "https://example.invalid"; // fallback

// Small startup log (helps visibility)
Console.WriteLine($"[Swish] Environment: '{env?.ToUpperInvariant()}' | BaseAddress: {baseUrl}");

builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(baseUrl);
    opts.ApiKey = Environment.GetEnvironmentVariable("SWISH_API_KEY") ?? "dev-key";
    opts.Secret = Environment.GetEnvironmentVariable("SWISH_SECRET") ?? "dev-secret";
    // No enforced mTLS here; with SWISH_USE_NAMED_CLIENT=1 + cert envs,
    // the named client path will use mTLS primary transparently.
});

// -------------------------------------------------------------
// Nonce store (replay protection):
// Use Redis if REDIS_URL or SWISH_REDIS_CONN is set; otherwise InMemory.
// -------------------------------------------------------------
var redisConn =
    Environment.GetEnvironmentVariable("REDIS_URL")
    ?? Environment.GetEnvironmentVariable("SWISH_REDIS_CONN");

if (!string.IsNullOrWhiteSpace(redisConn))
{
    // Prod/Test — Redis-backed nonce store
    builder.Services.AddSingleton<ISwishNonceStore>(_ =>
        new RedisNonceStore(redisConn, "swish:nonce:"));
}
else
{
    // Dev fallback — InMemory (with TTL scavenging)
    builder.Services.AddSingleton<ISwishNonceStore>(_ =>
        new InMemoryNonceStore(TimeSpan.FromMinutes(5)));
}

// -------------------------------------------------------------
// Webhook verifier — shared secret from env/config
// -------------------------------------------------------------
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var secret = Environment.GetEnvironmentVariable("SWISH_WEBHOOK_SECRET")
                 ?? cfg["SWISH_WEBHOOK_SECRET"];
    if (string.IsNullOrWhiteSpace(secret))
        throw new InvalidOperationException("Missing SWISH_WEBHOOK_SECRET.");

    var nonces = sp.GetRequiredService<ISwishNonceStore>();
    var opts = new SwishWebhookVerifierOptions
    {
        SharedSecret = secret
    };

    // Current signature: (options, nonceStore)
    return new SwishWebhookVerifier(opts, nonces);
});

var app = builder.Build();

app.MapGet("/", () =>
    "Swish sample is running. Try /health, /di-check, /ping, or POST /webhook/swish").AllowAnonymous();

app.MapGet("/health", () => "ok").AllowAnonymous();

app.MapGet("/di-check", (ISwishClient swish) =>
    swish is not null ? "ISwishClient is registered" : "not found").AllowAnonymous();

app.MapGet("/ping", () => Results.Ok("pong (mocked)")).AllowAnonymous();

app.MapPost("/webhook/swish", async (
    HttpRequest req,
    [FromServices] SwishWebhookVerifier verifier) =>
{
    var isDebug = string.Equals(
        Environment.GetEnvironmentVariable("SWISH_DEBUG"), "1", StringComparison.Ordinal);
    var allowOld = string.Equals(
        Environment.GetEnvironmentVariable("SWISH_ALLOW_OLD_TS"), "1", StringComparison.Ordinal);

    req.EnableBuffering();

    string rawBody;
    using (var reader = new StreamReader(req.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        rawBody = (await reader.ReadToEndAsync()) ?? string.Empty;
    req.Body.Position = 0;

    if (isDebug)
    {
        Console.WriteLine("[DEBUG] Incoming headers:");
        foreach (var h in req.Headers)
        {
            var values = string.Join(", ", h.Value.ToArray());
            Console.WriteLine($"  {h.Key} = {values}");
        }
    }

    var tsHeader = req.Headers["X-Swish-Timestamp"].ToString();
    if (string.IsNullOrWhiteSpace(tsHeader))
        tsHeader = req.Headers["X-Timestamp"].ToString();

    var sigHeader = req.Headers["X-Swish-Signature"].ToString();
    if (string.IsNullOrWhiteSpace(sigHeader))
        sigHeader = req.Headers["X-Signature"].ToString();

    var nonce = req.Headers["X-Swish-Nonce"].ToString();
    if (string.IsNullOrWhiteSpace(nonce))
        nonce = req.Headers["X-Nonce"].ToString();

    if (isDebug) Console.WriteLine($"[DEBUG] Raw tsHeader: '{tsHeader}'");

    if (string.IsNullOrWhiteSpace(tsHeader) ||
        string.IsNullOrWhiteSpace(sigHeader))
    {
        var payload = new { reason = "missing-headers", tsHeader, sigHeader };
        return isDebug
            ? Results.BadRequest(payload)
            : Results.BadRequest("Missing X-Swish-Timestamp or X-Signature");
    }

    if (!TryParseTimestamp(tsHeader, out var ts))
    {
        var payload = new { reason = "bad-timestamp", tsHeader };
        return isDebug
            ? Results.BadRequest(payload)
            : Results.BadRequest("Invalid X-Swish-Timestamp");
    }

    var now = DateTimeOffset.UtcNow;
    var skewSeconds = Math.Abs((now - ts).TotalSeconds);
    if (!allowOld && skewSeconds > TimeSpan.FromMinutes(5).TotalSeconds)
    {
        var payload = new
        {
            reason = "timestamp-skew",
            now = now.ToUnixTimeSeconds(),
            ts = ts.ToUnixTimeSeconds(),
            deltaSeconds = (int)(now - ts).TotalSeconds
        };
        return isDebug
            ? Results.Json(payload, statusCode: 401)
            : Results.Unauthorized();
    }

    var canonical = $"{tsHeader}\n{nonce}\n{rawBody}";
    if (isDebug)
    {
        Console.WriteLine("[DEBUG] Server-canonical:");
        Console.WriteLine(canonical);
    }

    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["X-Swish-Timestamp"] = tsHeader,
        ["X-Swish-Signature"] = sigHeader,
        ["X-Swish-Nonce"] = nonce
    };

    var result = verifier.Verify(rawBody, headers, DateTimeOffset.UtcNow);
    if (!result.Success)
    {
        var payload = new { reason = result.Reason ?? "sig-or-replay-failed" };
        return isDebug
            ? Results.Json(payload, statusCode: 401)
            : Results.Unauthorized();
    }

    return Results.Ok(new { received = true });
}).AllowAnonymous();

app.Run();

static bool TryParseTimestamp(string tsHeader, out DateTimeOffset ts)
{
    if (long.TryParse(tsHeader, out var num))
    {
        if (tsHeader.Length >= 13)
        {
            ts = DateTimeOffset.FromUnixTimeMilliseconds(num).ToUniversalTime();
            return true;
        }
        ts = DateTimeOffset.FromUnixTimeSeconds(num).ToUniversalTime();
        return true;
    }

    if (DateTimeOffset.TryParse(
            tsHeader,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed))
    {
        ts = parsed.ToUniversalTime();
        return true;
    }

    ts = default;
    return false;
}

public partial class Program { }
