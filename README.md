# NordAPI.Swish SDK (MVP)

[![Build](https://github.com/NordAPI/NordAPI.SwishSdk/actions/workflows/ci.yml/badge.svg)](https://github.com/NordAPI/NordAPI.SwishSdk/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/badge/NuGet-Unlisted-blue)](https://www.nuget.org/packages/NordAPI.Swish)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)

> 🇸🇪 Swedish version: [README.sv.md](./src/NordAPI.Swish/README.sv.md)  
> ✅ See also: [Integration Checklist](./docs/integration-checklist.md)

A lightweight and secure .NET SDK for integrating **Swish payments and refunds** in test and development environments.  
Includes built-in support for HMAC authentication, mTLS, and rate limiting.

---

## 🚀 Features

- ✅ Create and verify Swish payments  
- 🔁 Refund support  
- 🔐 HMAC + mTLS support  
- 📉 Rate limiting  
- 🧪 ASP.NET Core integration  
- 🧰 Environment variable configuration

---

## ⚡ Quick start (ASP.NET Core)

With this SDK you get a working Swish client in just minutes:

- **HttpClientFactory** with retry and rate limiting  
- **Built-in HMAC signing**  
- **mTLS (optional)** via environment variables — strict chain in Release; relaxed only in Debug  
- **Webhook verification** with replay protection (nonce-store)

### 1) Install / reference

Install from NuGet:

```powershell
dotnet add package NordAPI.Swish
```

Or add a project reference (for local development):

```xml
<ItemGroup>
  <ProjectReference Include="..\src\NordAPI.Swish\NordAPI.Swish.csproj" />
</ItemGroup>
```

### 2) Register the client in *Program.cs*

```csharp
using NordAPI.Swish;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(
        Environment.GetEnvironmentVariable("SWISH_BASE_URL")
        ?? "https://example.invalid");

    opts.ApiKey = Environment.GetEnvironmentVariable("SWISH_API_KEY")
                  ?? throw new InvalidOperationException("Missing SWISH_API_KEY");

    opts.Secret = Environment.GetEnvironmentVariable("SWISH_SECRET")
                  ?? throw new InvalidOperationException("Missing SWISH_SECRET");
});

var app = builder.Build();

app.MapGet("/ping", async (ISwishClient swish) =>
{
    var result = await swish.PingAsync();
    return Results.Ok(result);
});

app.Run();
```

### 3) Use in your code

```csharp
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly ISwishClient _swish;

    public PaymentsController(ISwishClient swish)
    {
        _swish = swish;
    }

    [HttpPost("pay")]
    public async Task<IActionResult> Pay()
    {
        var create = new CreatePaymentRequest(100.00m, "SEK", "46701234567", "Test purchase");
        var payment = await _swish.CreatePaymentAsync(create);
        return Ok(payment);
    }
}
```

---

## 🔐 mTLS via environment variables (optional)

Enable mutual TLS with a client certificate (PFX):

- `SWISH_PFX_PATH` — path to `.pfx`  
- `SWISH_PFX_PASSWORD` — password for the certificate  

**Behavior:**
- No certificate → falls back to non-mTLS.  
- **Debug:** relaxed server certificate validation (local only).  
- **Release:** strict chain (no "allow invalid chain").  

**Example (PowerShell):**
```powershell
$env:SWISH_PFX_PATH = "C:\certs\swish-client.pfx"
$env:SWISH_PFX_PASSWORD = "secret-password"
```

> 🔒 In production, store certs and secrets in **Azure Key Vault** or similar — never in your repository.

---

## 🧪 Run & smoke test

Start the sample app (port 5000) with the webhook secret:

```powershell
$env:SWISH_WEBHOOK_SECRET = "dev_secret"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

Then, in another PowerShell window, run:

```powershell
.\scripts\smoke-webhook.ps1 -Secret dev_secret -Url http://localhost:5000/webhook/swish
```

### ✅ Expected (Success)
```json
{"received": true}
```

### ❌ Expected on replay (Error)
```json
{"reason": "replay detected (nonce seen before)"}
```

- In production: set `SWISH_REDIS` (the sample also accepts aliases `REDIS_URL` and `SWISH_REDIS_CONN`).  
  Without Redis, an in-memory store is used (recommended for local development).

---

## 🌐 Common environment variables

| Variable             | Purpose                                   | Example                     |
|----------------------|--------------------------------------------|-----------------------------|
| SWISH_BASE_URL       | Base URL for Swish API                     | https://example.invalid     |
| SWISH_API_KEY        | API key for HMAC                           | dev-key                     |
| SWISH_SECRET         | Shared secret for HMAC                     | dev-secret                  |
| SWISH_PFX_PATH       | Path to client certificate (.pfx)          | C:\certs\swish-client.pfx |
| SWISH_PFX_PASSWORD   | Password for client certificate            | ••••                        |
| SWISH_WEBHOOK_SECRET | Webhook HMAC secret                        | dev_secret                  |
| SWISH_REDIS          | Redis connection string (nonce store)      | localhost:6379              |
| SWISH_DEBUG          | Verbose logging / relaxed verification     | 1                           |
| SWISH_ALLOW_OLD_TS   | Allow older timestamps for verification    | 1 (dev only)                |

> 💡 Never hard-code secrets. Use environment variables, Secret Manager, or GitHub Actions Secrets.

---

## 🧰 Troubleshooting

- **404 / Connection refused:** Make sure your app listens on the right URL/port (`--urls`).  
- **mTLS errors:** Verify `SWISH_PFX_PATH` + `SWISH_PFX_PASSWORD` and ensure the certificate chain is valid.  
- **Replay always denied:** Clear the in-memory/Redis nonce store or use a fresh nonce when testing.

---

## 🧩 ASP.NET Core integration (strict validation)

```csharp
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(Environment.GetEnvironmentVariable("SWISH_BASE_URL")
        ?? throw new InvalidOperationException("Missing SWISH_BASE_URL"));
    opts.ApiKey = Environment.GetEnvironmentVariable("SWISH_API_KEY")
        ?? throw new InvalidOperationException("Missing SWISH_API_KEY");
    opts.Secret = Environment.GetEnvironmentVariable("SWISH_SECRET")
        ?? throw new InvalidOperationException("Missing SWISH_SECRET");
});

