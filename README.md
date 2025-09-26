
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

```csharp
using NordAPI.Swish;

// Skapa HttpClient med HMAC, RateLimit och mTLS
var http = SwishClient.CreateHttpClient(
    baseAddress: new Uri("https://example.test"),
    apiKey: Environment.GetEnvironmentVariable("SWISH_API_KEY") ?? "dev-key",
    secret: Environment.GetEnvironmentVariable("SWISH_SECRET") ?? "dev-secret",
    innerHandler: null,
    certOptions: new SwishCertificateOptions {
        PfxPath = Environment.GetEnvironmentVariable("SWISH_PFX_PATH"),
        PfxPassword = Environment.GetEnvironmentVariable("SWISH_PFX_PASSWORD")
    },
    allowInvalidChainForDev: true // Endast för lokal utveckling
);

var swish = new SwishClient(http);

// Skapa betalning
var create = new CreatePaymentRequest(100.00m, "SEK", "46701234567", "Testköp");
var payment = await swish.CreatePaymentAsync(create);

// Kontrollera status
var status = await swish.GetPaymentStatusAsync(payment.Id);

// Återköp
var refund = await swish.CreateRefundAsync(new CreateRefundRequest(payment.Id, 100.00m, "SEK", "Retur"));
var refundStatus = await swish.GetRefundStatusAsync(refund.Id);
```

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




