# Spec parity: Swish Commerce callback (Merchant Integration Guide 2.6)

Goal: Keep a clear mapping between the official Swish requirements and NordAPI.Swish SDK behavior/docs.
This is intentionally short (MVP) and should be expanded before release.

Source: Swish Merchant Integration Guide 2.6 (PDF)
- Technical callback requirements (verify exact section): HTTPS on port 443, TLS certificate validation, IP filtering recommendation, no SNI.
- Callback behavior & retries (verify exact section): callback delivery and retry behavior; stop on HTTP 200.

## Official baseline (Swish)
- Callback delivery is **HTTPS POST** over TLS, endpoint must be **HTTPS on port 443**.
- Swish validates the callback server TLS certificate against commonly recognized CAs.
- IP filtering is highly recommended.
- Callback does **not** support Server Name Indication (SNI).
- TLS version requirements: verify the guide’s minimum TLS version for callbacks (commonly TLS 1.2+).
- If callback delivery fails, Swish retries (per guide retry policy) and stops retrying when **HTTP 200 OK** is returned.

## NordAPI behavior (SDK + docs)
- We document the official Swish baseline in `docs/production-webhook-checklist.md`.
- The SDK’s `X-Swish-*` HMAC header verification is treated as **optional hardening** (extra signing layer) and is not claimed to be Swish-official.
- Replay protection guidance: use Redis/DB nonce store in production (not in-memory).
- Operational guidance: idempotent handlers and consistent status codes.

## Parity checklist (MVP)
| Official Swish requirement | NordAPI status | Notes / action |
|---|---|---|
| HTTPS callback on port 443 | Documented | Ensure sample/docs never suggest non-443 in production. |
| Swish validates server TLS certificate (CA) | Documented | Make sure docs mention publicly trusted cert chain. |
| IP filtering recommended | Documented | Add link/reference to Swish IP list section before release. |
| No SNI for callback | Documented | Mention: use dedicated host/cert (avoid multi-SNI setups). |
| Callback retries + stop on HTTP 200 | Documented | Ensure webhook handler returns 200 only after processing succeeds. |
| Optional: poll/retrieve status from Swish | Not emphasized | Before release: add a short note recommending polling/retrieve for final status if needed. |

## Open items (before release)
- Add the exact Swish guide section references for IP allowlisting (server IP list) and recommended operational patterns.
- Add a short "Why optional signing headers exist" section for customers using a gateway/proxy in front of the webhook.
