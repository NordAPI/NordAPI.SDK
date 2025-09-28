using StackExchange.Redis;
using SwishSample.Web.Security;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;
using NordAPI.Swish.Security.Webhooks;

var builder = WebApplication.CreateBuilder(args);

// Swish SDK-klient (mockade värden i sample)
builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(
        Environment.GetEnvironmentVariable("SWISH_BASE_URL")
        ?? "https://example.invalid");
    opts.ApiKey = Environment.GetEnvironmentVariable("SWISH_API_KEY")
                  ?? "dev-key";
    opts.Secret = Environment.GetEnvironmentVariable("SWISH_SECRET")
                  ?? "dev-secret";

#if !DEBUG
    // mTLS – ladda PFX från miljövariabler (Base64 + lösenord)
    var pfxB64 = Environment.GetEnvironmentVariable("SWISH_PFX_BASE64");
    var pfxPwd = Environment.GetEnvironmentVariable("SWISH_PFX_PASSWORD");
    if (string.IsNullOrWhiteSpace(pfxB64) || string.IsNullOrWhiteSpace(pfxPwd))
        throw new InvalidOperationException("Missing SWISH_PFX_BASE64 or SWISH_PFX_PASSWORD");

    var pfxBytes = Convert.FromBase64String(pfxB64);
    var cert = new X509Certificate2(
        pfxBytes,
        pfxPwd,
        X509KeyStorageFlags.EphemeralKeySet);

    opts.ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(cert);
        handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 
                             | System.Security.Authentication.SslProtocols.Tls13;
        handler.ClientCertificateOptions = ClientCertificateOption.Manual;
        return handler;
    });
#endif
});


// Replay-skydd (nonce-store)
builder.Services.AddSingleton<ISwishNonceStore, InMemoryNonceStore>();

// Webhook verifierare – läs hemlig nyckel från env/konfig
builder.Services.AddSingleton(sp =>
{
    var cfg    = sp.GetRequiredService<IConfiguration>();
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

// ===== [4.2] Redis (valfritt) =============================================
// Aktivera genom SWISH_REDIS=1; anslutning via SWISH_REDIS_CONN (default "localhost:6379")
var useRedis = string.Equals(Environment.GetEnvironmentVariable("SWISH_REDIS"), "1", StringComparison.Ordinal);
if (useRedis)
{
    var connStr = Environment.GetEnvironmentVariable("SWISH_REDIS_CONN") ?? "localhost:6379";
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connStr));
}
// ==========================================================================

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
    var isDebug  = string.Equals(
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
        Console.WriteLine("[DEBUG] Inkommande headers:");
        foreach (var h in req.Headers)
        {
            var values = string.Join(", ", h.Value.ToArray());
            Console.WriteLine($"  {h.Key} = {values}");
        }
    }

    var tsHeader  = req.Headers["X-Swish-Timestamp"].ToString();
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
            now    = now.ToUnixTimeSeconds(),
            ts     = ts.ToUnixTimeSeconds(),
            deltaSeconds = (int)(now - ts).TotalSeconds
        };
        return isDebug
            ? Results.Json(payload, statusCode: 401)
            : Results.Unauthorized();
    }

    // ===== [4.3] Redis-gate (persistant anti-replay) =======================
    // Kör före signaturverifiering. Aktiveras bara om IConnectionMultiplexer finns registrerad.
    IConnectionMultiplexer? redis = null;
    try { redis = req.HttpContext.RequestServices.GetService<IConnectionMultiplexer>(); } catch { /* ignore */ }

    if (redis is not null && !string.IsNullOrWhiteSpace(nonce))
    {
        // TTL = skew-fönster + liten marginal
        var ttl = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(30);
        var isNew = await RedisReplayGate.TryRegisterNonceAsync(redis, "swish:nonce", nonce, ttl);
        if (!isNew)
        {
            var payload = new { reason = "replay-detected (redis)" };
            return Results.Json(payload, statusCode: 401);
        }
    }
    // =======================================================================

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
        ["X-Swish-Nonce"]     = nonce
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

