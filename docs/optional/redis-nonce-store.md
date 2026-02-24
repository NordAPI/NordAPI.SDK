# Optional: Redis Nonce Store (Distributed replay protection)

This document explains how to use the built-in Redis-based nonce store for **webhook replay protection** in **distributed** deployments (multiple instances / containers).

> NordAPI.Swish does **not** require Redis.
> The default in-memory nonce store is sufficient for single-instance setups and local development.
> Use Redis only when you need a shared nonce state across multiple instances.

---

## Why you might need Redis

Webhook replay protection relies on a nonce store that can determine whether a nonce has already been seen within the accepted time window.

In a single-process deployment, in-memory storage is fine.

In a multi-instance deployment (Kubernetes, multiple pods, autoscaling), each instance has its own memory. Without a shared store:
- the same webhook can be accepted by multiple instances if retries are routed to different nodes.

Redis provides shared state so **only one instance** can accept a given nonce.

---

## What this affects (and what it does not)

- ✅ Affects: **webhook anti-replay** (nonce verification)
- ❌ Does not affect: API calls / mTLS / request signing
- ❌ Does not add any runtime requirement unless you opt in

---

## Enable Redis nonce storage

Use `AddSwishWebhookVerification(...)` and then register the nonce store using `AddNonceStoreFromEnvironment(...)`.

TTL is a required argument and should be aligned with your replay acceptance window.

```csharp
// Reads connection details from environment variables (see "Configuration" below)
services.AddSwishWebhookVerification(options =>
{
    options.SharedSecret = "your_webhook_secret";
})
.AddNonceStoreFromEnvironment(TimeSpan.FromMinutes(10));
```

Optional: pass a key prefix if you want to namespace Redis keys (recommended when multiple apps share the same Redis instance).

```csharp
services.AddSwishWebhookVerification(options =>
{
    options.SharedSecret = "your_webhook_secret";
})
.AddNonceStoreFromEnvironment(TimeSpan.FromMinutes(10), prefix: "MyAppPrefix:");
#csharp

**API reference:**
- `src/NordAPI.Swish/DependencyInjection/SwishWebhookServiceCollectionExtensions.cs`
- `src/NordAPI.Swish/Webhooks/NonceStoreExtensions.cs`
```

---

## Configuration (environment variables)

When using `AddNonceStoreFromEnvironment(...)`, the SDK resolves the Redis connection in this priority order:

1. `SWISH_REDIS`
2. `SWISH_REDIS_CONN`
3. `REDIS_URL`

**Source:** `src/NordAPI.Swish/Internal/RedisEnv.cs`

---

## Production security notes

Redis must be secured in production:

- Use authentication (password / ACL)
- Use TLS where applicable
- Restrict network access (private network / firewall rules)
- Never expose Redis publicly

---

## Operational guidance

- Choose a TTL aligned with your replay acceptance window.
- Monitor Redis availability; nonce verification depends on it when enabled.
- For high availability, use a managed Redis offering or Redis with failover.

---

## Next references

- Production webhook checklist: `docs/production-webhook-checklist.md`
