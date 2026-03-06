# NordAPI.Swish Documentation Hub

Operational, security, and production guidance for the NordAPI Swish SDK.

This hub provides deeper documentation for developers and architects moving beyond the initial quickstart. For installation and basic usage, see the [root README](../README.md).

## Production & Security
- [Integration Checklist](./integration-checklist.md) — Mandatory steps for production readiness.
- [Webhook Hardening](./production-webhook-checklist.md) — Detailed guidance on HMAC, timestamps, and raw byte-exact verification.
- [Compliance & Audit](./compliance.md) — Security standards and spec-parity notes.

## Operations & Reliability
- [Maintenance](./maintenance.md) — Long-term operation and certificate rotation guidance.
- [Redis Nonce Store](./optional/redis-nonce-store.md) — Scaling replay protection for distributed environments.
- [Release QA](./release-qa.md) — Stability gates and release verification guidance.

## Spec Parity
- [Swish Commerce Callback Notes](./spec-parity/swish-commerce-callback.md) — Edge cases and merchant integration specifics.

---

NordAPI.Swish is part of NordAPI. Target: enterprise-grade stability for Swedish financial infrastructure.
