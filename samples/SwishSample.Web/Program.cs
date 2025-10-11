using System.Globalization;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;
using NordAPI.Swish.Webhooks;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------
// Swish SDK client (ENV-driven base address + HMAC), mTLS via env/cert
// -------------------------------------------------------------
var envName = Environment.GetEnvironmentVariable("SWISH_ENV") ?? ""; // TEST | PROD (optional)
var baseUrl =
    Environment.GetEnvironmentVariable("SWISH_BASE_URL") ??
    (string.Equals(envName, "TEST", StringComparison.OrdinalIgnoreCase)
        ? Environment.GetEnvironmentVariable("SWISH_BASE_URL_TEST")
        : string.Equals(envName, "PROD", StringComparison.OrdinalIgnoreCase)
            ? Environment.GetEnvironmentVariable("SWISH_BASE_URL_PROD")
            : null) ??
    "https://example.invalid";

Console.WriteLine($"[Swish] Environment: '{envName?.ToUpperInvariant()}' | BaseAddress: {baseUrl}");

builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(baseUrl);
    opts.ApiKey = Environment.GetEnvironmentVariable("SWISH_API_KEY") ?? "dev-key";
    opts.Secret = Environment.GetEnvironmentVariable("SWISH_SECRET") ?? "dev-secret";
});

// -------------------------------------------------------------
// Nonce store (replay protection):
// Use Redis if SWISH_REDIS (or alias) is set; otherwise InMemory (dev).
// -------------------------------------------------------------
var redisConn =
    Environment.GetEnvironmentVariable("SWISH_REDIS")
    ?? Environment.GetEnvironmentVariable("REDIS_URL")
    ?? Environment.GetEnvironmentVariable("SWISH_REDIS_CONN");

if (!string.IsNullOrWhiteSpace(redisConn))
{
    builder.Services.AddSingleton<ISwishNonceStore>(_ =>
        new RedisNonceStore(redisConn, "swish:nonce:"));
}
else
{
    builder.Services.AddSingleton<ISwishNonceStore>(_ =>
        new InMemoryNonceStore(TimeSpan.FromMinutes(5)));
}

// -------------------------------------------------------------
// Webhook verifier — requires SWISH_WEBHOOK_SECRET
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

    return new SwishWebhookVerifier(opts, nonces);
});

var app = builder.Build();

// Small helper endpoints
app.MapGet("/", () =>
    "Swish sample is running. Try /health, /di-check, /ping, or POST /webhook/swish").AllowAnonymous();

app.MapGet("/health", () => "ok").AllowAnonymous();

app.MapGet("/di-check", (ISwishClient swish) =>
    swish is not null ? "ISwishClient is registered" : "not found").AllowAnonymous();

app.MapGet("/ping", () => Results.Ok("pong (mocked)")).AllowAnonymous();

// -------------------------------------------------------------
// Webhook endpoint (hardened):
// - Enforce HTTPS outside Development
// - Timestamp skew ±5 min (can relax via SWISH_ALLOW_OLD_TS=1 in Development)
// - Replay protection via nonce store
// - Structured logging (signatures are never fully logged)
// -------------------------------------------------------------
app.MapPost("/webhook/swish", async (
    HttpRequest req,
    [FromServices] SwishWebhookVerifier verifier,
    [FromServices] ILoggerFactory loggerFactory,
    [FromServices] IWebHostEnvironment hostEnv) =>
{
    var log = loggerFactory.CreateLogger("Swish.Webhook");
    var isDev = hostEnv.IsDevelopment();

    // 1) Require HTTPS when not in Development
    if (!isDev && !req.IsHttps)
    {
        log.LogWarning("Webhook rejected due to non-HTTPS in non-Development environment.");
        return Results.StatusCode(400);
    }

    // 2) Optional skew relaxation only for Development
    var allowOldTsEnv = string.Equals(
        Environment.GetEnvironmentVariable("SWISH_ALLOW_OLD_TS"), "1", StringComparison.Ordinal);
    var allowOldInThisRequest = isDev && allowOldTsEnv;

    req.EnableBuffering();

    string rawBody;
    using (var reader = new StreamReader(req.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        rawBody = (await reader.ReadToEndAsync()) ?? string.Empty;
    req.Body.Position = 0;

    // Headers (with common aliases)
    var tsHeader = ValueOr(req.Headers["X-Swish-Timestamp"], req.Headers["X-Timestamp"]);
    var sigHeader = ValueOr(req.Headers["X-Swish-Signature"], req.Headers["X-Signature"]);
    var nonce = ValueOr(req.Headers["X-Swish-Nonce"], req.Headers["X-Nonce"]);

    if (string.IsNullOrWhiteSpace(tsHeader) || string.IsNullOrWhiteSpace(sigHeader))
    {
        log.LogWarning("Webhook missing required headers. Timestamp='{Ts}', HasSignature='{HasSig}'",
            tsHeader, !string.IsNullOrWhiteSpace(sigHeader));
        return Results.BadRequest(new { reason = "missing-headers" });
    }

    if (!TryParseTimestamp(tsHeader, out var ts))
    {
        log.LogWarning("Webhook bad timestamp format. Raw='{Raw}'", tsHeader);
        return Results.BadRequest(new { reason = "bad-timestamp" });
    }

    // 3) Skew check (±5 minutes by default)
    var now = DateTimeOffset.UtcNow;
    var skewSeconds = Math.Abs((now - ts).TotalSeconds);
    var maxSkew = TimeSpan.FromMinutes(5).TotalSeconds;
    if (!allowOldInThisRequest && skewSeconds > maxSkew)
    {
        log.LogWarning("Webhook timestamp skew too large. Now={Now}, Ts={Ts}, DeltaSec={Delta}",
            now.ToUnixTimeSeconds(), ts.ToUnixTimeSeconds(), (int)(now - ts).TotalSeconds);

        return Results.Json(new { reason = "timestamp-skew" }, statusCode: 401);
    }

    // 4) Verify signature + replay via nonce
    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["X-Swish-Timestamp"] = tsHeader,
        ["X-Swish-Signature"] = sigHeader,
        ["X-Swish-Nonce"] = nonce ?? string.Empty
    };

    var result = verifier.Verify(rawBody, headers, now);
    if (!result.Success)
    {
        // Mask signature in logs — never log secrets or raw signature values
        log.LogWarning("Webhook verification failed. Reason='{Reason}', Nonce='{Nonce}'",
            result.Reason ?? "sig-or-replay-failed", nonce ?? "(none)");

        return Results.Json(new { reason = result.Reason ?? "sig-or-replay-failed" }, statusCode: 401);
    }

    log.LogInformation("Webhook accepted. Nonce='{Nonce}'", nonce ?? "(none)");
    return Results.Ok(new { received = true });
}).AllowAnonymous();

app.Run();

static string ValueOr(string v1, string v2) => string.IsNullOrWhiteSpace(v1) ? v2 : v1;

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

