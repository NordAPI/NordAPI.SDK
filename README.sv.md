# NordAPI.Swish SDK (MVP)

[![Build](https://github.com/NordAPI/NordAPI.SwishSdk/actions/workflows/ci.yml/badge.svg)](https://github.com/NordAPI/NordAPI.SwishSdk/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/badge/NuGet-Unlisted-blue)](https://www.nuget.org/packages/NordAPI.Swish)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)

> 🇬🇧 English version: [README.md](../../README.md)  
> ✅ Se även: [Integration Checklist](../../docs/integration-checklist.md)

Ett lättviktigt och säkert .NET SDK för att integrera **Swish-betalningar och återköp** i test- och utvecklingsmiljöer.  
Stöd för HMAC-autentisering, mTLS och hastighetsbegränsning ingår som standard.

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

Med detta SDK får du en färdig Swish-klient på bara några minuter:

- **HttpClientFactory** med retry och rate limiting  
- **HMAC-signering** inbyggt  
- **mTLS (valfritt)** via miljövariabler — strikt kedja i Release; avslappnad endast i Debug  
- **Webhook-verifiering** med replay-skydd (nonce-store)

### 1) Installera / referera

Installera från NuGet:

```powershell
dotnet add package NordAPI.Swish
```

Eller lägg till en projektreferens (lokalt under utveckling):

```xml
<ItemGroup>
  <ProjectReference Include="..\src\NordAPI.Swish\NordAPI.Swish.csproj" />
</ItemGroup>
```

### 2) Registrera klienten i *Program.cs*

```csharp
using NordAPI.Swish;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(
        Environment.GetEnvironmentVariable("SWISH_BASE_URL")
        ?? "https://example.invalid");

    opts.ApiKey = Environment.GetEnvironmentVariable("SWISH_API_KEY")
                  ?? throw new InvalidOperationException("Saknar SWISH_API_KEY");

    opts.Secret = Environment.GetEnvironmentVariable("SWISH_SECRET")
                  ?? throw new InvalidOperationException("Saknar SWISH_SECRET");
});

var app = builder.Build();

app.MapGet("/ping", async (ISwishClient swish) =>
{
    var result = await swish.PingAsync();
    return Results.Ok(result);
});

app.Run();
```

### 3) Använd i din kod

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
        var create = new CreatePaymentRequest(100.00m, "SEK", "46701234567", "Testköp");
        var payment = await _swish.CreatePaymentAsync(create);
        return Ok(payment);
    }
}
```

---


## 🔐 mTLS via miljövariabler (valfritt)

Aktivera mutual TLS med klientcertifikat (PFX):

- `SWISH_PFX_PATH` — sökväg till `.pfx`  
- `SWISH_PFX_PASSWORD` — lösenord till certifikatet  

**Beteende:**
- Inget certifikat → fallback utan mTLS.  
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

Starta sample-appen med hemlighet (port 5000):

```powershell
$env:SWISH_WEBHOOK_SECRET = "dev_secret"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

Kör röktest i ett nytt fönster:

```powershell
.\scripts\smoke-webhook.ps1 -Secret dev_secret -Url http://localhost:5000/webhook/swish
```

### ✅ Förväntat svar (Success)
```json
{"received": true}
```

### ❌ Förväntat svar vid replay (Error)
```json
{"reason": "replay upptäckt (nonce sedd tidigare)"}
```

- I produktion: sätt `SWISH_REDIS` (sample accepterar även aliasen `REDIS_URL` och `SWISH_REDIS_CONN`).  
  Utan Redis används in-memory-store (bra för lokal utveckling).

---

## 🌐 Vanliga miljövariabler

| Variabel             | Syfte                                      | Exempel                      |
|----------------------|--------------------------------------------|------------------------------|
| SWISH_BASE_URL       | Bas-URL till Swish-API                     | https://example.invalid      |
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

- **404 / Connection refused:** Kontrollera att appen lyssnar på rätt URL (`--urls`) och port.  
- **mTLS-fel:** Kontrollera `SWISH_PFX_PATH` + `SWISH_PFX_PASSWORD` och att certifikatet är giltigt.  
- **Replay nekas alltid:** Rensa in-memory/Redis nonce-store eller byt nonce vid test.

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
services.AddSwishHttpClient(); // registrerar "Swish" (timeout + retry + mTLS om miljövariabler finns)
```

**Utöka eller ersätt:**
```csharp
services.AddSwishHttpClient();
services.AddHttpClient("Swish")
        .AddHttpMessageHandler(_ => new MyCustomHandler()); // ligger utanför SDK:s retry-pipeline
```

**Avaktivera:**
- Anropa inte `AddSwishHttpClient()` (då används standardpipelinen utan retry och timeout).  
- Eller registrera om `"Swish"` manuellt för att ersätta eller utöka handlers och inställningar.

---

## 🛡️ Security Disclosure

Om du hittar ett säkerhetsproblem, rapportera det privat via e-post till `security@nordapi.se`.  
Använd **inte** GitHub Issues för säkerhetsärenden.

---

## 📦 Licens

Detta projekt är licensierat under **MIT-licensen**.

---

_Senast uppdaterad: Oktober 2025_
















