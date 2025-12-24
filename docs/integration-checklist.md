# NordAPI Swish SDK — Integration Checklist

This is a concise checklist to get a production-ready integration with the NordAPI Swish SDK. Keep it short, verify each box, and move on.

---

## 1) Setup

- [ ] **Install the SDK**: `dotnet add package NordAPI.Swish`
- [ ] **Environment variables** (dev/test values first, replace before go-live):
  - `SWISH_BASE_URL` — e.g., sandbox URL (or your mock during development)
  - `SWISH_API_KEY`
  - `SWISH_SECRET`
- [ ] (Production) **mTLS certificate** is loaded via secure config (KeyVault/env), never from files in the repo:
  - `SWISH_PFX_PATH` — Absolute path to client certificate (.pfx/.p12)
  - `SWISH_PFX_PASSWORD` — PFX password (or legacy `SWISH_PFX_PASS`)
  - Note: `SWISH_PFX_BASE64` is not used by the SDK currently.
- [ ] Do **not** commit secrets/certificates. Keep `.gitignore` covering: `*.pfx, *.p12, *.pem, *.key, *.crt, *.cer`.

---

## 2) Webhook Configuration

Your Swish callback endpoint must verify HMAC and reject replays.

**Required headers** sent by your client/test tool:
- `X-Swish-Timestamp` — UNIX timestamp **in seconds**
- `X-Swish-Nonce` — unique nonce (GUID/128-bit random)
- `X-Swish-Signature` — `Base64(HMACSHA256(secret, canonical))`

**Canonical string** (exact newlines, no trailing spaces):
<timestamp>\n<nonce>\n<body>

Where:
- `<timestamp>` is the exact value of `X-Swish-Timestamp`
- `<nonce>` is the exact value of `X-Swish-Nonce`
- `<body>` is the **raw** JSON request body (no reformatting)

**Server verification must**:
- [ ] Parse the three headers (timestamp, nonce, signature)
- [ ] Rebuild the canonical string exactly as above
- [ ] Compute `HMACSHA256` with your `SWISH_SECRET`
- [ ] Compare with `X-Swish-Signature` (Base64)
- [ ] Enforce **timestamp skew**: ±5 minutes
- [ ] Enforce **replay protection**: nonce must be unique (see section 3)

---

## 3) Security Checklist

- [ ] **HTTPS only** (enforce TLS 1.2+; enable HSTS)
- [ ] **Timestamp tolerance**: ±5 minutes (reject outside window)
- [ ] **Replay protection**: persistent store (**Redis/DB**, not in-memory in production)
- [ ] **Rate limiting** at the edge/service level
- [ ] **Secrets management**: environment/KeyVault; rotation plan in place
- [ ] **Structured logging** without PII (mask sensitive values)
- [ ] Disable debug/dev flags in production
- [ ] mTLS configured for Swish where applicable

---

## 4) Testing

- [ ] **Local smoke test** of the webhook signer/verifier:
  - Script: `.\scripts\smoke-webhook.ps1`
  - Typical usage:
    ```
    .\scripts\smoke-webhook.ps1 -Secret dev_secret
    .\scripts\smoke-webhook.ps1 -Secret dev_secret -Replay   # expect replay to be rejected
    ```
  - Verify server logs show the three-line canonical and a **signature match**.
- [ ] **Refund flow** tested end-to-end in your app
- [ ] **Error handling** paths exercised (invalid signature, old timestamp, replayed nonce)
- [ ] (When available) **Swish sandbox** end-to-end tests pass

---

## 5) Before Go-Live

- [ ] Replace all dev values (`SWISH_BASE_URL`, `SWISH_API_KEY`, `SWISH_SECRET`)
- [ ] Install production mTLS certificate (via KeyVault/env; never commit certs to the repo)
- [ ] Enable **Redis/DB** backed nonce store; clear/rotate before launch
- [ ] Verify **HMAC** on a real/integration path, compare canonical line-by-line
- [ ] Endpoint exposed via **HTTPS** with HSTS
- [ ] Documentation/README updated for your environment
- [ ] CI gates remain on; publish only via explicit, gated workflow

---

## Notes

- Always sign the **raw** request body; avoid any whitespace/normalization changes.
- If your signature mismatches: print the server canonical exactly, compare quotes/whitespace, recompute locally.
- Keep your `.gitignore` strict for certificates/keys at all times.
