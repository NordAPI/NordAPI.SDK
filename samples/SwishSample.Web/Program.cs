// samples/SwishSample.Web/Program.cs

using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;
using NordAPI.Swish.Webhooks;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------
// Swish SDK-klient (ENV-styrd bas-URL + HMAC). mTLS sätts i SDK:t.
// -------------------------------------------------------------
var envName = Environment.GetEnvironmentVariable("SWISH_ENV") ?? "";
var baseUrl =
    Environment.GetEnvironmentVariable("SWISH_BASE_URL") ??
    (string.Equals(envName, "TEST", StringComparison.OrdinalIgnoreCase)
        ? Environment.GetEnvironmentVariable("SWISH_BASE_URL_TEST")
        : string.Equals(envName, "PROD", StringComparison.OrdinalIgnoreCase)
            ? Environment.GetEnvironmentVariable("SWISH_BASE_URL_PROD")
            : null) ??
    "https://example.invalid";

Console.WriteLine($"[Swish] Env='{envName?.ToUpperInvariant()}', BaseAddress={baseUrl}");

builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(baseUrl);
    opts.ApiKey = Environment.GetEnvironmentVariable("SWISH_API_KEY") ?? "dev-key";
    opts.Secret = Environment.GetEnvironmentVariable("SWISH_SECRET") ?? "dev-secret";

// -------------------------------------------------------------
// Webhook-verifiering + nonce-store via våra extensions
//   - Hemlighet från secrets/env: SWISH_WEBHOOK_SECRET
//   - InMemory som default; Redis om SWISH_REDIS/SWISH_REDIS_CONN/REDIS_URL finns
// -------------------------------------------------------------
var wh = builder.Services.AddSwishWebhookVerification(cfg =>
{
    var secret =
        Environment.GetEnvironmentVariable("SWISH_WEBHOOK_SECRET")
        ?? builder.Configuration["SWISH_WEBHOOK_SECRET"];

    if (string.IsNullOrWhiteSpace(secret))
        throw new InvalidOperationException("Missing SWISH_WEBHOOK_SECRET.");

    cfg.SharedSecret = secret;
});

// Registrera nonce store direkt på IServiceCollection (samma funktionalitet)
var redisConn =
    Environment.GetEnvironmentVariable("SWISH_REDIS")
    ?? Environment.GetEnvironmentVariable("REDIS_URL")
    ?? Environment.GetEnvironmentVariable("SWISH_REDIS_CONN");

wh.Services.AddSingleton<ISwishNonceStore>(sp =>
    string.IsNullOrWhiteSpace(redisConn)
        ? new InMemoryNonceStore(TimeSpan.FromMinutes(5))
        : new RedisNonceStore(redisConn, "swish:nonce:"));

    });

var app = builder.Build();

// -------------------------------------------------------------
// Små hjälpendpoints
// -------------------------------------------------------------
app.MapGet("/", () => "Swish sample is running. Try /health, /di-check, or POST /webhook/swish");
app.MapGet("/health", () => "ok");
app.MapGet("/di-check", (ISwishClient swish) => swish is not null ? "ISwishClient is registered" : "not found");

// -------------------------------------------------------------
// Webhook-endpoint (HTTPS-krav i prod, signatur + timestamp + nonce)
// -------------------------------------------------------------
app.MapPost("/webhook/swish", async (
    HttpRequest req,
    [FromServices] SwishWebhookVerifier verifier,
    [FromServices] IWebHostEnvironment env,
    [FromServices] ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("Swish.Webhook");

    // 1) Kräver HTTPS utanför Development
    if (!env.IsDevelopment() && !req.IsHttps)
        return Results.BadRequest(new { reason = "https-required" });

    // 2) Läs rå body
    req.EnableBuffering();
    string rawBody;
    using (var reader = new StreamReader(req.Body, Encoding.UTF8, false, leaveOpen: true))
        rawBody = (await reader.ReadToEndAsync()) ?? string.Empty;
    req.Body.Position = 0;

    // 3) Plocka headers (alias hanteras i verifiern)
    var tsHeader  = ValueOr(req.Headers["X-Swish-Timestamp"], req.Headers["X-Timestamp"]);
    var sigHeader = ValueOr(req.Headers["X-Swish-Signature"], req.Headers["X-Signature"]);
    var nonce     = ValueOr(req.Headers["X-Swish-Nonce"],      req.Headers["X-Nonce"]);

    if (string.IsNullOrWhiteSpace(tsHeader) || string.IsNullOrWhiteSpace(sigHeader))
        return Results.BadRequest(new { reason = "missing-headers" });

    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["X-Swish-Timestamp"] = tsHeader,
        ["X-Swish-Signature"] = sigHeader,
        ["X-Swish-Nonce"]     = nonce ?? string.Empty
    };

    // 4) Verifiera
    var now = DateTimeOffset.UtcNow;
    var result = verifier.Verify(rawBody, headers, now);

    if (!result.Success)
    {
        log.LogWarning("Webhook verification failed. Reason='{Reason}', Nonce='{Nonce}'",
            result.Reason ?? "sig-or-replay-failed", nonce ?? "(none)");
        return Results.Json(new { reason = result.Reason ?? "sig-or-replay-failed" }, statusCode: 401);
    }

    log.LogInformation("Webhook accepted. Nonce='{Nonce}'", nonce ?? "(none)");
    return Results.Ok(new { received = true });
});

app.Run();

// -------------------------------------------------------------
// Helpers
// -------------------------------------------------------------
static string ValueOr(StringValues a, StringValues b)
    => string.IsNullOrWhiteSpace(a) ? b.ToString() : a.ToString();

// Exponera Program för tester
public partial class Program { }



