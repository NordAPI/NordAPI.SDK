# NordAPI.Swish SDK

Officiellt NordAPI-bibliotek för Swish-betalningar.

[![Build](https://github.com/NordAPI/NordAPI.Swish/actions/workflows/ci.yml/badge.svg)](https://github.com/NordAPI/NordAPI.Swish/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/NordAPI.Swish.svg?label=NuGet)](https://www.nuget.org/packages/NordAPI.Swish)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)
![.NET](https://img.shields.io/badge/.NET-8%2B-blueviolet)

> 🇬🇧 English version: [README.md](https://github.com/NordAPI/NordAPI.Swish/blob/main/README.md)
> ✅ **Dokumentation**
> - [Integrationschecklista (Live)](https://nordapi.net/integration-checklist/) – *Rekommenderas för uppdaterade operativa noter (t.ex. callback-baseline + driftsguidance).*
> - [Säkerhet & Compliance (Repo)](https://github.com/NordAPI/NordAPI.Swish/blob/main/docs/compliance.md) – *Säkerhetsrelevant beteende + ansvarsfördelning för denna SDK-version.*

Ett lättviktigt och säkert .NET SDK för att integrera **Swish-betalningar och återköp** med deterministiska, fail-closed standardval.
Inkluderar mTLS som är påslaget som standard, valfri HMAC-härdning för webhook-verifiering och hjälpfunktioner för hastighetsbegränsning.
💡 *Stöd för BankID kommer härnäst — håll utkik efter paketet `NordAPI.BankID`.*

**Kräver .NET 8+ (LTS-kompatibel)**

> **Produktionsnotis**
> Minneslagring av nonce är endast för **utvecklingsmiljö**. I produktion **måste** du använda en persistent lagring (Redis/DB).
> Ange `SWISH_REDIS` (eller `REDIS_URL` / `SWISH_REDIS_CONN`). Exempelappen stoppar start i `Production` om ingen Redis är satt.

**Licensnotis:** NordAPI är ett SDK. Du behöver egna Swish/BankID-avtal och certifikat. NordAPI tillhandahåller inte dessa.

---

## Trust Guarantees

NordAPI skiljer mellan Swishs inbyggda transportgarantier och NordAPIs hardening-garantier.

- Swish tillhandahåller den grundläggande callback-/API-transportmodellen.
- NordAPI lägger till valfri hardening på applikationsnivå, såsom canonical byte-verifiering, Base64 HMAC-validering, Unix-seconds timestamp-hantering och nonce-baserat replay-skydd.
- `X-Swish-Timestamp`, `X-Swish-Nonce` och `X-Swish-Signature` är NordAPI-specifika hardening-headers och skickas inte av Swish som en del av den inbyggda callback-modellen.
- mTLS är enforced som standard för API-transport.
- `Idempotency-Key` genereras en gång per logisk operation och samma nyckel återanvänds vid retries.
- Ingen gateway- eller proxy-layer krävs för normal drift.

Se den publika Integration Checklist och Security & Compliance-noterna ovan för operativ vägledning och tydliga ansvarsfördelningar.

---

## Scope

För att hålla SDK:t fokuserat och lätt att granska är gränserna medvetet snäva.

### Inom scope
- **mTLS för Swish API-kommunikation**
- **Deterministisk HMAC-SHA256-signering och webhook-verifiering**
- **Spec-låst hantering av säkerhetsrelevanta protokollelement** (timestamps i sekunder, nonces, canonicalization)
- **Fail-closed-validering av säkerhetskänslig konfiguration** under applikationsstart

### Utanför scope
- **Hantering av hemligheter** (generering, lagring, rotation)
- **Infrastruktursäkerhet** (WAF, brandväggar, IDS/IPS)
- **Persistensimplementation** för nonce-lagring (Redis/SQL-setup)
- **Merchant onboarding, avtal och extern tillgänglighet i Swish-plattformen**

---

## 📚 Innehållsförteckning
- [🚀 Funktioner](#-funktioner)
- [📦 Krav](#-krav)
- [⬇️ Installation](#️-installation)
- [⚡ Snabbstart — Minimal Program.cs](#-snabbstart--minimal-programcs)
- [💳 Exempel på användning: Skapa en betalning](#-exempel-på-användning-skapa-en-betalning)
- [🧭 Typiskt Swish-flöde (översikt)](#-typiskt-swish-flöde-översikt)
- [🧰 Konfiguration — Miljövariabler & User-Secrets](#-konfiguration--miljövariabler--user-secrets)
- [🔐 mTLS (krävs som standard)](#-mtls-krävs-som-standard)
- [🧪 Köra samples och tester](#-köra-samples-och-tester)
- [🧾 Webhook-röktest](#-webhook-röktest)
- [🧩 API-översikt (Signaturer & Modeller)](#-api-översikt-signaturer--modeller)
- [⏱️ Felscenarier & Retry-policy](#-felscenarier--retry-policy)
- [❓ FAQ](#-faq)
- [💬 Få hjälp](#-få-hjälp)
- [🚨 Rapportera säkerhetsbrister](#-rapportera-säkerhetsbrister)
- [🛠️ Bidra](#️-bidra)
- [📦 Release & Versionering](#-release--versionering)
- [📜 Licens](#-licens)

---

## 🚀 Funktioner
- ✅ **Skapa och verifiera Swish-betalningar**
- 🔁 **Stöd för återköp (Refunds)**
- 🔐 **mTLS-driven transport och valfri webhook-härdning**
- ⏱️ **Deterministisk retry- och timeout-logik**
- 🧪 **ASP.NET Core-integration** (fluent registration)
- 🧰 **Konfiguration via miljövariabler** (User-Secrets & Vault-ready)

---

## 📦 Krav
- **.NET 8+** (SDK och Runtime)
- Windows / macOS / Linux
- *(Valfritt)* Redis om du vill ha distribuerat replay-skydd för webhooks

---

## ⬇️ Installation

Installera via NuGet:

```powershell
dotnet add package NordAPI.Swish
```

Eller via `PackageReference` i `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="NordAPI.Swish" />
</ItemGroup>
```

> Tips: utelämna fast version för att hämta senaste stabila. Pinna en specifik version i produktion.

---

## ⚡ Snabbstart — Minimal Program.cs

> Denna kod är **körbar** som en komplett fil i ett nytt `web`-projekt (`dotnet new web`).
> Fil: `Program.cs`

```csharp
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Registrera Swish-klienten via miljövariabler
// OBS (Snabbstart): använder enkla dev-fallbacks; i produktion, använd secrets och ta bort fallbacks.
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
dotnet add package NordAPI.Swish
# Ersätt innehållet i Program.cs
dotnet run
```

---

## 💳 Exempel på användning: Skapa en betalning

> Ersätt `payeeAlias` med **ditt** Swish-handlarnummer (MSISDN utan “+”). Signaturer och modellnamn matchar API-översikten nedan.

```csharp
using Microsoft.AspNetCore.Mvc;

// Minimal API-endpoint som skapar en betalning via SDK-modellen
app.MapPost("/payments", async ([FromBody] CreatePaymentRequest input, ISwishClient swish, CancellationToken ct) =>
{
    // Tips: validera Amount/Currency/alias innan anrop till Swish
    var payment = await swish.CreatePaymentAsync(input, ct);
    return Results.Ok(payment);
});
```

**Exempel på request body**
```json
{
  "payerAlias": "46701234567",
  "payeeAlias": "1231181189",
  "amount": "100.00",
  "currency": "SEK",
  "message": "Testköp",
  "callbackUrl": "https://yourdomain.test/webhook/swish"
}
```

**curl**
```bash
curl -v -X POST http://localhost:5000/payments \
  -H "Content-Type: application/json" \
  --data-raw '{
    "payerAlias": "46701234567",
    "payeeAlias": "1231181189",
    "amount": "100.00",
    "currency": "SEK",
    "message": "Testköp",
    "callbackUrl": "https://yourdomain.test/webhook/swish"
  }'
```

> I produktion är flödet **asynkront**: Swish notifierar din backend via webhook (`callbackUrl`). Se röktestet för signaturdetaljer.

---

## 🧭 Typiskt Swish-flöde (översikt)

```
1) Klient (webb/app)
        |
        v
2) Din backend
        |
        v
3) Swish API
        |
        v
4) Användaren godkänner betalning i Swish-appen
        |
        v
5) Swish skickar callback (betalningsresultat)
        |
        v
6) Din Webhook-endpoint
        |
        v
   Uppdatera orderstatus / notifiera klient
```

- Din backend skapar betalningen via `CreatePaymentAsync`.
- Slutanvändaren godkänner i Swish-appen.
- Swish POST:ar resultatet till din **webhook** (`callbackUrl`).

> Obs: Swish skickar inte dessa `X-Swish-*` headers som standard. Det här är NordAPI:s valfria webhook-hardening-mönster (du lägger det vid din edge).

Din webhook måste verifiera HMAC (`X-Swish-Signature`) som **Base64** HMAC-SHA256 av `"<ts>\n<nonce>\n<body>"` (UTF-8).

---

## 🧰 Konfiguration — Miljövariabler & User-Secrets

| Variabel               | Syfte                                        | Exempel                         |
|------------------------|----------------------------------------------|----------------------------------|
| `SWISH_BASE_URL`       | Bas-URL för Swish API                        | `https://example.invalid`        |
| `SWISH_API_KEY`        | API-nyckel för HMAC                          | `dev-key`                        |
| `SWISH_SECRET`         | Delad hemlighet för HMAC                     | `dev-secret`                     |
| `SWISH_PFX_PATH`       | Sökväg till klientcertifikat (.pfx)          | `C:\certs\swish-client.pfx`      |
| `SWISH_PFX_PASSWORD`   | Lösenord för certifikatet                    | `••••`                           |
| `SWISH_WEBHOOK_SECRET` | Hemlighet för webhook-HMAC                   | `dev_secret`                     |
| `SWISH_REDIS`          | Redis-anslutningssträng (nonce-store)        | `localhost:6379`                 |
| `SWISH_DEBUG`          | (DEV ONLY) Debug / relaxed dev behavior      | `1`                              |
| `SWISH_ALLOW_OLD_TS`   | (DEV ONLY) Tillåt gamla webhook-timestamps   | `1`                              |

> ⚠️ **ENDAST DEV:** `SWISH_DEBUG` och `SWISH_ALLOW_OLD_TS` får aldrig vara aktiverade i produktion.

Sätt med **User-Secrets** (exempel):
```powershell
dotnet user-secrets init
dotnet user-secrets set "SWISH_API_KEY" "dev-key"
dotnet user-secrets set "SWISH_SECRET" "dev-secret"
dotnet user-secrets set "SWISH_BASE_URL" "https://example.invalid"
```

> 🔒 Commita aldrig hemligheter eller certifikat. Använd miljövariabler, User-Secrets eller ett valv (t.ex. Azure Key Vault).

---

## 🔐 mTLS (krävs som standard)

Aktivera klientcertifikat (PFX):
```powershell
$env:SWISH_PFX_PATH = "C:\certs\swish-client.pfx"
$env:SWISH_PFX_PASSWORD = "hemligt-lösenord"
```

**Beteende**
- Som standard kräver SDK:t mTLS.
- Om `RequireMtls = true` (standard) och certifikat saknas kastar SDK:t `SwishConfigurationException` när HTTP-handlern skapas.
- Sätt `RequireMtls = false` endast för kontrollerad lokal testning/mock där mTLS medvetet inte används.
- **Debug:** avslappnad servercertifikatvalidering (endast lokalt).
- **Release:** strikt certkedja (ingen "allow invalid chain").

---

## 🧪 Köra samples och tester

```powershell
# Bygg hela repot
dotnet restore
dotnet build

# Kör sample-webbappen
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000

# Kör tester
dotnet test
```

---

## 🧾 Webhook-röktest

Starta sample-servern i en terminal:
```powershell
$env:SWISH_WEBHOOK_SECRET = "dev_secret"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

Kör röktestet i en annan terminal:
```powershell
.\scripts\smoke-webhook.ps1 -Secret dev_secret -Url http://localhost:5000/webhook/swish
```

För snabb manuell testning kan du även POST:a webhooken med **curl** (bash/macOS/Linux).
**Signatur-spec:** HMAC-SHA256 över den kanoniska strängen `"<timestamp>\n<nonce>\n<body>"`, med `SWISH_WEBHOOK_SECRET`. Kodas som **Base64**.

> 🧩 **Notis:** Signera exakt UTF-8-bytes från den råa request body:n. All whitespace/prettifying kan bryta signaturverifieringen.

### Obligatoriska headers
| Header              | Beskrivning                                         | Exempel                                |
|---------------------|-----------------------------------------------------|----------------------------------------|
| `X-Swish-Timestamp` | Unix-timestamp i **sekunder**                       | `1735589201`                           |
| `X-Swish-Nonce`     | Unikt ID för att förhindra replay                   | `550e8400-e29b-41d4-a716-446655440000` |
| `X-Swish-Signature` | **Base64** HMAC-SHA256 av `"<ts>\n<nonce>\n<body>"` | `W9CzL8f...==`                         |

### Exempel på webhook-payload
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

### curl-röktest (bash / macOS / Linux)
```bash
# 1) Förbered värden
ts="$(date +%s)"
nonce="$(uuidgen)"
body='{"event":"payment_received","paymentId":"pay_123456","amount":"100.00","currency":"SEK","payer":{"phone":"46701234567"},"metadata":{"orderId":"order_987"}}'

# 2) Beräkna kanonisk sträng och Base64-signatur (använder SWISH_WEBHOOK_SECRET)
canonical="$(printf "%s\n%s\n%s" "$ts" "$nonce" "$body")"
sig="$(printf "%s" "$canonical" | openssl dgst -sha256 -hmac "${SWISH_WEBHOOK_SECRET:-dev_secret}" -binary | openssl base64)"

# 3) Skicka
curl -v -X POST "http://localhost:5000/webhook/swish" \
  -H "Content-Type: application/json" \
  -H "X-Swish-Timestamp: $ts" \
  -H "X-Swish-Nonce: $nonce" \
  -H "X-Swish-Signature: $sig" \
  --data-raw "$body"
```

✅ **Förväntat (HTTP 200)**
```json
{"received": true}
```

❌ **Förväntat vid replay (HTTP 409)**
```json
{"reason": "replay upptäckt (nonce sedd tidigare)"}
```

> I produktion: sätt `SWISH_REDIS` (aliasen `REDIS_URL` och `SWISH_REDIS_CONN` accepteras). Utan Redis används en in-memory-store (bra för lokal utveckling).

---

## 🧩 API-översikt (Signaturer & Modeller)

> Typerna nedan illustrerar den förväntade ytan. Namn/namespaces ska matcha biblioteket du refererar till.

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

## ⏱️ Felscenarier & Retry-policy

SDK:t gör retry internt i `SwishClient`.

- **Timeout:** 30 sekunder (HttpClient timeout som standard)
- **Retry:** upp till 3 försök med exponentiell backoff + jitter på:
  - `408`
  - `429`
  - `5xx`
  - `HttpRequestException`
  - `TaskCanceledException` (timeout)

Retry är deterministisk och gäller per operation.
Idempotency-Key genereras en gång per operation och återanvänds för alla retry-försök.

Om du registrerar en egen `HttpClient`, se till att du inte lägger till ett extra retry-lager om det inte är avsiktligt.

Vanliga svar:
- **400 Bad Request** → valideringsfel (kontrollera obligatoriska fält).
- **401 Unauthorized** → ogiltig `SWISH_API_KEY`/`SWISH_SECRET` eller saknade headers.
- **429 Too Many Requests** → hanteras av den interna retry-mekanismen.
- **5xx** → transient; hanteras av den interna retry-mekanismen.

---

## ❓ FAQ
- **401 i tester** — Kontrollera `SWISH_API_KEY`/`SWISH_SECRET` och se till att din klocka är synkroniserad.
- **Replay nekas alltid** — Byt `nonce` mellan anrop och rensa in-memory/Redis. Kontrollera `SWISH_REDIS`.
- **mTLS-fel i produktion** — Verifiera `SWISH_PFX_PATH` + `SWISH_PFX_PASSWORD` och certifikatkedjan. Om `RequireMtls = true` (standard) och inget certifikat kan hittas kastar SDK:t `SwishConfigurationException`.
- **Startup-fel** — SDK:t tillämpar en strikt maxgräns på 15 minuter för både klockdiff (`AllowedClockSkew`) och meddelandeålder (`MaxMessageAge`) vid validering under applikationsstart av säkerhetsskäl.

---

## 🏛️ Produktionsbaslinje

Nuvarande `main`-gren är den formella produktionsbaslinjen för NordAPI.Swish.

Tidigare utvecklingshistorik finns arkiverad separat för spårbarhet, medan aktiv utveckling och supportade releaser fortsätter från nuvarande baslinje. Denna repo-struktur är avsiktlig och stödjer en tydligare produktionsinriktad release- och förtroendeposition.

---

## 💬 Få hjälp
- 💬 **Frågor / feedback**: Använd [GitHub Discussions](https://github.com/NordAPI/NordAPI.Swish/discussions).
- 🐛 **Buggar / önskemål**: Öppna ett [GitHub Issue](https://github.com/NordAPI/NordAPI.Swish/issues).
- 🔒 **Säkerhetsfrågor**: Mejla [security@nordapi.com](mailto:security@nordapi.com).
- **Compliance**: Se [Security & Compliance Notes](https://github.com/NordAPI/NordAPI.Swish/blob/main/docs/compliance.md).

---

## 🚨 Rapportera säkerhetsbrister
Om du hittar en säkerhetsbrist, vänligen rapportera den privat till `security@nordapi.com`. Använd **inte** GitHub Issues för säkerhetsrelaterade ärenden.

---

## 🛠️ Bidra
1. Skapa en feature-branch från `main`.
2. Verifiera lokalt: `dotnet build`, `dotnet test` och röktest för webhooks.
3. Säkerställ att README-exemplen kompilerar.
4. Öppna PR; CI måste gå igenom.

---

## 📦 Release & Versionering
- **SemVer**: `MAJOR.MINOR.PATCH`.
- **NuGet**: Filen `PackageReadmeFile` visas på NuGet.org.
- Installera specifik version: `dotnet add package NordAPI.Swish --version 1.1.6`.

---

## 📜 Licens
Detta projekt är licensierat under **MIT-licensen**. Säkerhetskontakt: `security@nordapi.com`.

---

_Senast uppdaterad: Mars 2026_
