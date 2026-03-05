# NordAPI.Swish SDK

Officiellt NordAPI-bibliotek för Swish-betalningar.

[![Build](https://github.com/NordAPI/NordAPI.Swish/actions/workflows/ci.yml/badge.svg)](https://github.com/NordAPI/NordAPI.Swish/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/NordAPI.Swish.svg?label=NuGet)](https://www.nuget.org/packages/NordAPI.Swish)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)
![.NET](https://img.shields.io/badge/.NET-8%2B-blueviolet)

> 🇬🇧 English version: [README.md](https://github.com/NordAPI/NordAPI.Swish/blob/main/README.md)
> ✅ Se även: [Integration Checklist (web)](https://nordapi.net/integration-checklist/) • [Integration Checklist (repo)](./docs/integration-checklist.md)

Ett lättviktigt och säkert .NET SDK för att integrera **Swish-betalningar och återköp** med deterministiska, fail-closed standardval.
Inkluderar mTLS som är påslaget som standard, valfri HMAC-härdning för webhook-verifiering och hjälpfunktioner för hastighetsbegränsning.
💡 *Stöd för BankID kommer härnäst — håll utkik efter paketet `NordAPI.BankID`.*

**Kräver .NET 8+ (LTS-kompatibel)**

> **Produktionsnotis**
> Minneslagring av nonce är endast för **utvecklingsmiljö**. I produktion **måste** du använda en persistent lagring (Redis/DB).
> Ange `SWISH_REDIS` (eller `REDIS_URL` / `SWISH_REDIS_CONN`). Exempelappen stoppar start i `Production` om ingen Redis är satt.

**Licensnotis:** NordAPI är ett SDK. Du behöver egna Swish/BankID-avtal och certifikat. NordAPI tillhandahåller inte dessa.

---

## Scope

För att hålla SDK:t fokuserat och lätt att granska är gränserna medvetet smala.

### Inom scope
- **mTLS för Swish API-kommunikation**
- **Deterministisk HMAC-SHA256-signering och webhook-verifiering**
- **Spec-låst hantering av säkerhetsrelevanta protokollelement** som timestamps, nonces och canonicalization
- **Fail-closed-validering av säkerhetskänslig konfiguration** vid applikationsstart

### Utanför scope
- **Secret management** i din miljö (generering, lagring, rotation)
- **Infrastruktur-säkerhet** såsom WAF, brandväggar och IDS/IPS
- **Persistensimplementation** för nonce-lagring (till exempel Redis eller SQL)
- **Merchant onboarding, avtal och extern tillgänglighet i Swish-plattformen**

---

## 📚 Innehållsförteckning
- [🚀 Funktioner](#-funktioner)
- [⚡ Snabbstart (ASP.NET Core)](#-snabbstart-aspnet-core)
- [🔐 mTLS via miljövariabler (krävs som standard)](#-mtls-via-miljövariabler-krävs-som-standard)
- [🧪 Starta & röktesta](#-starta--röktesta)
- [🌐 Vanliga miljövariabler](#-vanliga-miljövariabler)
- [🧰 Felsökning](#-felsökning)
- [🚦 Go live-checklista (kund)](#-go-live-checklista-kund)
- [🧩 ASP.NET Core-integration](#-aspnet-core-integration-skärpt-validering)
- [🛠️ Snabba utvecklingskommandon](#️-snabba-utvecklingskommandon)
- [⏱️ HTTP-timeout & återförsök](#️-http-timeout--återförsök-namngiven-klient-swish)
- [💬 Få hjälp](#-få-hjälp)
- [🛡️ Security Disclosure](#️-security-disclosure)
- [📦 Licens](#-licens)

---

## 🚀 Funktioner
- ✅ Skapa och verifiera Swish-betalningar
- 🔁 Stöd för återköp
- 🔐 HMAC + mTLS-stöd
- 📉 Hastighetsbegränsning
- 🧪 ASP.NET Core-integration
- 🧰 Miljövariabelhantering

---

## ⚡ Snabbstart (ASP.NET Core)

Med detta SDK får du en fungerande Swish-klient på bara några minuter:

- **HttpClientFactory** för att konfigurera HTTP-pipelinen (HMAC, rate limiting, mTLS)
- **Valfri NordAPI Security Hardening (HMAC-signering)** för utgående requests (inte Swish-officiellt)
- **mTLS (krävs som standard)** via miljövariabler — strikt kedja i Release; avslappnad endast i Debug (lokalt)
- **Webhook-verifiering (valfri hardening)** med replay-skydd (nonce-store). Swish skickar inte `X-Swish-*` signatur-headers som standard.
- **Intern retry/backoff** för transienta fel (endast ett retry-lager; Idempotency-Key återanvänds per operation)

### 1) Installera / referera
```powershell
dotnet add package NordAPI.Swish
```

Eller lägg till en projektreferens:
```xml
<ItemGroup>
  <ProjectReference Include="..\src\NordAPI.Swish\NordAPI.Swish.csproj" />
</ItemGroup>
```

### 2) Registrera klienten i *Program.cs*
```csharp
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(Environment.GetEnvironmentVariable("SWISH_BASE_URL")
        ?? throw new InvalidOperationException("Saknar SWISH_BASE_URL"));
    opts.ApiKey = Environment.GetEnvironmentVariable("SWISH_API_KEY")
        ?? throw new InvalidOperationException("Saknar SWISH_API_KEY");
    opts.Secret = Environment.GetEnvironmentVariable("SWISH_SECRET")
        ?? throw new InvalidOperationException("Saknar SWISH_SECRET");
});

var app = builder.Build();

app.MapGet("/ping", async (ISwishClient swish) => await swish.PingAsync());
app.Run();

```

### 3) Använd i din kod
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
    var create = new CreatePaymentRequest(
      PayerAlias: "46701234567",
      PayeeAlias: "1231181189",
      Amount: "100.00",
      Currency: "SEK",
      Message: "Testköp",
      CallbackUrl: "https://yourdomain.test/webhook/swish"
    );

    var payment = await _swish.CreatePaymentAsync(create);
    return Ok(payment);
  }
}
```

---

## 🔐 mTLS via miljövariabler (krävs som standard)

Aktivera mTLS med klientcertifikat (PFX):

- `SWISH_PFX_PATH` — sökväg till `.pfx`
- `SWISH_PFX_PASSWORD` — lösenord till certifikatet
- legacy fallback: `SWISH_PFX_PASS` (om `SWISH_PFX_PASSWORD` inte är satt)

**Beteende:**
- Som standard kräver SDK:t mTLS.
- Om `RequireMtls = true` (standard) och inget klientcertifikat kan hittas, kastar SDK:t `SwishConfigurationException` när HTTP-handlern skapas.
- Sätt `RequireMtls = false` endast för kontrollerad lokal testning/mock där mTLS medvetet inte används.
- **Debug:** avslappnad servercert-validering (endast lokalt).
- **Release:** strikt certkedja (ingen "allow invalid chain").

**Exempel (PowerShell):**
```powershell
$env:SWISH_PFX_PATH = "C:\certs\swish-client.pfx"
$env:SWISH_PFX_PASSWORD = "hemligt-lösenord"
```

> 🔒 I produktion ska certifikat och hemligheter lagras i **Azure Key Vault** eller liknande — aldrig i repo.

---

## 🧪 Starta & röktesta

Starta sample-appen (port 5000) med webhook-hemligheten:
```powershell
$env:SWISH_WEBHOOK_SECRET = "dev_secret"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

Kör sedan i ett nytt PowerShell-fönster:
```powershell
.\scripts\smoke-webhook.ps1 -Secret dev_secret -Url http://localhost:5000/webhook/swish
```

För snabb manuell testning kan du även POST:a webhooken med **curl** (bash/macOS/Linux).
**Signatur-spec:** HMAC-SHA256 över den kanoniska strängen **`"<timestamp>\n<nonce>\n<body>"`**, med **`SWISH_WEBHOOK_SECRET`** som nyckel. Kodas som **Base64**.

### Obligatoriska headers
| Header              | Beskrivning                                     | Exempelvärde                          |
|---------------------|--------------------------------------------------|---------------------------------------|
| `X-Swish-Timestamp` | Unix-timestamp i **sekunder**                   | `1735589201`                          |
| `X-Swish-Nonce`     | Unikt ID för replay-skydd                       | `550e8400-e29b-41d4-a716-446655440000` |
| `X-Swish-Signature` | **Base64** HMAC-SHA256 av `"<ts>\n<nonce>\n<body>"` | `W9CzL8f...==`                         |

### Exempel på webhook-payload
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

### curl röktest (bash / macOS / Linux)
```bash
# 1) Förbered värden
ts="$(date +%s)"
nonce="$(uuidgen)"
body='{"event":"payment_received","paymentId":"pay_123456","amount":100.00,"currency":"SEK","payer":{"phone":"46701234567"},"metadata":{"orderId":"order_987"}}'

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

> Tips för Windows: PowerShell-användare kan köra det medföljande skriptet eller `Invoke-RestMethod`. Se till att du beräknar **Base64-HMAC** över `"<ts>\n<nonce>\n<body>"` och sätter `X-Swish-Signature` korrekt.

✅ **Förväntat (Success)**
```json
{"received": true}
```

❌ **Förväntat vid replay (Error)**
```json
{"reason": "replay upptäckt (nonce sedd tidigare)"}
```

> I produktion: sätt `SWISH_REDIS` (aliasen `REDIS_URL` och `SWISH_REDIS_CONN` accepteras). Utan Redis används en in-memory-store (bra för lokal utveckling).

---

## 🌐 Vanliga miljövariabler

| Variabel             | Syfte                                      | Exempel                      |
|----------------------|--------------------------------------------|------------------------------|
| SWISH_BASE_URL       | Bas-URL till Swish API                     | https://example.invalid      |
| SWISH_API_KEY        | API-nyckel för HMAC                        | dev-key                      |
| SWISH_SECRET         | Hemlighet för HMAC                         | dev-secret                   |
| SWISH_PFX_PATH       | Sökväg till klientcertifikat (.pfx)        | C:\certs\swish-client.pfx  |
| SWISH_PFX_PASSWORD   | Lösenord till klientcertifikat             | ••••                         |
| SWISH_WEBHOOK_SECRET | Hemlighet för webhook-HMAC                 | dev_secret                   |
| SWISH_REDIS          | Redis-anslutningssträng (nonce-store)      | localhost:6379               |
| SWISH_DEBUG          | Verbosare loggning / lättare verifiering   | 1                            |
| SWISH_ALLOW_OLD_TS   | Tillåt äldre timestamps vid verifiering    | 1 (endast dev)               |

> 💡 Hårdkoda aldrig hemligheter. Använd miljövariabler, Secret Manager eller GitHub Actions Secrets.

---

## 🧰 Felsökning

- **404 / Connection refused:** Kontrollera att appen lyssnar på rätt URL och port (`--urls`).
- **mTLS-fel:** Kontrollera `SWISH_PFX_PATH` + `SWISH_PFX_PASSWORD` och att certifikatet är giltigt.
- **Replay nekas alltid:** Rensa in-memory/Redis nonce-store eller använd en ny nonce vid test.

---

## 🚦 Go live-checklista (kund)

Använd den här checklistan innan du kör mot riktiga Swish/BankID-miljöer.

### Certifikat och hemligheter
- Använd **egna** produktionsavtal och certifikat (mTLS) från bank/leverantör.
- Commita aldrig certifikat eller hemligheter i repo.
- Lagra hemligheter i miljövariabler, en secret manager (t.ex. Azure Key Vault) eller plattformens secret store.
- Rotera hemligheter regelbundet och direkt vid misstanke om läcka.

### HTTPS och transportskydd
- Kör **endast HTTPS** för webhook-endpoints (överväg HSTS vid edge).
- Om du terminerar TLS i en reverse proxy: lås ner och lita på interna hopp.

### Webhook-verifiering (krävs för detta hardening-lager)
> Obs: Swish skickar inte dessa `X-Swish-*` headers som standard. Det här är NordAPI:s valfria webhook-hardening-mönster (du lägger det vid din edge).
- Kräv dessa headers:
  - `X-Swish-Timestamp` (Unix-tid i **sekunder**)
  - `X-Swish-Nonce`
  - `X-Swish-Signature` (Base64 HMAC-SHA256)
- Verifiera signaturen över den kanoniska strängen: `"<timestamp>\n<nonce>\n<body>"` med `SWISH_WEBHOOK_SECRET`.
- Neka requests utanför tillåtet tidsfönster (rekommendation: **±5 minuter**).
- Kör **anti-replay** med persistenta nonces (Redis/DB). Använd **inte** in-memory nonce-store i produktion.

Valfritt (distribuerat): För produktion över flera instanser, se docs/optional/redis-nonce-store.md för delat nonce-state (replay protection).

### Drift-härdning
- Stäng av alla debug-relaxations i produktion (undvik t.ex. att tillåta gamla timestamps).
- Lägg på rate limiting och strukturerad loggning (undvik PII i loggar).
- Bevaka verifieringsfel (signatur mismatch, tidsdrift, replay) och larma vid avvikelser.

---

## 🧩 ASP.NET Core-integration (skärpt validering)

```csharp
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(Environment.GetEnvironmentVariable("SWISH_BASE_URL")
        ?? throw new InvalidOperationException("Saknar SWISH_BASE_URL"));
    opts.ApiKey = Environment.GetEnvironmentVariable("SWISH_API_KEY")
        ?? throw new InvalidOperationException("Saknar SWISH_API_KEY");
    opts.Secret = Environment.GetEnvironmentVariable("SWISH_SECRET")
        ?? throw new InvalidOperationException("Saknar SWISH_SECRET");
});

var app = builder.Build();

app.MapGet("/ping", async (ISwishClient swish) => await swish.PingAsync());
app.Run();
```

---

## 🛠️ Snabba utvecklingskommandon

**Bygg & test**
```powershell
dotnet build
dotnet test
```

**Kör sample (utveckling)**
```powershell
dotnet watch --project .\samples\SwishSample.Web\SwishSample.Web.csproj run
```

---

## ⏱️ HTTP-timeout & återförsök (namngiven klient "Swish")

SDK:t tillhandahåller en **opt-in** namngiven `HttpClient` **"Swish"** med:
- **Timeout:** 30 sekunder
- **Återförsökspolicy:** upp till 3 försök med exponentiell backoff + jitter
  (på statuskoder 408, 429, 5xx, samt `HttpRequestException` och `TaskCanceledException`)

**Aktivera:**
```csharp
services.AddSwishMtlsTransport(); // registrerar "Swish" (HTTP-pipeline + mTLS om miljövariabler finns)
```

**Utöka eller ersätt:**
```csharp
services.AddSwishMtlsTransport();
services.AddHttpClient("Swish")
        .AddHttpMessageHandler(_ => new MyCustomHandler()); // ligger utanför SDK:ns HTTP-pipeline
```

**Avaktivera:**
- Anropa inte `AddSwishMtlsTransport()` om du inte vill anpassa SDK:ns HTTP-pipeline.
- Eller registrera om `"Swish"` manuellt för att ersätta eller utöka handlers och inställningar.

---

## 💬 Få hjälp

- 💬 Frågor / feedback: använd [GitHub Discussions](https://github.com/NordAPI/NordAPI.Swish/discussions) (inga secrets/PII).
- 🐛 Buggar / feature requests: öppna ett [GitHub Issue](https://github.com/NordAPI/NordAPI.Swish/issues).
- 🔒 Säkerhetsärenden: e-posta [security@nordapi.com](mailto:security@nordapi.com) (öppna inte publika issues/discussions).
- Säkerhet & compliance: se [docs/compliance.md](docs/compliance.md).

---

## 🛡️ Security Disclosure

Om du hittar ett säkerhetsproblem, rapportera det privat via e-post till `security@nordapi.com`.
Använd **inte** GitHub Issues för säkerhetsärenden.

---

## 📦 Licens

Detta projekt är licensierat under **MIT-licensen**.

---

_Senast uppdaterad: Mars 2026_
