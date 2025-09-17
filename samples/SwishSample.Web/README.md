# SwishSample.Web – Lokal Webhook-Test

Detta är en minimal ASP.NET Core-app för att testa webhook-endpointen i **NordAPI.Swish SDK**.

## Funktioner

- Swish-klient registrerad via Dependency Injection (mockad i dev)
- Webhook-endpoint `/webhook/swish` med verifiering:
  - Timestamp-validering (±5 min)
  - HMAC-SHA256 signaturkontroll
  - Replay-skydd med nonce-store
- Extra endpoints för hälsa:
  - `/health`
  - `/ping`
  - `/di-check`

---

## Systemkrav

- .NET 8 SDK
- PowerShell 5.1 eller senare
- `curl` (finns i `C:\Windows\System32`)

---

## Miljövariabler för lokal utveckling

Ställ in följande variabler innan du kör:

```powershell
$env:ASPNETCORE_ENVIRONMENT   = "Development"
$env:SWISH_WEBHOOK_SECRET     = "dev_secret"
$env:SWISH_DEBUG              = "1"
$env:SWISH_ALLOW_OLD_TS       = "1"
$env:SWISH_REQUIRE_NONCE      = "0"
$env:SWISH_NONCE_TTL_SECONDS  = "600"
```
---

## Starta servern
```powershell
dotnet watch --project samples/SwishSample.Web run
```

---

## Skicka test-webhook i PowerShell
# Parametrar
```powershell
$secret   = "dev_secret"
$bodyJson = '{"id":"test-1","amount":100}'
$ts       = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$nonce    = [guid]::NewGuid().ToString("N")
```

# Canonical string: <ts>\n<nonce>\n<body>
```powershell
$message = "{0}`n{1}`n{2}" -f $ts, $nonce, $bodyJson
```

# HMAC-SHA256
```powershell
$key    = [Text.Encoding]::UTF8.GetBytes($secret)
$hmac   = [System.Security.Cryptography.HMACSHA256]::new($key)
$sigB64 = [Convert]::ToBase64String($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($message)))
```

# Skicka
```powershell
$curl = "$env:SystemRoot\System32\curl.exe"
$uri  = "http://localhost:5287/webhook/swish"

& $curl -i -X POST $uri `
  -H "Content-Type: application/json" `
  -H "X-Swish-Timestamp: $ts" `
  -H "X-Swish-Nonce: $nonce" `
  -H "X-Swish-Signature: $sigB64" `
  -d $bodyJson
  ```

# Skicka samma igen => ska få 401 replay-detected
```powershell
& $curl -i -X POST $uri `
  -H "Content-Type: application/json" `
  -H "X-Swish-Timestamp: $ts" `
  -H "X-Swish-Nonce: $nonce" `
  -H "X-Swish-Signature: $sigB64" `
  -d $bodyJson
  ```

---

## QuickStart
```powershell
git clone https://github.com/NordAPI/NordAPI.SwishSdk.git
cd NordAPI.SwishSdk
```


# Ställ in miljövariabler
```powershell
$env:ASPNETCORE_ENVIRONMENT   = "Development"
$env:SWISH_WEBHOOK_SECRET     = "dev_secret"
$env:SWISH_DEBUG              = "1"
$env:SWISH_ALLOW_OLD_TS       = "1"
$env:SWISH_REQUIRE_NONCE      = "0"
$env:SWISH_NONCE_TTL_SECONDS  = "600"
```
# Starta servern
```powershell
dotnet watch --project samples/SwishSample.Web run
```

---
