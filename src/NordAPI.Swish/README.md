# NordAPI.Swish SDK (MVP) — English README

[![Build](https://github.com/NordAPI/NordAPI.SwishSdk/actions/workflows/ci.yml/badge.svg)](https://github.com/NordAPI/NordAPI.SwishSdk/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/NordAPI.Swish.svg)](https://www.nuget.org/packages/NordAPI.Swish)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)

> 🇸🇪 Swedish version: `README.sv.md`  
> ✅ See also: `docs/integration-checklist.md`

**NordAPI.Swish** is a lightweight and secure .NET SDK for **Swish payments and refunds** in test and development environments.  
Built-in HMAC signing, mTLS support, and retry/rate limiting via `HttpClientFactory`.

---

## Table of Contents
- [Requirements](#requirements)
- [Installation](#installation)
- [Quickstart — Minimal Program.cs](#quickstart--minimal-programcs)
- [Configuration — Environment Variables & User-Secrets](#configuration--environment-variables--user-secrets)
- [mTLS (optional)](#mtls-optional)
- [Running Samples and Tests](#running-samples-and-tests)
- [Webhook Smoke Test](#webhook-smoke-test)
- [API Overview (Signatures & Models)](#api-overview-signatures--models)
- [Error Scenarios & Retry Policy](#error-scenarios--retry-policy)
- [Security Recommendations](#security-recommendations)
- [Contributing (PR/CI Requirements)](#contributing-prci-requirements)
- [Release & Versioning](#release--versioning)
- [FAQ](#faq)
- [License](#license)

---

## Requirements
- **.NET 8.0** (SDK and Runtime)
- Windows / macOS / Linux
- (Optional) Redis if you want distributed replay protection for webhooks

---

## Installation

Install the latest stable NuGet version:

```powershell
dotnet add package NordAPI.Swish --version x.y.z
```

> Tip: replace `x.y.z` with the current badge version or omit `--version` for the latest.

Alternative: via `PackageReference` in `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="NordAPI.Swish" Version="x.y.z" />
</ItemGroup>
```

---

## Quickstart — Minimal Program.cs

> This block is **compilable** as a full file in a new `console` or minimal API project (`dotnet new web`).  
> File: `Program.cs`

```csharp
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Register the Swish client using environment variables
builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(Environment.GetEnvironmentVariable("SWISH_BASE_URL")
        ?? "https://example.invalid");
    opts.ApiKey = Environment.GetEnvironmentVariable("SWISH_API_KEY")
        ?? "dev-key";
    opts.Secret = Environment.GetEnvironmentVariable("SWISH_SECRET")
        ?? "dev-secret";
});

var app = builder.Build();

app.MapGet("/ping", async (ISwishClient swish) =>
{
    var result = await swish.PingAsync();
    return Results.Ok(new { ping = result });
});

app.Run();
```

Run:
```powershell
dotnet new web -n SwishQuickStart
cd SwishQuickStart
dotnet add package NordAPI.Swish --version x.y.z
# Replace Program.cs content
dotnet run
```

---

## Configuration — Environment Variables & User-Secrets

| Variable              | Purpose                                    | Example                        |
|------------------------|--------------------------------------------|--------------------------------|
| `SWISH_BASE_URL`       | Base URL for Swish API                    | `https://example.invalid`      |
| `SWISH_API_KEY`        | API key for HMAC authentication           | `dev-key`                      |
| `SWISH_SECRET`         | Shared secret for HMAC                    | `dev-secret`                   |
| `SWISH_PFX_PATH`       | Path to client certificate (.pfx)         | `C:\certs\swish-client.pfx`  |
| `SWISH_PFX_PASSWORD`   | Password for the certificate              | `••••`                         |
| `SWISH_WEBHOOK_SECRET` | Secret for webhook HMAC                   | `dev_secret`                   |
| `SWISH_REDIS`          | Redis connection string (nonce store)     | `localhost:6379`               |
| `SWISH_DEBUG`          | Verbose logging / enable dev modes        | `1`                            |
| `SWISH_ALLOW_OLD_TS`   | Allow older timestamps (dev only)         | `1`                            |

Setting with **User-Secrets** (example):
```powershell
dotnet user-secrets init
dotnet user-secrets set "SWISH_API_KEY" "dev-key"
dotnet user-secrets set "SWISH_SECRET" "dev-secret"
dotnet user-secrets set "SWISH_BASE_URL" "https://example.invalid"
```

---

## mTLS (optional)

Enable client certificate (PFX):
```powershell
$env:SWISH_PFX_PATH = "C:\certs\swish-client.pfx"
$env:SWISH_PFX_PASSWORD = "secret-password"
```

**Behavior**
- No certificate → fallback without mTLS.  
- **Debug:** relaxed server certificate validation (local only).  
- **Release:** strict certificate chain (no “allow invalid chain”).

> In production: store certificates/secrets in **Azure Key Vault** or similar — never in the repo.

---

## Running Samples and Tests

```powershell
# Build the entire repo
dotnet restore
dotnet build

# Run sample (Web)
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000

# Run tests
dotnet test
```

---

## Webhook Smoke Test

Start the sample server in one terminal:
```powershell
$env:SWISH_WEBHOOK_SECRET = "dev_secret"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

Run the smoke test in another terminal:
```powershell
.\scripts\smoke-webhook.ps1 -Secret dev_secret -Url http://localhost:5000/webhook/swish
```

**Success**
```json
{"received": true}
```

**Error (replay)**
```json
{"reason": "replay detected (nonce seen before)"}
```

> In production: set `SWISH_REDIS`. The sample also accepts aliases `REDIS_URL` and `SWISH_REDIS_CONN`. Without Redis, an in-memory store is used (suitable for local development).

---

## API Overview (Signatures & Models)

**ISwishClient**
```csharp
Task<string> PingAsync(CancellationToken ct = default);

Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken ct = default);
Task<CreatePaymentResponse> GetPaymentStatusAsync(string paymentId, CancellationToken ct = default);

Task<CreateRefundResponse> CreateRefundAsync(CreateRefundRequest request, CancellationToken ct = default);
Task<CreateRefundResponse> GetRefundStatusAsync(string refundId, CancellationToken ct = default);
```

**Example: CreatePaymentRequest / Response (simplified model)**
```csharp
public sealed record CreatePaymentRequest(
    string PayerAlias,
    string PayeeAlias,
    string Amount,
    string Currency,
    string Message,
    string CallbackUrl
);

public sealed record CreatePaymentResponse(
    string Id,
    string Status,
    string? ErrorCode = null,
    string? ErrorMessage = null
);
```

**JSON Example (Response)**
```json
{
  "id": "PAYMENT-123",
  "status": "CREATED"
}
```

**Refund**
```csharp
public sealed record CreateRefundRequest(
    string OriginalPaymentReference,
    string Amount,
    string Currency,
    string Message,
    string CallbackUrl
);

public sealed record CreateRefundResponse(
    string Id,
    string Status,
    string? ErrorCode = null,
    string? ErrorMessage = null
);
```

---

## Error Scenarios & Retry Policy

The SDK registers a named `HttpClient` **"Swish"** with:
- **Timeout:** 30s  
- **Retry:** up to 3 attempts (exponential backoff + jitter) on `408`, `429`, `5xx`, `HttpRequestException`, `TaskCanceledException` (timeout).

Enable/extend:
```csharp
services.AddSwishHttpClient(); // registers "Swish" (timeout + retry + mTLS if environment vars exist)
services.AddHttpClient("Swish")
        .AddHttpMessageHandler(_ => new MyCustomHandler()); // outside SDK retry-pipeline
```

Common responses:
- **400 Bad Request** → validation error (check required fields).  
- **401 Unauthorized** → invalid `SWISH_API_KEY`/`SWISH_SECRET` or missing headers.  
- **429 Too Many Requests** → follow retry policy/backoff.  
- **5xx** → transient; retry automatically triggered by pipeline.

---

## Security Recommendations
- Use **User-Secrets** / Key Vault for secrets — never hardcode in code or repo.  
- `allowInvalidChainForDev` should **only** be used locally. In production, a valid certificate chain is required.  
- Webhook secret (`SWISH_WEBHOOK_SECRET`) should be rotated regularly and stored securely.

---

## Contributing (PR/CI Requirements)
1. Create a branch from `main`.
2. Run locally: `dotnet build`, `dotnet test`, and webhook smoke test if modified.
3. Ensure README examples still compile (Quickstart **must** be copy-paste runnable).
4. Open PR with description + checklist. CI must pass:
   - Build & tests green
   - (Optional) Lint/format
5. Code review → squash/merge.

---

## Release & Versioning
- **SemVer**: `MAJOR.MINOR.PATCH`  
- Tag via GitHub Release (e.g., `v1.0.0`) → CI builds and publishes automatically to NuGet.  
- The README in the package root (`PackageReadmeFile`) is shown on NuGet.

Install a specific version:
```powershell
dotnet add package NordAPI.Swish --version x.y.z
```

---

## FAQ
**Receiving 401 in tests.**  
Check `SWISH_API_KEY`/`SWISH_SECRET` and ensure your clock is synchronized (timestamp may be rejected).

**Replay always denied.**  
Change `nonce` between calls and clear in-memory/Redis. Check `SWISH_REDIS` in production.

**mTLS error in production.**  
Validate `SWISH_PFX_PATH` + `SWISH_PFX_PASSWORD` and the certificate chain.

---

## License

MIT License. See `LICENSE`.