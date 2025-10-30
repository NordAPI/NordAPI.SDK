# NordAPI.Swish SDK

Official NordAPI SDK for Swish and upcoming BankID integrations.

[![Build](https://github.com/NordAPI/NordAPI.SwishSdk/actions/workflows/ci.yml/badge.svg)](https://github.com/NordAPI/NordAPI.SwishSdk/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/NordAPI.Swish.svg?label=NuGet)](https://www.nuget.org/packages/NordAPI.Swish)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)
![.NET](https://img.shields.io/badge/.NET-7%2B-blueviolet)

> 🇸🇪 Swedish version: [README.sv.md](./README.sv.md)  
> ✅ See also: [Integration Checklist](./docs/integration-checklist.md)

A lightweight and secure .NET SDK for integrating **Swish payments and refunds** in test and development environments.  
Includes built-in support for HMAC authentication, mTLS, and rate limiting.  
💡 *BankID SDK support is planned next — stay tuned for the NordAPI.BankID package.*

**Requires .NET 7+ (LTS compatible)**

---

## 📚 Table of Contents
- [🚀 Features](#-features)
- [⚡ Quick start (ASP.NET Core)](#-quick-start-aspnet-core)
- [🔐 mTLS via environment variables](#-mtls-via-environment-variables-optional)
- [🧪 Run & smoke test](#-run--smoke-test)
- [🌐 Common environment variables](#-common-environment-variables)
- [🧰 Troubleshooting](#-troubleshooting)
- [🧩 ASP.NET Core integration](#-aspnet-core-integration-strict-validation)
- [🛠️ Quick development commands](#️-quick-development-commands)
- [⏱️ HTTP timeout & retries](#️-http-timeout--retries-named-client-swish)
- [💬 Getting help](#-getting-help)
- [🛡️ Security Disclosure](#️-security-disclosure)
- [📦 License](#-license)

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
```powershell
dotnet add package NordAPI.Swish
```

Or add a project reference:
```xml
<ItemGroup>
  <ProjectReference Include="..\src\NordAPI.Swish\NordAPI.Swish.csproj" />
</ItemGroup>
```

### 2) Register the client in *Program.cs*
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

### 3) Use in your code
```csharp
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

For quick manual testing you can also POST the webhook using **curl** (bash/macOS/Linux).  
**Signature spec:** HMAC-SHA256 over the canonical string **`"<timestamp>\n<nonce>\n<body>"`**, using **`SWISH_WEBHOOK_SECRET`**. Encode as **Base64**.

### Required request headers
| Header              | Description                                       | Example                              |
|---------------------|---------------------------------------------------|--------------------------------------|
| `X-Swish-Timestamp` | Unix timestamp in **seconds**                     | `1735589201`                         |
| `X-Swish-Nonce`     | Unique ID to prevent replay                       | `550e8400-e29b-41d4-a716-446655440000` |
| `X-Swish-Signature` | **Base64** HMAC-SHA256 of `"<ts>\n<nonce>\n<body>"` | `W9CzL8f...==`                        |

### Example webhook payload
```json
{
  "event": "payment_received",
  "paymentId": "pay_123456",
  "amount": 100.00,
  "currency": "SEK",
  "payer": { "phone": "46701234567" },
  "metadata": { "orderId": "order_987" }
}
```

### curl smoke test (bash / macOS / Linux)
```bash
# 1) Prepare values
ts="$(date +%s)"
nonce="$(uuidgen)"
body='{"event":"payment_received","paymentId":"pay_123456","amount":100.00,"currency":"SEK","payer":{"phone":"46701234567"},"metadata":{"orderId":"order_987"}}'

# 2) Compute canonical and Base64 signature (uses SWISH_WEBHOOK_SECRET)
canonical="$(printf "%s\n%s\n%s" "$ts" "$nonce" "$body")"
sig="$(printf "%s" "$canonical" | openssl dgst -sha256 -hmac "${SWISH_WEBHOOK_SECRET:-dev_secret}" -binary | openssl base64)"

# 3) Send
curl -v -X POST "http://localhost:5000/webhook/swish"   -H "Content-Type: application/json"   -H "X-Swish-Timestamp: $ts"   -H "X-Swish-Nonce: $nonce"   -H "X-Swish-Signature: $sig"   --data-raw "$body"
```

> Windows tip: PowerShell users can run the provided script or use `Invoke-RestMethod`. Ensure you compute **Base64 HMAC** over `"<ts>\n<nonce>\n<body>"` and set `X-Swish-Signature` accordingly.

✅ **Expected (Success)**
```json
{"received": true}
```

❌ **Expected on replay (Error)**
```json
{"reason": "replay detected (nonce seen before)"}
```

> In production: set `SWISH_REDIS` (aliases `REDIS_URL` and `SWISH_REDIS_CONN` are accepted). Without Redis, an in-memory store is used (good for local dev).

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

## 💬 Getting help

- 📂 Open [GitHub Issues](https://github.com/NordAPI/NordAPI.SwishSdk/issues) for general questions or bug reports.  
- 🔒 Security concerns? Email [security@nordapi.se](mailto:security@nordapi.se).

---

## 🛡️ Security Disclosure

If you discover a security issue, please report it privately to `security@nordapi.se`.  
Do **not** use GitHub Issues for security-related matters.

---

## 📦 License

This project is licensed under the **MIT License**.

---

_Last updated: November 2025_

