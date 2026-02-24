# Production Webhook Checklist (Swish)

This checklist covers production-grade requirements for receiving Swish callbacks securely.

> **Important (Swish baseline):** The official Swish Merchant Integration Guide requires callback delivery over **HTTPS on port 443**.
> Swish validates your callback server TLS certificate against commonly recognized CAs, recommends **IP filtering**, and notes that callback does **not** support SNI.
> The official guide does not describe Swish-sent HMAC signature headers; the `X-Swish-*` header checks below apply **only if you add an optional signing layer** (e.g., an internal proxy/gateway or local hardening) on top of Swish callbacks.


## Transport & endpoint
- [ ] Serve the webhook endpoint over **HTTPS only**.
- [ ] Enforce **HSTS** (at least on the primary domain).
- [ ] Consider a dedicated hostname/path for the Swish webhook (e.g. `/webhook/swish`).
- [ ] Ensure your server clock is synchronized (NTP).
- [ ] Accept **POST** only (reject other methods).
- [ ] Validate `Content-Type` starts with `application/json` (reject otherwise).
- [ ] Set a reasonable max request body size to avoid abuse.

## Optional signing headers (only if you enable an extra signing layer)
- [ ] `X-Swish-Timestamp` — Unix timestamp in **seconds** (integer).
- [ ] `X-Swish-Nonce` — unique value per request (UUID recommended).
- [ ] `X-Swish-Signature` — **Base64** HMAC-SHA256 signature.

## Signature verification
- [ ] Compute HMAC-SHA256 over the canonical string (UTF-8):

  `"<timestamp>\n<nonce>\n<body>"`

- [ ] Sign/verify the **exact raw request body bytes** (no JSON prettifying, no whitespace normalization).
- [ ] Read the body as raw bytes exactly once (avoid re-encoding).
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
- [ ] Ensure all dev-only relaxations are disabled in Release/Production (e.g. do not bypass certificate validation or timestamp checks).
- [ ] Ensure relaxed TLS validation is never enabled in Release.

## Operational validation (go-live)
- [ ] Confirm the endpoint is reachable from Swish (public DNS + TLS).
- [ ] Verify a valid request returns HTTP 200 and your handler processes the event.
- [ ] Verify invalid signature returns 401/403.
- [ ] Verify replay returns a consistent failure status (commonly 401/403 or 409) and keep it consistent.
- [ ] Verify old timestamp returns a consistent failure status (commonly 400/401/403) and keep it consistent.
- [ ] Ensure the handler is **idempotent** (the same event may be delivered more than once).
- [ ] Decide and document exact status codes for failures (e.g. 401/403 signature, 409 replay) and keep them consistent.
- [ ] Verify callback retries behave as expected (Swish retries on non-200; HTTP 200 stops retries).

Optional (distributed): For multi-instance production, see docs/optional/redis-nonce-store.md for Redis-backed nonce storage (replay protection).

## Redis nonce store (recommended)
- [ ] Set `SWISH_REDIS` in production.
- [ ] Aliases (if supported in the sample): `REDIS_URL`, `SWISH_REDIS_CONN`.
- [ ] Confirm Redis connectivity and TTL behavior before going live.
