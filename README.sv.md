
# NordAPI.Swish SDK (MVP)

Ett lättviktigt och säkert .NET SDK för att integrera Swish-betalningar och återköp i test- och utvecklingsmiljöer.  
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

## ⚡ Snabbstart

## Kom igång på 5 minuter (ASP.NET Core)

Med detta SDK får du en färdig Swish-klient på bara några minuter:

- **HttpClientFactory** med retry och rate limiting
- **HMAC-signering** inbyggt
- **mTLS (valfritt)** via miljövariabler — strikt kedja i Release; avslappnad endast i Debug
- **Webhook-verifiering** med replay-skydd (nonce-store)

### 1) Installera / referera

Lägg till en projektreferens (lokalt under utveckling):

```xml
<ItemGroup>
  <ProjectReference Include="..\src\NordAPI.Swish\NordAPI.Swish.csproj" />
</ItemGroup>
```

### 2) Registrera klienten i program.cs

```csharp
using NordAPI.Swish;

var builder = WebApplication.CreateBuilder(args);

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
```

### 3) Använd i din kod

```csharp
public class PaymentsController
{
    private readonly ISwishClient _swish;

    public PaymentsController(ISwishClient swish)
    {
        _swish = swish;
    }

    [HttpPost("/pay")]
    public async Task<IActionResult> Pay()
    {
        var create = new CreatePaymentRequest(100.00m, "SEK", "46701234567", "Testköp");
        var payment = await _swish.CreatePaymentAsync(create);

        return Results.Ok(payment);
    }
}
```

### 4) mTLS via miljövariabler (valfritt)

## Aktivera mutual TLS med en klientcertfil:

- SWISH_PFX_PATH – sökväg till .pfx

- SWISH_PFX_PASS – lösenord

Beteende:

- Inget cert → fallback utan mTLS.

- Debug = avslappnad servercert-validering (endast lokalt).

- Release = strikt certkedja (ingen “allow invalid chain”).

## Exempel(PowerShell):

```powershell
$env:SWISH_PFX_PATH = "C:\certs\swish-client.pfx"
$env:SWISH_PFX_PASS = "hemligt-lösenord"
```
Produktion: lagra cert/secret i KeyVault/Secret Manager - aldrig i repo.

### 5) Starta & smoketesta

Starta sample-appen (port 5000):
```powershell
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

I ett nytt PowerShell-fönster, kör smoketest:
```powershell
.\scripts\smoke-webhook.ps1 -Secret dev_secret -Url http://localhost:5000/webhook/swish
```

Förväntat svar:
```json
{"received": true}
```

### 6) Testa replay-skydd

Kör samma smoketest två gånger direkt efter varandra.
Det andra anropet ska nekas (replay detekteras)
- Använder du Redis för nonce-store, sätt REDIS_URL/SWISH_REDIS_CONN. Utan Redis används in-memory-store (bra för lokal dev).

### 7) Vanliga miljövariabler

| Variabel           | Syfte                                      | Exempel                    |
|--------------------|--------------------------------------------|----------------------------|
| SWISH_BASE_URL     | Bas-URL till Swish API                     | https://example.invalid    |
| SWISH_API_KEY      | API-nyckel för HMAC                        | dev-key                    |
| SWISH_SECRET       | Hemlighet för HMAC                         | dev-secret                 |
| SWISH_PFX_PATH     | Sökväg till klientcert (.pfx)              | C:\certs\swish-client.pfx  |
| SWISH_PFX_PASS     | Lösenord för .pfx                          | ••••                       |
| SWISH_DEBUG        | Verbosare loggning / lättare verifiering   | 1                          |
| SWISH_ALLOW_OLD_TS | Tillåt äldre timestamps vid verifiering    | 1 (endast dev)             |

### 8) Felsökning (kort)

- 404/connection refused: kontrollera att appen lyssnar på rätt URL (--urls) och port.
- mTLS fel: validera SWISH_PFX_PATH + SWISH_PFX_PASS och att certkedjan är giltig (Release är strikt).
- Replay nekar alltid: rensa in-memory/Redis nonce-store eller byt nonce vid test.

---

## 🌐 ASP.NET Core-integration

```csharp
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(Environment.GetEnvironmentVariable("SWISH_BASE_URL")
        ?? throw new InvalidOperationException("Saknar SWISH_BASE_URL"));
    opts.ApiKey = Environment.GetEnvironmentVariable("SWISH_API_KEY")
        ?? throw new InvalidOperationException("Saknar SWISH_API_KEY"));
    opts.Secret = Environment.GetEnvironmentVariable("SWISH_SECRET")
        ?? throw new InvalidOperationException("Saknar SWISH_SECRET"));
});

var app = builder.Build();

app.MapGet("/ping", async (ISwishClient swish) => await swish.PingAsync());

