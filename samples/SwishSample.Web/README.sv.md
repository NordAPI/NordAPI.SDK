# SwishSample.Web — Lokal webhook-test

Detta sample är en minimal ASP.NET Core-app för att testa Swish webhook-endpointen och verifieraren i **NordAPI.Swish**.

## Funktioner

- Webhook-endpoint: `POST /webhook/swish`
- Verifiering:
  - Timestamp-validering (rekommenderat fönster: ±5 minuter)
  - HMAC-SHA256 signaturkontroll (Base64)
  - Replay-skydd med nonce-store (återanvänd nonce nekas)
- Hälsa:
  - `GET /health`
  - `GET /di-check`

---

## Systemkrav

- .NET 8 SDK
- PowerShell 5.1 eller senare
- `curl` (finns i `C:\Windows\System32\curl.exe`)

---

## Miljövariabler för lokal utveckling

Rekommenderade defaults:

```powershell
$env:ASPNETCORE_ENVIRONMENT  = "Development"
$env:SWISH_WEBHOOK_SECRET    = "dev_secret"
$env:SWISH_DEBUG             = "1"
$env:SWISH_REQUIRE_NONCE     = "1"
$env:SWISH_NONCE_TTL_SECONDS = "600"

# Endast dev (håll AV om du inte felsöker)
$env:SWISH_ALLOW_OLD_TS      = "0"
```

**Säkerhetsnotis:** `SWISH_WEBHOOK_SECRET="dev_secret"` är endast för lokal utveckling.
I test/produktion: sätt en riktig hemlighet via env vars eller en secret store (t.ex. Azure Key Vault). Hårdkoda/committa aldrig hemligheter.

---

## Starta servern (deterministisk URL)

```powershell
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

(Valfritt) Auto-reload:

```powershell
dotnet watch --project .\samples\SwishSample.Web\SwishSample.Web.csproj run -- --urls http://localhost:5000
```

---

## Röktest (rekommenderat)

I en andra terminal:

```powershell
.\scripts\smoke-webhook.ps1 -Secret dev_secret -Url http://localhost:5000/webhook/swish
```

Förväntat:
- HTTP 200 med `{"received": true}`
- Om du skickar exakt samma request två gånger ska den nekas som replay (non-200; vanligtvis 409 eller 401 beroende på konfiguration)

---

## Skicka en signerad test-webhook (PowerShell)

Detta sample verifierar signaturen över den kanoniska strängen:

`"<timestamp>\n<nonce>\n<body>"`

Där:
- `<timestamp>` är Unix-tid i **sekunder**
- `<nonce>` är unikt per request (GUID rekommenderas)
- `<body>` är exakt JSON-payload (signera exakta UTF-8-byten)

### 1) Parametrar

```powershell
$secret   = "dev_secret"
$bodyJson = '{"id":"test-1","amount":100}'
$ts       = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$nonce    = [guid]::NewGuid().ToString("N")
```

### 2) Kanonisk sträng (exakt)

```powershell
$canonical = "{0}`n{1}`n{2}" -f $ts, $nonce, $bodyJson
```

### 3) HMAC-SHA256 (Base64)

```powershell
$key    = [Text.Encoding]::UTF8.GetBytes($secret)
$hmac   = [System.Security.Cryptography.HMACSHA256]::new($key)
$sigB64 = [Convert]::ToBase64String(
  $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical))
)
```

### 4) Skicka

```powershell
$curl = "$env:SystemRoot\System32\curl.exe"
$uri  = "http://localhost:5000/webhook/swish"

& $curl -i -X POST $uri `
  -H "Content-Type: application/json; charset=utf-8" `
  -H "X-Swish-Timestamp: $ts" `
  -H "X-Swish-Nonce: $nonce" `
  -H "X-Swish-Signature: $sigB64" `
  --data-raw $bodyJson
```

### Replay-test

Kör exakt samma curl-kommando igen (samma `$ts`, `$nonce`, `$sigB64`). Då ska den nekas som replay.

---

## Webhook-beteende

När webhooken tar emot en giltig payload svarar den med:

```json
{ "received": true }
```

Vid replay/signatur/header-fel returnerar endpointen en non-200 respons.

---

## QuickStart

```powershell
git clone https://github.com/NordAPI/NordAPI.Swish.git
cd NordAPI.Swish
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

---

## Avancerat: Named HttpClient med mTLS (valfritt)

Om du aktiverar named client-pipelinen kan samplet registrera en named `HttpClient` (vanligtvis `"Swish"`) som använder SDK:ns mTLS-handler.

- Aktivera named client pipeline:
  - `SWISH_USE_NAMED_CLIENT=1`
- Ange klientcertifikat:
  - `SWISH_PFX_PATH`
  - `SWISH_PFX_PASSWORD`
  - Notis: Som standard kräver SDK:t mTLS om du inte sätter `RequireMtls = false` i `SwishOptions` (endast test/mock).

**Säkerhetsnotis:** Commita aldrig certifikat/nycklar. Använd env vars eller en secret store.
I SDK:n tillåts relaxed chain endast i **DEBUG**; i **Release** ska validering vara strikt.

Exempel (PowerShell):

```powershell
$env:SWISH_USE_NAMED_CLIENT = "1"
$env:SWISH_PFX_PATH         = "C:\path\client.pfx"
$env:SWISH_PFX_PASSWORD     = "secret"

dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

---

## Avancerat: Val av BaseAddress (valfritt)

Om det är konfigurerat kan samplet välja Swish base address från miljövariabler:

1. `SWISH_BASE_URL` (absolut override, om satt)
2. `SWISH_ENV=TEST|PROD`:
   - `SWISH_BASE_URL_TEST` när `SWISH_ENV=TEST`
   - `SWISH_BASE_URL_PROD` när `SWISH_ENV=PROD`
3. Fallback: `https://example.invalid`

Exempel (PowerShell):

```powershell
# TEST
$env:SWISH_ENV           = "TEST"
$env:SWISH_BASE_URL_TEST = "https://your-test-url"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000

# PROD
$env:SWISH_ENV           = "PROD"
$env:SWISH_BASE_URL_PROD = "https://your-prod-url"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000

# Absolut override
$env:SWISH_BASE_URL = "https://override.example"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

---

## Felsökning

| Problem | Trolig orsak | Åtgärd |
|---|---|---|
| `401 Unauthorized` | Kanonisk sträng mismatch, fel hemlighet eller annan body | Säkerställ exakt `"<ts>\n<nonce>\n<body>"` och signera exakta UTF-8-byten av bodyn |
| Replay nekas alltid | Du återanvände `$nonce` | Skapa ny GUID för `$nonce` per request |
| Saknade headers | Obligatoriska headers saknas | Skicka `X-Swish-Timestamp`, `X-Swish-Nonce`, `X-Swish-Signature` |
| Servern startar inte | Porten används redan | Stoppa tidigare `dotnet run` eller byt `--urls` |

---

## Se även

- Root README: `../../README.md`
- Integration checklist: https://nordapi.net/integration-checklist/
- Paket-README: `../../src/NordAPI.Swish/README.md`
