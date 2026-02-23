# Release QA (Quality Gate) — NordAPI.Swish (MVP)

This checklist is the single “go/no-go” gate before any public release (NuGet/GitHub visibility).
Keep it short, factual, and executable.

---

## A. Repo state & hygiene
- [ ] Working tree is clean: `git status --porcelain` is empty.
- [ ] On `main` and up to date: `git rev-list --left-right --count origin/main...main` returns `0 0`.
- [ ] No accidental files added (especially backups/temp): verify `git status` before commit.
- [ ] No secrets/certs were committed (see section E).

---

## B. Build & tests (must be repeatable)
Run from repo root:
- [ ] `dotnet --info` recorded (for troubleshooting).
- [ ] `dotnet restore`
- [ ] `dotnet build -c Release`
- [ ] `dotnet test -c Release`

Optional (but recommended for confidence):
- [ ] `dotnet test -c Release --collect:"XPlat Code Coverage"` (if coverage is enabled in this repo)

## B2. mTLS fail-fast (enforced by default)
- [ ] With `RequireMtls = true` (default) and **no** client certificate configured (`SWISH_PFX_PATH` not set / invalid), SDK startup fails fast by throwing `SwishConfigurationException` when the HTTP handler is created.
- [ ] With `RequireMtls = false` (test/mock only), the app starts without requiring a client certificate.

---

## C. Webhook verification (Swish baseline + optional signing layer)
- [ ] Swish baseline: endpoint is HTTPS-only and returns **HTTP 200** only after successful processing (non-200 triggers Swish retries).
- [ ] Replay protection works (same nonce rejected) and returns a consistent failure status (commonly 401/403 or 409).
- [ ] Timestamp skew is enforced (old/new timestamps rejected) and returns a consistent failure status.
- [ ] Optional signing layer (ONLY if enabled): signature mismatch returns 401/403 (consistent behavior).
- [ ] Handler is idempotent (duplicate callbacks do not break business logic).
> Note: Swish does not send `X-Swish-*` HMAC signature headers. Signature checks apply only if you add an optional signing layer (internal proxy/gateway or local test tool).

Suggested commands (repo-specific):
- Run sample server:
  - [ ] Set `SWISH_WEBHOOK_SECRET` (dev only) and start the sample.
- Run smoke script (tests the optional signing layer):
  - [ ] `.\scripts\smoke-webhook.ps1 ...` (verify expected 200 and a consistent failure status for replay/timestamp/signature)

Production guidance:
- [ ] In-memory nonce store is not used in production.
- [ ] Redis/DB nonce store is required in production (see section D).

---

## D. Redis nonce store (production requirement)
- [ ] Production requires a persistent nonce store (Redis/DB).
- [ ] Sample behavior in `Production` fails fast if no Redis is configured (if intended).
- [ ] Env var is standardized: `SWISH_REDIS` (aliases allowed if documented).
- [ ] Validate TTL behavior (nonce lives at least the allowed timestamp window).

---

## E. Secrets & certificates safety (non-negotiable)
- [ ] `.gitignore` blocks certificate/key files (at minimum): `*.pfx, *.p12, *.pem, *.key, *.crt, *.cer`
- [ ] No secrets in repo history for this release branch.
- [ ] No secrets in docs/examples beyond clearly-marked dev placeholders.

Quick local checks:
- [ ] Search for obvious secret patterns (manual review):
  - `git grep -n "BEGIN PRIVATE KEY" -- .`
  - `git grep -n "SWISH_.*SECRET" -- .`
  - `git grep -n "dev_secret" -- .` (ensure dev-only use is clearly labeled)
- [ ] Confirm no cert files are tracked:
  - `git ls-files "*.pfx" "*.p12" "*.pem" "*.key" "*.crt" "*.cer"`

---

## F. Docs sanity (rendering + correctness)
- [ ] Links in docs render correctly on GitHub (no 404).
- [ ] `docs/production-webhook-checklist.md` is clear about:
  - Swish baseline (HTTPS 443, TLS validation, IP filtering, no SNI)
  - Optional signing layer (if using extra headers)
- [ ] `docs/spec-parity/swish-commerce-callback.md` exists and tracks “before release” spec-lock items.
- [ ] Src package READMEs render correctly (EN/SV), and do not claim unsupported behavior.
- [ ] Avoid README encoding issues: edits done in editor; final files have LF and no BOM.
- [ ] Public API contract is updated: `SwishConfigurationException` exists in `tests/NordAPI.Swish.Tests/PublicApi/PublicTypeSanityTests.cs` (CI must not break on release).

---

## G. CI & release safeguards (must remain gated)
- [ ] CI is green on `main`.
- [ ] Publish workflow remains gated (no accidental releases).
- [ ] Release requires explicit tag + gate variable + NuGet API key (per repo policy).
- [ ] No public NuGet release until explicitly approved.

---

## H. Spec-lock verification (required before public release)
Before any public release, lock documentation against official Swish material.
Upload/provide the following (as files or pasted excerpts):
- [ ] Merchant Integration Guide (PDF) — full document preferred.
- [ ] Callback technical requirements section (HTTPS 443, TLS validation, IP filtering, SNI notes).
- [ ] Callback retry policy section (exact retry count and intervals; stop condition).
- [ ] TLS minimum version requirements (exact statement, including dates if present).
- [ ] Any official Swish IP allowlisting information (IP ranges / where published).
- [ ] Any official statements about callback authentication (confirm that Swish does **not** send signature headers; document any exception if the guide says otherwise).

Outcome:
- [ ] Update `docs/spec-parity/...` with exact section references and confirmed values.
- [ ] Ensure `docs/production-webhook-checklist.md` matches official baseline wording.

---

## I. Final go/no-go
- [ ] All sections A–H checked.
- [ ] “Known limitations” documented (if any).
- [ ] Versioning decision is correct (SemVer; no breaking change without major bump).
- [ ] After merge, cleanup done (delete branches, prune remotes).
