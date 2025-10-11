# NordAPI.Swish SDK (MVP) — Svensk README

[![Build](https://github.com/NordAPI/NordAPI.SwishSdk/actions/workflows/ci.yml/badge.svg)](https://github.com/NordAPI/NordAPI.SwishSdk/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/NordAPI.Swish.svg)](https://www.nuget.org/packages/NordAPI.Swish)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)

> 🇬🇧 English version: `README.md` (root)  
> ✅ Se även: `docs/integration-checklist.md`

**NordAPI.Swish** är ett lättviktigt och säkert .NET‑SDK för **Swish‑betalningar och återköp** i test‑ och utvecklingsmiljöer.  
HMAC‑signering, mTLS‑stöd och retry/rate‑limiting via `HttpClientFactory` ingår.

---

## Innehåll
- [Krav](#krav)
- [Installation](#installation)
- [Snabbstart — Minimal Program.cs](#snabbstart--minimal-programcs)
- [Konfiguration — Miljövariabler & User-Secrets](#konfiguration--miljövariabler--user-secrets)
- [mTLS (valfritt)](#mtls-valfritt)
- [Köra samples och tester](#köra-samples-och-tester)
- [Webhook röktest (smoke)](#webhook-röktest-smoke)
- [API-översikt (signaturer & modeller)](#api-översikt-signaturer--modeller)
- [Felscenarier & retry-policy](#felscenarier--retry-policy)
- [Säkerhetsrekommendationer](#säkerhetsrekommendationer)
- [Contributing (PR/CI-krav)](#contributing-prci-krav)
- [Release & versionering](#release--versionering)
- [FAQ](#faq)
- [Licens](#licens)

---

## Krav
- **.NET 8.0** (SDK och Runtime)
- Windows/macOS/Linux
- (Valfritt) Redis om du vill ha distribuerat replay‑skydd för webhooks

---

## Installation

Installera senaste stabila NuGet‑versionen:

```powershell
dotnet add package NordAPI.Swish --version x.y.z
```

> Tips: ersätt `x.y.z` med nuvarande badge‑version eller utelämna `--version` för senaste.

Alternativ: via `PackageReference` i `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="NordAPI.Swish" Version="x.y.z" />
</ItemGroup>
```

---

## Snabbstart — Minimal Program.cs

> Detta block är **kompilerbart** som en hel fil i ett nytt `console`/minimal API‑projekt (`dotnet new web`).  
> Fil: `Program.cs`

```csharp
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Registrera Swish-klienten med miljövariabler
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

Kör:
```powershell
dotnet new web -n SwishQuickStart
cd SwishQuickStart
dotnet add package NordAPI.Swish --version x.y.z
# Klistra in Program.cs över innehållet
dotnet run
```

---

## Konfiguration — Miljövariabler & User-Secrets

| Variabel             | Syfte                                      | Exempel                         |
|----------------------|--------------------------------------------|----------------------------------|
| `SWISH_BASE_URL`     | Bas‑URL till Swish‑API                     | `https://example.invalid`        |
| `SWISH_API_KEY`      | API‑nyckel för HMAC                        | `dev-key`                        |
| `SWISH_SECRET`       | Hemlighet för HMAC                         | `dev-secret`                     |
| `SWISH_PFX_PATH`     | Sökväg till klientcertifikat (.pfx)        | `C:\certs\swish-client.pfx`    |
| `SWISH_PFX_PASSWORD` | Lösenord till klientcertifikat             | `••••`                           |
| `SWISH_WEBHOOK_SECRET` | Hemlighet för webhook‑HMAC               | `dev_secret`                     |
| `SWISH_REDIS`        | Redis connection string (nonce‑store)      | `localhost:6379`                 |
| `SWISH_DEBUG`        | Verbosare loggning / tillåt dev‑lägen      | `1`                              |
| `SWISH_ALLOW_OLD_TS` | Tillåt äldre timestamps (endast dev)       | `1`                              |

Sätta via **User‑Secrets** (exempel):
```powershell
dotnet user-secrets init
dotnet user-secrets set "SWISH_API_KEY" "dev-key"
dotnet user-secrets set "SWISH_SECRET" "dev-secret"
dotnet user-secrets set "SWISH_BASE_URL" "https://example.invalid"
```

---

## mTLS (valfritt)

Aktivera klientcertifikat (PFX):
```powershell
$env:SWISH_PFX_PATH = "C:\certs\swish-client.pfx"
$env:SWISH_PFX_PASSWORD = "hemligt-lösenord"
```

**Beteende**
- Inget certifikat → fallback utan mTLS.  
- **Debug**: avslappnad servercert‑validering (endast lokalt).  
- **Release**: strikt certkedja (ingen ”allow invalid chain”).

> Produktion: lagra cert/secret i **Azure Key Vault** eller liknande — aldrig i repo.

---

## Köra samples och tester

```powershell
# Bygg hela repo
dotnet restore
dotnet build

# Kör sample (Web)
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000

# Kör tester
dotnet test
```

---

## Webhook röktest (smoke)

Starta sample‑servern i ett fönster:
```powershell
$env:SWISH_WEBHOOK_SECRET = "dev_secret"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

Kör smoke från ett annat fönster:
```powershell
.\scripts\smoke-webhook.ps1 -Secret dev_secret -Url http://localhost:5000/webhook/swish
```

**Success**
```json
{"received": true}
```

**Error (replay)**
```json
{"reason": "replay upptäckt (nonce sedd tidigare)"}
```

> För produktion: sätt `SWISH_REDIS`. Sample accepterar även aliasen `REDIS_URL` och `SWISH_REDIS_CONN`. Utan Redis används in‑memory‑store (bra för lokal utveckling).

---

## API-översikt (signaturer & modeller)

**ISwishClient**
```csharp
Task<string> PingAsync(CancellationToken ct = default);

Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken ct = default);
Task<CreatePaymentResponse> GetPaymentStatusAsync(string paymentId, CancellationToken ct = default);

Task<CreateRefundResponse> CreateRefundAsync(CreateRefundRequest request, CancellationToken ct = default);
Task<CreateRefundResponse> GetRefundStatusAsync(string refundId, CancellationToken ct = default);
```

**Exempel: CreatePaymentRequest / Response (förenklad modell)**
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

**JSON‑exempel (response)**
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

## Felscenarier & retry-policy

SDK:t registrerar en namngiven `HttpClient` **"Swish"** med:
- **Timeout:** 30s  
- **Retry:** upp till 3 försök (exponentiell backoff + jitter) på `408`, `429`, `5xx`, `HttpRequestException`, `TaskCanceledException` (timeout).

Aktivera/ersätt:
```csharp
services.AddSwishHttpClient(); // registrerar "Swish" (timeout + retry + mTLS om miljövariabler finns)
services.AddHttpClient("Swish")
        .AddHttpMessageHandler(_ => new MyCustomHandler()); // utanför SDKs retry-pipeline
```

Vanliga svar:
- **400 Bad Request** → valideringsfel (kontrollera obligatoriska fält).  
- **401 Unauthorized** → felaktig `SWISH_API_KEY`/`SWISH_SECRET` eller saknade headers.  
- **429 Too Many Requests** → följ retry‑policy eller backoff.  
- **5xx** → transient; retry triggas automatiskt av pipeline.

---

## Säkerhetsrekommendationer
- Använd **User‑Secrets**/Key Vault för hemligheter — aldrig hårdkodat i kod eller repo.  
- `allowInvalidChainForDev` ska **endast** användas lokalt. I prod krävs giltig certkedja.  
- Webhook‑hemlighet (`SWISH_WEBHOOK_SECRET`) roteras regelbundet; lagras säkert.

---

## Contributing (PR/CI-krav)
1. Skapa branch från `main`.
2. Kör lokalt: `dotnet build`, `dotnet test`, och webhook‑smoke om du ändrat den delen.
3. Se till att README‑exempel fortfarande kompilerar (snabbstart **måste** gå att klistra in).
4. Öppna PR med beskrivning + checklista. CI måste vara grön:
   - Build & test passerar
   - (Valfritt) Lint/format
5. Code review → squash/merge.

---

## Release & versionering
- **SemVer**: `MAJOR.MINOR.PATCH`  
- Tagga via GitHub Release (t.ex. `v1.0.0`) → CI packar och publicerar till NuGet (automatiserat).  
- README i paketroten (`PackageReadmeFile`) visas på NuGet.

Installera specifik version:
```powershell
dotnet add package NordAPI.Swish --version x.y.z
```

---

## FAQ
**Får 401 i test.**  
Kontrollera `SWISH_API_KEY`/`SWISH_SECRET` och att klockan inte driver (timestamp kan nekas).

**Replay nekar alltid.**  
Byt `nonce` mellan anrop och rensa in‑memory/Redis. Kontrollera att `SWISH_REDIS` är korrekt i prod.

**mTLS fel i prod.**  
Validera `SWISH_PFX_PATH` + `SWISH_PFX_PASSWORD` och certkedjan.

---

## Licens

MIT‑licens. Se `LICENSE`.




