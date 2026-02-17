# NordAPI.Swish SDK — Maintenance & Release Gates

This document is intentionally short and operational.
If a step is not green/verified, do not release.

---

## 1) SemVer policy (MAJOR / MINOR / PATCH)

**MAJOR (X.0.0) — breaking changes**
- Remove or rename any **public** type/member/namespace.
- Change any **public** method signature (params, return type), including adding a required parameter.
- Change behavior in a way that breaks existing consumers (even if signatures are unchanged).
- Tighten defaults in a way that can break runtime behavior for existing users (security changes included).

**MINOR (X.Y.0) — backward compatible additions**
- Add new **public** types, members, overloads, optional parameters (with safe defaults).
- Add new configuration flags/options that do not change existing defaults.
- Any update that requires adding entries to `tests/NordAPI.Swish.Tests/PublicApi/PublicTypeSanityTests.cs`
  - Adding entries → at least **MINOR**
  - Removing entries → **MAJOR**

**PATCH (X.Y.Z) — backward compatible fixes**
- Bug fixes that do not change public surface area.
- Performance improvements.
- Internal refactors (no behavior change for consumers).
- Docs/test changes only.

**Rule of thumb**
- If you must update `PublicTypeSanityTests` → it is **not** a patch.
- If you touched public signatures → it is **MAJOR**.

---

## 2) Enterprise Release Gate (required before any tag/publish)

### 2.1 Branch & repo sanity (must pass)
Run from repo root:

```powershell
# Must be on a feature/release branch (never release from main with unreviewed changes)
git rev-parse --abbrev-ref HEAD

# Must be clean (no unstaged/staged leftovers)
git status

# Ensure branch is up to date with origin
git fetch --prune
git status -sb
```

**Fail conditions**
- You are on `main` with unreviewed changes.
- `git status` is not clean (unless you are in the middle of a controlled commit).

---

### 2.2 Tests (must pass)
```powershell
dotnet test
```

---

### 2.3 Release build (must pass)
```powershell
dotnet build -c Release
```

**Fail condition**
- Any DEBUG-only behavior leaks into Release (e.g., relaxed TLS validation). Treat as a release blocker.

---

### 2.4 Package (must succeed)
```powershell
dotnet pack -c Release
```

Locate the produced `.nupkg` (typically under `src/**/bin/Release`).

---

### 2.5 Package inspection (manual but mandatory)
Inspect the `.nupkg` in **NuGet Package Explorer** (or equivalent).

Verify:
- Package metadata is correct (Id, Version, Description).
- Package README renders correctly (the `PackageReadmeFile` content is the intended README).
- License is present and correct.
- Icon (if used) is present and correct.
- No unexpected files are included.
- No secrets/certs/keys are included (must never ship):
  - `.pfx`, `.p12`, `.pem`, `.key`, `.crt`, `.cer`

---

## 3) Retry policy guard (no double retry)

**Invariant**
- `SwishClient` is the single source of truth for retry/backoff.

Before release, verify:
- No additional retry layer is introduced by docs or samples.

Checks:
```powershell
git grep -n "AddPolicyHandler" -- .
git grep -n "Polly" -- .
git grep -n "Retry" -- samples docs src
```

**Fail condition**
- Samples or docs suggest adding a retry policy on the same HttpClient pipeline unless explicitly documented as an intentional advanced scenario.

---

## 4) Security gates (non-negotiable)

Before release, verify:
- No secrets in repo:
  ```powershell
  git ls-files | findstr /i "\.pfx$ \.p12$ \.pem$ \.key$ \.crt$ \.cer$"
  ```
- Release build does not allow dev relaxations:
  ```powershell
  dotnet build -c Release
  ```
- Logging contains no PII by default (review changes to logging when touched).

---

## 5) Spec-parity audit cadence (Swish official docs)

Frequency:
- **Quarterly** (or immediately if Swish/bank/provider announces changes).

What to check:
- mTLS requirements & certificate guidance
- Any stated constraints for callback endpoints (HTTPS, ports, SNI)
- Retry behavior for callbacks (count/interval/stop condition)
- Any official signature/canonicalization requirements (if changed)

Output requirements:
- Update `docs/spec-parity/*` with concrete notes.
- If behavior changes are needed: create a dedicated PR (spec-locked), with tests.

---

## 6) Final release checklist (do not skip)

- [ ] SemVer decision recorded (why MAJOR/MINOR/PATCH)
- [ ] `dotnet test` PASS
- [ ] `dotnet build -c Release` PASS
- [ ] `dotnet pack -c Release` PASS
- [ ] `.nupkg` inspected (README/lic/icon/contents)
- [ ] No double retry introduced (grep checks PASS)
- [ ] No cert/key files included anywhere
- [ ] Docs match code (especially security defaults)

---

## Notes
- Keep PRs single-purpose: one concern per PR.
- If you are unsure whether something is breaking, treat it as breaking and bump MAJOR.
