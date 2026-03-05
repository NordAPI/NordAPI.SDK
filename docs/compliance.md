# NordAPI.Swish — Security & Compliance Notes

This document describes security-relevant behavior implemented by the NordAPI.Swish SDK.
It is written to support technical reviews and production readiness audits.

## Scope

To support automated and manual audits, NordAPI.Swish clearly defines the boundary between SDK logic and consumer implementation.

### In Scope (SDK Responsibility)
- **Transport Integrity**: Enforcement of mutual TLS (mTLS) for outbound Swish API communication.
- **Cryptographic Guardrails**: Implementation of deterministic HMAC-SHA256 signing and constant-time comparison.
- **Validation Guardrails**: Fail-closed validation of security-sensitive configuration parameters (Clock Skew and Message Age) during application startup.

### Out of Scope (Consumer Responsibility)
- **Secret Management**: Secure storage, access control, and rotation of client certificates and shared secrets.
- **Infrastructure Security**: Network-level protection, including firewalls, WAFs, and IDS/IPS systems.
- **Persistence**: Choosing and maintaining the storage provider for nonces (e.g., Redis) used to prevent replay attacks.

## Non-negotiable security properties

### 1) Transport security (mTLS)
- The SDK requires mutual TLS (mTLS) by default for outbound Swish API calls.
- Fail-closed behavior: if the client certificate is missing when mTLS is required, SDK configuration fails fast during startup.

### 2) Deterministic request signing
The SDK implements a deterministic signing workflow:
- Canonical input is deterministic (byte-exact) to avoid ambiguous signatures.
- Timestamp uses Unix time in seconds.
- HMAC is computed with SHA-256 (HMAC-SHA256). Outbound request signing uses a hex-encoded MAC.
- Strict formatting is enforced to avoid signature drift.

### 3) Webhook verification (fail-closed)
Webhook verification is designed to be deterministic and fail-closed:
- **Startup Validation**: The SDK enforces a strict 15-minute cap on `AllowedClockSkew` and `MaxMessageAge` during service registration. Configurations exceeding this limit will cause the application to fail-fast.
- **Signature Verification**: Verified as Base64 HMAC-SHA256 of the canonical string: `"<timestamp>\n<nonce>\n<body>"`.
- **Constant-Time Comparison**: Signature verification utilizes constant-time equality logic to mitigate side-channel timing attacks.
- **Time Validation**: Enforced using allowed clock skew and maximum message age, with both values capped at 15 minutes during startup validation.
- **Replay Protection**: Nonce reuse is rejected via a consumer-provided nonce store.

### 4) Idempotency discipline
- Idempotency keys are generated once per logical operation and reused across retries to prevent duplicate logical operations.

## What the SDK intentionally does NOT do

- **No proxy / gateway behavior**: certificates and keys remain in your environment.
- **No persistence layer included**: production storage choices (e.g., Redis for nonce storage) are owned by the application.
- **No secrets shipped with the SDK**: certificates/keys must not be committed or embedded.
- **No relaxed “dev-mode” behavior** in Release builds.

## Operational guidance (high level)

- Use a durable nonce store in production to make replay protection effective across instances.
- Ensure certificate rotation procedures exist for your environment.
- Treat verification failures as security signals; do not ignore them.
