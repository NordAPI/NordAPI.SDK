# SwishSample.Web — Local webhook test

This sample is a minimal ASP.NET Core app for testing the Swish webhook endpoint and verifier in **NordAPI.Swish**.

## Features

- Webhook endpoint: `POST /webhook/swish`
- Verification:
  - Timestamp validation (recommended window: ±5 minutes)
  - HMAC-SHA256 signature validation (Base64)
  - Replay protection with a nonce store (nonce reuse is rejected)
- Health endpoints:
  - `GET /health`
  - `GET /di-check`

---

## System requirements

- .NET 8 SDK
- PowerShell 5.1 or later
- `curl` (available at `C:\Windows\System32\curl.exe`)

---

## Environment variables for local development

Recommended defaults:

```powershell
$env:ASPNETCORE_ENVIRONMENT  = "Development"
$env:SWISH_WEBHOOK_SECRET    = "dev_secret"
$env:SWISH_DEBUG             = "1"
$env:SWISH_REQUIRE_NONCE     = "1"
$env:SWISH_NONCE_TTL_SECONDS = "600"

# Dev-only toggle (keep OFF unless you are troubleshooting)
$env:SWISH_ALLOW_OLD_TS      = "0"
```

🔒 **Security note:** `SWISH_WEBHOOK_SECRET="dev_secret"` is for local development only.
In test/production, set a real secret value via environment variables or a secret store (e.g., Azure Key Vault). Never hardcode or commit secrets.

---

## Start the server (deterministic URL)

```powershell
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

(Optional) Auto-reload:

```powershell
dotnet watch --project .\samples\SwishSample.Web\SwishSample.Web.csproj run -- --urls http://localhost:5000
```

---

## Smoke test (recommended)

In a second terminal:

```powershell
.\scripts\smoke-webhook.ps1 -Secret dev_secret -Url http://localhost:5000/webhook/swish
```

Expected:
- ✅ **HTTP 200** with `{"received": true}`
- ✅ Sending the exact same request twice should be rejected as replay (non-200; typically 409 or 401 depending on configuration)

---

## Send a signed test webhook (PowerShell)

This sample verifies the signature over the canonical string:

`"<timestamp>\n<nonce>\n<body>"`

Where:
- `<timestamp>` is a Unix timestamp in **seconds**
- `<nonce>` is a unique value per request (GUID recommended)
- `<body>` is the exact JSON payload (sign the exact UTF-8 bytes)

### 1) Parameters

```powershell
$secret   = "dev_secret"
$bodyJson = '{"id":"test-1","amount":100}'
$ts       = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$nonce    = [guid]::NewGuid().ToString("N")
```

### 2) Canonical string (exact)

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

### 4) Send

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

### Replay test

Re-run the exact same curl command again (same `$ts`, `$nonce`, `$sigB64`). It should be rejected as a replay.

---

## Webhook behavior

When the webhook receives a valid payload, it responds with:

```json
{ "received": true }
```

On replay/signature/header errors, the endpoint returns a non-200 response.

---

## QuickStart

```powershell
git clone https://github.com/NordAPI/NordAPI.SwishSdk.git
cd NordAPI.SwishSdk
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

---

## Advanced: Named HttpClient with mTLS (optional)

If you enable the named client pipeline, the sample can register a named `HttpClient` (commonly `"Swish"`) that uses the SDK’s mTLS handler.

- Enable named client pipeline:
  - `SWISH_USE_NAMED_CLIENT=1`
- Provide a client certificate:
  - `SWISH_PFX_PATH`
  - `SWISH_PFX_PASSWORD`
  - Note: By default, the SDK requires mTLS unless you set `RequireMtls = false` in `SwishOptions` (test/mock only).

🔒 **Security note:** Never commit certificates/keys. Use environment variables or a secret store.
In the SDK, relaxed certificate chain validation is allowed only in **DEBUG** builds; **Release** should remain strict.

Example (PowerShell):

```powershell
$env:SWISH_USE_NAMED_CLIENT = "1"
$env:SWISH_PFX_PATH         = "C:\path\client.pfx"
$env:SWISH_PFX_PASSWORD     = "secret"

dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

---

## Advanced: BaseAddress selection (optional)

If configured, the sample can select the Swish base address from environment variables:

1. `SWISH_BASE_URL` (absolute override, if set)
2. `SWISH_ENV=TEST|PROD`:
   - `SWISH_BASE_URL_TEST` when `SWISH_ENV=TEST`
   - `SWISH_BASE_URL_PROD` when `SWISH_ENV=PROD`
3. Fallback: `https://example.invalid`

Example (PowerShell):

```powershell
# TEST
$env:SWISH_ENV           = "TEST"
$env:SWISH_BASE_URL_TEST = "https://your-test-url"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000

# PROD
$env:SWISH_ENV           = "PROD"
$env:SWISH_BASE_URL_PROD = "https://your-prod-url"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000

# Absolute override
$env:SWISH_BASE_URL = "https://override.example"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj --urls http://localhost:5000
```

---

## Troubleshooting

| Problem | Likely cause | Fix |
|---|---|---|
| `401 Unauthorized` | Canonical string mismatch, wrong secret, or body differs | Ensure canonical is exactly `"<ts>\n<nonce>\n<body>"` and sign the exact UTF-8 body bytes |
| Replay is always rejected | You reused `$nonce` | Generate a fresh GUID for `$nonce` per request |
| Missing headers | Required headers not sent | Send `X-Swish-Timestamp`, `X-Swish-Nonce`, `X-Swish-Signature` |
| Server won’t start | Port already in use | Stop any previous `dotnet run` instance or change `--urls` |

---

## See also

- Root README: `../../README.md`
- Integration checklist: https://nordapi.net/integration-checklist/
- Package README: `../../src/NordAPI.Swish/README.md`
