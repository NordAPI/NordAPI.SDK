# Production Webhook Checklist (Swish)

This checklist covers production-grade requirements for receiving Swish callbacks securely.

## Transport & endpoint
- [ ] Serve the webhook endpoint over **HTTPS only**.
- [ ] Enforce **HSTS** (at least on the primary domain).
- [ ] Consider a dedicated hostname/path for the Swish webhook (e.g. `/webhook/swish`).
- [ ] Ensure your server clock is synchronized (NTP).

## Required headers (reject if missing)
- [ ] `X-Swish-Timestamp` — Unix timestamp in **seconds** (integer).
- [ ] `X-Swish-Nonce` — unique value per request (UUID recommended).
- [ ] `X-Swish-Signature` — **Base64** HMAC-SHA256 signature.

## Signature verification
- [ ] Compute HMAC-SHA256 over the canonical string (UTF-8):

  `"<timestamp>\n<nonce>\n<body>"`

- [ ] Sign/verify the **exact raw request body bytes** (no JSON prettifying, no whitespace normalization).
- [ ] Use **constant-time** comparison for signature verification.
- [ ] Reject invalid Base64 or malformed signatures.

## Timestamp rules
- [ ] Require timestamp to be within an allowed skew window (recommended **±5 minutes**).
- [ ] Reject requests outside the window.
- [ ] Do not enable any “allow old timestamps” in production.

## Anti-replay (nonce)
- [ ] Reject replays using a **persistent** nonce store (Redis/DB).
- [ ] Do **not** rely on in-memory nonce storage in production.
- [ ] Nonce TTL should be at least the timestamp skew window (recommended **10 minutes**).

## Secrets management
- [ ] Store `SWISH_WEBHOOK_SECRET` in environment variables or a secret vault (e.g. Key Vault).
- [ ] Never commit secrets/certificates to source control.
- [ ] Rotate webhook secrets periodically and after suspected exposure.

## Observability & logging
- [ ] Log verification failures with reason codes (missing header, bad signature, timestamp drift, replay).
- [ ] Avoid logging sensitive payload fields and PII.
- [ ] Add rate limiting and alerting for abnormal traffic patterns.

## Environment hardening
- [ ] Ensure all dev-only flags are disabled in Release/Production (e.g. `SWISH_DEBUG`, `SWISH_ALLOW_OLD_TS`).
- [ ] Ensure relaxed TLS validation is never enabled in Release.

## Operational validation (go-live)
- [ ] Confirm the endpoint is reachable from Swish (public DNS + TLS).
- [ ] Verify a valid request returns HTTP 200 and your handler processes the event.
- [ ] Verify invalid signature returns 401/403.
- [ ] Verify replay returns 409 (or equivalent).
- [ ] Verify old timestamp returns 400/401/403 (choose one and keep consistent).

## Redis nonce store (recommended)
- [ ] Set `SWISH_REDIS` in production.
- [ ] Aliases (if supported in the sample): `REDIS_URL`, `SWISH_REDIS_CONN`.
- [ ] Confirm Redis connectivity and TTL behavior before going live.
