using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;
using NordAPI.Swish.Webhooks;

var builder = WebApplication.CreateBuilder(args);

// SDK i DI (env vars funkar; dev-placeholders lokalt är ok)

builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(Environment.GetEnvironmentVariable("SWISH_BASE_URL") ?? "https://example.invalid");
    opts.ApiKey      = Environment.GetEnvironmentVariable("SWISH_API_KEY") ?? "dev-key";
    opts.Secret      = Environment.GetEnvironmentVariable("SWISH_SECRET") ?? "dev-secret";
});

// Replay-skydd (in-memory)
builder.Services.AddSingleton<ISwishNonceStore, InMemoryNonceStore>();

var app = builder.Build();

// Root (slipp 404 på "/")
app.MapGet("/", () => "Swish sample is running. Try /health, /di-check, /ping (mock), or POST /webhook/swish");

// Health-check
app.MapGet("/health", () => "ok");

// Verifiera att ISwishClient finns i DI
app.MapGet("/di-check", (ISwishClient swish) => swish is not null ? "ISwishClient is registered" : "not found");

// Mockad ping (undviker riktiga HTTP-anrop i dev)
app.MapGet("/ping", () => Results.Ok("pong (mocked)"));

// =====================
// 📬 Webhook (signatur + replay-skydd) — stöder Base64 *eller* Hex
// =====================
// Förväntade headers:
//   X-Swish-Timestamp : unix seconds (text -> long)
//   X-Swish-Signature : HMAC-SHA256 av canonical "<timestamp>\n<body>" i Base64 ELLER Hex
//   X-Nonce           : valfri nonce för replay-skydd
// Hemlighet läses från env/konfig: SWISH_WEBHOOK_SECRET.

app.MapPost("/webhook/swish", async (
    HttpRequest req,
    ISwishNonceStore nonces,
    [FromServices] IConfiguration cfg) =>
{
    var secret = Environment.GetEnvironmentVariable("SWISH_WEBHOOK_SECRET") ?? cfg["SWISH_WEBHOOK_SECRET"];
    if (string.IsNullOrWhiteSpace(secret))
        return Results.Problem("Missing SWISH_WEBHOOK_SECRET", statusCode: 500);

    var tsHeader  = req.Headers["X-Swish-Timestamp"].ToString();
    var sigHeader = req.Headers["X-Swish-Signature"].ToString();
    var nonce     = req.Headers["X-Nonce"].ToString();

    if (string.IsNullOrWhiteSpace(tsHeader) || string.IsNullOrWhiteSpace(sigHeader))
        return Results.BadRequest("Missing X-Swish-Timestamp or X-Swish-Signature");

    if (!long.TryParse(tsHeader, out var ts))
        return Results.BadRequest("Invalid X-Swish-Timestamp");

    // Läs body exakt som skickats (utan att ändra format/whitespace)
    string body;
    using (var reader = new StreamReader(req.Body, Encoding.UTF8))
        body = await reader.ReadToEndAsync();

    // Tidsfönster (±5 min)
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    if (Math.Abs(now - ts) > TimeSpan.FromMinutes(5).TotalSeconds)
        return Results.Unauthorized();

    // Räkna förväntad HMAC
    var canonical = $"{ts}\n{body}";
    var keyBytes  = Encoding.UTF8.GetBytes(secret);
    var data      = Encoding.UTF8.GetBytes(canonical);
    using var hmac = new HMACSHA256(keyBytes);
    var expected = hmac.ComputeHash(data);

    // Jämför Base64 eller Hex (constant-time)
    if (!TryMatchSignature(sigHeader, expected))
        return Results.Unauthorized();

    // Replay-skydd om nonce finns
    if (!string.IsNullOrWhiteSpace(nonce))
    {
        var replay = await nonces.SeenRecentlyAsync(
            nonce,
            DateTimeOffset.FromUnixTimeSeconds(ts),
            TimeSpan.FromMinutes(10));

        if (replay)
            return Results.StatusCode(409); // Conflict (replay)
    }

    // TODO: hantera payload (kö, logg, DB …)
    return Results.Ok(new { received = true });

    // ===== Hjälpare =====

    static bool TryMatchSignature(string provided, ReadOnlySpan<byte> expected)
    {
        // 1) Base64
        if (TryFromBase64(provided, out var b64) && FixedTimeEquals(b64, expected))
            return true;

        // 2) Hex (tillåt blandade case/ev. mellanslag)
        var hex = provided.Replace(" ", "").Trim();
        if (TryFromHex(hex, out var raw) && FixedTimeEquals(raw, expected))
            return true;

        return false;
    }

    static bool TryFromBase64(string s, out byte[] bytes)
    {
        try { bytes = Convert.FromBase64String(s); return true; }
        catch { bytes = Array.Empty<byte>(); return false; }
    }

    static bool TryFromHex(string hex, out byte[] bytes)
    {
        try
        {
            // .NET 6/7/8 – robust och snabb
            bytes = Convert.FromHexString(hex);
            return true;
        }
        catch
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }

    static bool FixedTimeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
});

app.Run();