var app = builder.Build();

app.MapGet("/ping", async (ISwishClient swish) => await swish.PingAsync());
app.Run();
```

---

## 🛠️ Quick development commands

**Build & test**
```powershell
dotnet build
dotnet test
```

**Run sample (development)**
```powershell
dotnet watch --project .\samples\SwishSample.Web\SwishSample.Web.csproj run
```

---

## ⏱️ HTTP timeout & retries (named client "Swish")

The SDK provides an **opt-in** named `HttpClient` **"Swish"** with:  
- **Timeout:** 30 seconds  
- **Retry policy:** up to 3 retries with exponential backoff + jitter  
  (on status codes 408, 429, 5xx, `HttpRequestException`, and `TaskCanceledException`)

**Enable:**
```csharp
services.AddSwishHttpClient(); // registers "Swish" (timeout + retry + mTLS if env vars exist)
```

**Extend or override:**
```csharp
services.AddSwishHttpClient();
services.AddHttpClient("Swish")
        .AddHttpMessageHandler(_ => new MyCustomHandler()); // runs outside SDK's retry pipeline
```

**Disable:**
- Do not call `AddSwishHttpClient()` (the default pipeline will be used — no retry/timeout).  
- Or re-register `"Swish"` manually to replace handlers or settings.

---

## 🛡️ Security Disclosure

If you discover a security issue, please report it privately to `security@nordapi.se`.  
Do **not** use GitHub Issues for security-related matters.

---

## 📦 License

This project is licensed under the **MIT License**.

---

_Last updated: October 2025_