app.Run();
```

---

## 🔧 Miljövariabler

| Variabel             | Beskrivning                         |
|----------------------|-------------------------------------|
| `SWISH_BASE_URL`     | Bas-URL för Swish API               |
| `SWISH_API_KEY`      | API-nyckel för HMAC-autentisering   |
| `SWISH_SECRET`       | Delad nyckel för HMAC               |
| `SWISH_PFX_PATH`     | Sökväg till klientcertifikat (.pfx) |
| `SWISH_PFX_PASSWORD` | Lösenord för certifikatet           |

> Hårdkoda aldrig hemligheter. Använd miljövariabler, Secret Manager eller GitHub Actions Secrets.

---

## 🧪 Exempelprojekt

Se `samples/SwishSample.Web` för ett körbart exempel:

- `GET /health` → OK
- `GET /di-check` → Verifierar DI-konfiguration
- `GET /ping` → Mockat svar (ingen riktig HTTP)

Byt ut mot riktiga miljövariabler och aktivera `PingAsync()` för integrationstester.

---

### 🔧 Röktest av webhook (endast för lokal utveckling)

SDK:t innehåller ett enkelt röktest för att verifiera att webhook-signering fungerar lokalt.

1. Starta sample-servern med hemlighet:
   ```powershell
   $env:SWISH_WEBHOOK_SECRET = "dev_secret"
   $env:SWISH_DEBUG = "1"
   dotnet watch run --project .\samples\SwishSample.Web\SwishSample.Web.csproj
   ```

2.  Kör röktestet
    ```powershell
    .\scripts\smoke-webhook.ps1 -Secret dev_secret -Replay
    ```

3. Förväntat resultat:

Första request → {"received":true} (kan visas som True i PowerShell).

Andra request (replay) → 401 med {"reason":"replay upptäckt (nonce sedd tidigare)"}.

(Obs: Detta är ett utvecklarverktyg. Riktiga Swish-callbackar skickar inte dessa HMAC-headers. I produktion används en separat verifieringsmekanism.) 


---

## 🔐 mTLS-stöd

 Om din miljö kräver klientcertifikat:

```csharp
using System.Security.Cryptography.X509Certificates;

var cert = new X509Certificate2("sökväg/till/certifikat.pfx", "lösenord");
builder.Services.AddSwishClient(opts => { /* … */ }, clientCertificate: cert);
```


---

## Dev quick commands


# Build + test
```powershell
dotnet build
dotnet test
```

# Run sample (development)
```powershell
dotnet watch --project samples/SwishSample.Web run
```

---

## HTTP timeout & retry (named client **"Swish"**)

The SDK provides an **opt-in** named HttpClient `"Swish"` with:
- **Timeout:** `30s` (`HttpClient.Timeout`)
- **Retry policy:** up to **3** retries with exponential backoff + jitter  
  Retries on: **408**, **429**, **5xx**, **HttpRequestException**, **TaskCanceledException** (timeout)

**When it applies**
- Register the pipeline via:
  - `services.AddSwishHttpClient()` (SDK extension), or
  - In the sample: set `SWISH_USE_NAMED_CLIENT=1` (which calls the extension).
- If you do **not** call `AddSwishHttpClient()`, you’ll get the default pipeline (no custom retry, default .NET timeout).

**mTLS (optional)**
- Add a client cert when env vars are present:
  - `SWISH_PFX_PATH` **or** `SWISH_PFX_BASE64`  
  - and `SWISH_PFX_PASSWORD` **or** `SWISH_PFX_PASS`
- DEBUG allows relaxed chain (dev only). Release is strict.

**Override / extend**
- You can add more handlers around the named client (outermost are added last):
```csharp
services.AddSwishHttpClient(); // registers "Swish" with timeout+retry(+mTLS if env)
services.AddHttpClient("Swish")
        .AddHttpMessageHandler(_ => new MyCustomHandler()); // sits outside SDK retry
```
**Disable**

- Don’t call AddSwishHttpClient() (the SDK will use the plain default pipeline).

- Or re-register "Swish" yourself to replace/override handlers and timeout.

## Quick check (sample)

```powershell
$env:SWISH_USE_NAMED_CLIENT="1"
# optional mTLS
$env:SWISH_PFX_PATH="C:\path\client.pfx"
$env:SWISH_PFX_PASSWORD="secret"

dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj
```

---

## mTLS via miljövariabler (för SDK)

SDK:t kan ladda klientcertifikat för mTLS om miljövariablerna är satta:

- `SWISH_PFX_PATH` → sökväg till PFX-filen
- `SWISH_PFX_PASS` → lösenord till PFX-filen

Om dessa inte är satta används fallback utan mTLS.  
I DEBUG tillåts enklare utvecklarvalidering, i RELEASE krävs en strikt certkedja.


---





