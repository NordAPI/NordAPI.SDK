using System;
using System.Globalization;
using System.IO;
using System.Linq;                // hex-konvertering
using System.Text;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 1) Swish SDK-klient i DI (oförändrat)
builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(
        Environment.GetEnvironmentVariable("SWISH_BASE_URL")
        ?? "https://example.invalid");
    opts.ApiKey = Environment.GetEnvironmentVariable("SWISH_API_KEY")
                  ?? "dev-key";
    opts.Secret = Environment.GetEnvironmentVariable("SWISH_SECRET")
                  ?? "dev-secret";
});

var app = builder.Build();

// ===== In-memory NONCE-STORE (replay-skydd, dev) =====
// Prod ska använda Redis/DB; detta är tillräckligt för lokal utveckling.
var usedNonces = new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
bool IsReplay(string? nonce, TimeSpan ttl)
{
    if (string.IsNullOrWhiteSpace(nonce)) return false; // i prod: kräv nonce separat
    var now = DateTimeOffset.UtcNow;

    // Städa gamla (lättviktigt; O(n) på antal nonces)
    var cutoff = now - ttl;
    foreach (var kv in usedNonces)
        if (kv.Value < cutoff)
            usedNonces.TryRemove(kv.Key, out _);

    // Försök lägga till; om den redan finns = replay
    return !usedNonces.TryAdd(nonce, now);
}
// ===== slut nonce-store =====

// Bas-endpoints
app.MapGet("/", () =>
    "Swish sample is running. Try /health, /di-check, /ping, or POST /webhook/swish").AllowAnonymous();
app.MapGet("/health", () => "ok").AllowAnonymous();
app.MapGet("/di-check", (ISwishClient swish) =>
    swish is not null ? "ISwishClient is registered" : "not found").AllowAnonymous();
app.MapGet("/ping", () => Results.Ok("pong (mocked)")).AllowAnonymous();

//
// 📬 FAILSAFE WEBHOOK (utan SwishWebhookVerifier, men med replay-skydd)
//  - accepterar s/ms/ISO-8601 för timestamp
//  - canonical = "<ts>\n<nonce|empty>\n<body utan " >"
//  - signatur: accepterar Base64 ELLER hex
//  - replay-skydd via in-memory nonce-store
//
app.MapPost("/webhook/swish", async (HttpRequest req) =>
{
    bool isDebug       = string.Equals(Environment.GetEnvironmentVariable("SWISH_DEBUG"), "1");
    bool allowOldTs    = string.Equals(Environment.GetEnvironmentVariable("SWISH_ALLOW_OLD_TS"), "1");
    bool requireNonce  = string.Equals(Environment.GetEnvironmentVariable("SWISH_REQUIRE_NONCE"), "1"); // sätt "1" när vi vill kräva nonce
    string secret      = Environment.GetEnvironmentVariable("SWISH_WEBHOOK_SECRET") ?? "dev_secret";

    // TTL för nonce (sekunder), default 600 = 10 minuter
    int ttlSeconds = 600;
    int.TryParse(Environment.GetEnvironmentVariable("SWISH_NONCE_TTL_SECONDS"), out ttlSeconds);
    var nonceTtl = TimeSpan.FromSeconds(Math.Max(ttlSeconds, 1));

    // Läs rå body
    req.EnableBuffering();
    string body;
    using (var reader = new StreamReader(req.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        body = await reader.ReadToEndAsync();
    req.Body.Position = 0;

    // Hämta headers + alias
    string? tsHeader  = (req.Headers["X-Swish-Timestamp"].ToString()
                      ?? req.Headers["X-Timestamp"].ToString())?.Trim();
    string? sigHeader = (req.Headers["X-Swish-Signature"].ToString()
                      ?? req.Headers["X-Signature"].ToString())?.Trim();
    string nonce      = req.Headers["X-Swish-Nonce"].ToString();
    if (string.IsNullOrWhiteSpace(nonce)) nonce = req.Headers["X-Nonce"].ToString();
    string finalNonce = nonce ?? "";

    if (isDebug)
    {
        Console.WriteLine("[DEBUG] Inkommande headers:");
        foreach (var h in req.Headers)
            Console.WriteLine($"  {h.Key} = {string.Join(", ", h.Value)}");
        Console.WriteLine($"[DEBUG] Raw tsHeader: '{tsHeader}'");
    }

    if (string.IsNullOrWhiteSpace(tsHeader) || string.IsNullOrWhiteSpace(sigHeader))
        return Results.Json(new { reason = "missing-headers", tsHeader, sigHeader }, statusCode: 400);

    // Timestamp: s / ms / ISO-8601
    if (!TryParseTs(tsHeader!, out var ts))
        return Results.Json(new { reason = "bad-timestamp", tsHeader }, statusCode: 400);

    // Tidsfönster (±5 min) – kan stängas av i dev
    var now  = DateTimeOffset.UtcNow;
    var skew = (now - ts).Duration();
    if (!allowOldTs && skew > TimeSpan.FromMinutes(5))
        return Results.Json(new { reason = "ogiltig timestamp", skew_seconds = (int)skew.TotalSeconds }, statusCode: 401);

    // === Replay-regler ===
    if (requireNonce && string.IsNullOrWhiteSpace(finalNonce))
        return Results.Json(new { reason = "missing-nonce" }, statusCode: 400);

    if (!string.IsNullOrWhiteSpace(finalNonce) && IsReplay(finalNonce, nonceTtl))
        return Results.Json(new { reason = "replay-detected" }, statusCode: 401);
    // =====================

    // Canonical body = body utan alla "
    string canonicalBody = body.Replace("\"", "");

    // Canonical meddelande: ts \n nonce|empty \n canonicalBody
    var canonical = string.Join("\n", new[] { tsHeader, finalNonce ?? "", canonicalBody });

    if (isDebug)
    {
        Console.WriteLine("[DEBUG] Server-canonical:");
        Console.WriteLine(canonical);
    }

    // HMAC-SHA256(secret, canonical)
    var key = Encoding.UTF8.GetBytes(secret);
    using var hmac = new System.Security.Cryptography.HMACSHA256(key);
    var mac = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));

    // Jämför både Base64 och hex (case-insensitive för hex)
    string expectedB64 = Convert.ToBase64String(mac);
    string expectedHex = string.Concat(mac.Select(b => b.ToString("x2")));

    bool match = sigHeader!.Equals(expectedB64, StringComparison.Ordinal)
              || sigHeader.Equals(expectedHex, StringComparison.OrdinalIgnoreCase);

    if (!match)
        return Results.Json(new { reason = "ogiltig signatur", expect_base64 = expectedB64, expect_hex = expectedHex }, statusCode: 401);

    return Results.Ok(new { ok = true });
}).AllowAnonymous();

app.Run();

/// <summary>
/// Accepterar UNIX sekunder, UNIX millisekunder samt ISO-8601 (UTC).
/// </summary>
static bool TryParseTs(string tsHeader, out DateTimeOffset ts)
{
    if (long.TryParse(tsHeader, out var num))
    {
        if (tsHeader.Length >= 13) { ts = DateTimeOffset.FromUnixTimeMilliseconds(num); return true; }
        ts = DateTimeOffset.FromUnixTimeSeconds(num);
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
