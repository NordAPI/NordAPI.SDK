# NordAPI.Swish SDK

> **Production notice**
> In-memory nonce store is for **development only**. In production you **must** use a persistent store (Redis/DB).
> Set `SWISH_REDIS` (or `REDIS_URL` / `SWISH_REDIS_CONN`). The sample fails fast in `Production` if none is set.

**Licensing notice:** NordAPI is an SDK. You need your own Swish/BankID production agreements and certificates. NordAPI does not provide them.


Official NordAPI SDK for Swish and upcoming BankID integrations.

[![Build](https://github.com/NordAPI/NordAPI.SwishSdk/actions/workflows/ci.yml/badge.svg)](https://github.com/NordAPI/NordAPI.SwishSdk/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/NordAPI.Swish.svg?label=NuGet)](https://www.nuget.org/packages/NordAPI.Swish)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)
![.NET](https://img.shields.io/badge/.NET-8%2B-blueviolet)

> 🇸🇪 Swedish version: [README.sv.md](./README.sv.md)
> ✅ See also: [Integration Checklist](../../docs/integration-checklist.md)

A lightweight and secure .NET SDK for integrating **Swish payments and refunds**, with a focus on safe test and development workflows.
Includes built-in HMAC signing, optional mTLS, and an internal retry/backoff mechanism for transient errors.
💡 *BankID SDK support is planned next — stay tuned for the `NordAPI.BankID` package.*

**Supported .NET versions:** .NET 8 (LTS). Planned: .NET 10 (LTS) support.

---

## 📚 Table of Contents
- [Requirements](#requirements)
- [Installation](#installation)
- [Quickstart — Minimal Program.cs](#quickstart--minimal-programcs)
- [Usage Example: Creating a Payment](#usage-example-creating-a-payment)
- [Typical Swish Flow (high-level)](#typical-swish-flow-high-level)
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
- **.NET 8+** (SDK and Runtime)
- Windows / macOS / Linux
- (Optional) Redis if you want distributed replay protection for webhooks

---

## Installation

Install from NuGet:

```powershell
dotnet add package NordAPI.Swish
```

Or via `PackageReference` in `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="NordAPI.Swish" />
</ItemGroup>
```

> Tip: omit a fixed version to pull the latest stable. Pin a concrete version in production deployments.

---

## Quickstart — Minimal Program.cs

> This block is **compilable** as a full file in a new `web` project (`dotnet new web`).
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
// NOTE (Quickstart): uses simple dev fallbacks; in production, use secrets and remove fallbacks.
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
dotnet add package NordAPI.Swish
# Replace Program.cs content
dotnet run
```

---

## Usage Example: Creating a Payment

> Replace `payeeAlias` with **your** Swish merchant number (MSISDN without “+”). Signatures and model names match the API Overview below.

```csharp
using Microsoft.AspNetCore.Mvc;

// Minimal API endpoint that creates a payment using the SDK model
app.MapPost("/payments", async ([FromBody] CreatePaymentRequest input, ISwishClient swish, CancellationToken ct) =>
{
    // Tip: validate Amount/Currency/aliases before calling Swish
    var payment = await swish.CreatePaymentAsync(input, ct);
    return Results.Ok(payment);
});
```

**Example request body**
```json
{
  "payerAlias": "46701234567",
  "payeeAlias": "1231181189",
  "amount": "100.00",
  "currency": "SEK",
  "message": "Test purchase",
  "callbackUrl": "https://yourdomain.test/webhook/swish"
}
```

**curl**
```bash
curl -v -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json; charset=utf-8" \
  --data-raw '{
    "payerAlias": "46701234567",
    "payeeAlias": "1231181189",
    "amount": "100.00",
    "currency": "SEK",
    "message": "Test purchase",
    "callbackUrl": "https://yourdomain.test/webhook/swish"
  }'
```

> In production, the flow is **asynchronous**: Swish will notify your backend via the webhook (`callbackUrl`). See the smoke test for signature details.

---

## Typical Swish Flow (high-level)

```
1) Client (web/app)
        |
        v
2) Your Backend
        |
        v
3) Swish API
        |
        v
4) User approves payment in Swish app
        |
        v
5) Swish sends callback (payment result)
        |
        v
6) Your Webhook endpoint
        |
        v
   Update order status / notify client

```

- Your backend creates the payment via `CreatePaymentAsync`.
- The end-user approves in the Swish app.
- Swish POSTs the result to your **webhook** (`callbackUrl`).
  Your webhook must verify HMAC (`X-Swish-Signature`) as **Base64** HMAC-SHA256 of `"<ts>\n<nonce>\n<body>"` (UTF-8).

---

## Configuration — Environment Variables & User-Secrets

| Variable               | Purpose                                   | Example                        |
|------------------------|-------------------------------------------|--------------------------------|
| `SWISH_BASE_URL`       | Base URL for Swish API                    | `https://example.invalid`      |
| `SWISH_API_KEY`        | API key for HMAC authentication           | `dev-key`                      |
| `SWISH_SECRET`         | Shared secret for HMAC                    | `dev-secret`                   |
| `SWISH_PFX_PATH`       | Path to client certificate (.pfx)         | `C:\certs\swish-client.pfx`    |
| `SWISH_PFX_PASSWORD`   | Password for the certificate              | `••••`                         |
| `SWISH_WEBHOOK_SECRET` | Secret for webhook HMAC                   | `dev_secret`                   |
| `SWISH_REDIS`          | Redis connection string (nonce store)     | `localhost:6379`               |
| `SWISH_DEBUG`          | Verbose logging / enable dev modes        | `1`                            |
| `SWISH_ALLOW_OLD_TS`   | Allow older timestamps (dev only)         | `1`                            |

Set with **User-Secrets** (example):
```powershell
dotnet user-secrets init
dotnet user-secrets set "SWISH_API_KEY" "dev-key"
dotnet user-secrets set "SWISH_SECRET" "dev-secret"
dotnet user-secrets set "SWISH_BASE_URL" "https://example.invalid"
```

> 🔒 Never commit secrets or certificates. Use environment variables, User-Secrets, or a vault (e.g., Azure Key Vault).

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

---

## Running Samples and Tests

```powershell
# Build the repository
dotnet restore
dotnet build

# Run the sample web app
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

For quick manual testing you can also POST the webhook using **curl** (bash/macOS/Linux).
**Signature spec:** HMAC-SHA256 over the canonical string `"<timestamp>\n<nonce>\n<body>"`, using `SWISH_WEBHOOK_SECRET`. Encode as **Base64**.

> 🧩 **Note:** Sign the exact UTF‑8 bytes of the compact JSON body (Content-Type: `application/json; charset=utf-8`). Any whitespace or prettifying will break signature verification.

### Required request headers
| Header              | Description                                       | Example                                |
|---------------------|-----------------------------------------------------|----------------------------------------|
| `X-Swish-Timestamp` | Unix timestamp in **seconds**                       | `1735589201`                           |
| `X-Swish-Nonce`     | Unique ID to prevent replay                         | `550e8400-e29b-41d4-a716-446655440000` |
| `X-Swish-Signature` | **Base64** HMAC-SHA256 of `"<ts>\n<nonce>\n<body>"` | `W9CzL8f...==`                         |

### Example webhook payload
```json
{
  "event": "payment_received",
  "paymentId": "pay_123456",
  "amount": "100.00",
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
body='{"event":"payment_received","paymentId":"pay_123456","amount":"100.00","currency":"SEK","payer":{"phone":"46701234567"},"metadata":{"orderId":"order_987"}}'

# 2) Compute canonical and Base64 signature (uses SWISH_WEBHOOK_SECRET)
canonical="$(printf "%s\n%s\n%s" "$ts" "$nonce" "$body")"
sig="$(printf "%s" "$canonical" | openssl dgst -sha256 -hmac "${SWISH_WEBHOOK_SECRET:-dev_secret}" -binary | openssl base64)"

# 3) Send
curl -v -X POST "http://localhost:5000/webhook/swish" \
  -H "Content-Type: application/json; charset=utf-8" \
  -H "X-Swish-Timestamp: $ts" \
  -H "X-Swish-Nonce: $nonce" \
  -H "X-Swish-Signature: $sig" \
  --data-raw "$body"
```

✅ **Expected (HTTP 200)**
```json
{"received": true}
```

❌ **Expected on replay (HTTP 409)**
```json
{"reason": "replay detected (nonce seen before)"}
```

> In production: set `SWISH_REDIS` (aliases `REDIS_URL` and `SWISH_REDIS_CONN` are accepted). Without Redis, an in-memory store is used (suitable for local development).

---

## API Overview (Signatures & Models)

> The types below illustrate the expected surface. Names/namespaces should match the library you reference.

```csharp
public interface ISwishClient
{
    Task<string> PingAsync(CancellationToken ct = default);

    Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken ct = default);
    Task<CreatePaymentResponse> GetPaymentStatusAsync(string paymentId, CancellationToken ct = default);

    Task<CreateRefundResponse> CreateRefundAsync(CreateRefundRequest request, CancellationToken ct = default);
    Task<CreateRefundResponse> GetRefundStatusAsync(string refundId, CancellationToken ct = default);
}

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

The SDK performs retry internally inside `SwishClient`.

- **Timeout:** 30 seconds (default HttpClient timeout)
- **Retry:** up to 3 attempts with exponential backoff + jitter on:
  - `408`
  - `429`
  - `5xx`
  - `HttpRequestException`
  - `TaskCanceledException` (timeout)

Retry is deterministic and scoped per operation.
The Idempotency-Key is generated once per operation and reused across all retry attempts.

If you register your own `HttpClient`, ensure you do not introduce an additional retry layer unless explicitly intended.

Common responses:
- **400 Bad Request** → validation error (check required fields).
- **401 Unauthorized** → invalid `SWISH_API_KEY`/`SWISH_SECRET` or missing headers.
- **429 Too Many Requests** → handled by the internal retry mechanism.
- **5xx** → transient; handled by the internal retry mechanism.

---

## Security Recommendations
- Use **User-Secrets** / Key Vault for secrets — never hardcode in code or commit to the repo.
- mTLS “allow invalid chain” must **only** be used locally (Debug). In production, enforce a valid chain.
- Webhook secret (`SWISH_WEBHOOK_SECRET`) should be rotated regularly and stored securely (e.g., Key Vault).

---

## Contributing (PR/CI Requirements)
1. Create a feature branch from `main`.
2. Verify locally: `dotnet build`, `dotnet test`, and webhook smoke test if modified.
3. Ensure README examples compile (Quickstart must be copy-paste runnable).
4. Open PR with description + checklist. CI must pass:
   - Build & tests green
   - (Optional) Lint/format
5. Code review → squash/merge.

---

## Release & Versioning
- **SemVer**: `MAJOR.MINOR.PATCH`
- CI publish is gated; tag the repo (e.g., `v1.0.0`) to publish to NuGet.
- The README in the package (`PackageReadmeFile`) is shown on NuGet.

Install a specific version:
```powershell
dotnet add package NordAPI.Swish --version 1.2.3
```

---

## FAQ
**401 in tests** — Check `SWISH_API_KEY`/`SWISH_SECRET` and ensure your clock is synchronized.
**Replay always denied** — Change `nonce` between calls and clear in-memory/Redis. Check `SWISH_REDIS`.
**mTLS error in production** — Validate `SWISH_PFX_PATH` + `SWISH_PFX_PASSWORD` and the certificate chain.

---

## License

MIT License. Security contact: `security@nordapi.com`.

---

_Last updated: November 2025_
