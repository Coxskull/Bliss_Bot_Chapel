# Phase 9 Runtime Hardening Evidence

## Acceptance result

Phase 9 hardens the OIDC-secured API for reverse-proxied, replaceable, and
replicated production instances without changing Bliss business behavior.

## Automated verification

Command:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --no-restore --verbosity minimal
```

Result: **84 passed, 0 failed, 0 skipped**.

Dedicated tests prove:

- liveness and database readiness return machine-readable health documents;
- healthy in-memory readiness includes the named database check;
- response hardening headers remain present;
- the server product header is absent;
- the configured controlled-write quota returns `429` and `Retry-After`.

## Production-mode verification

A production-mode process was started with OIDC enabled, a temporary persistent
key path, and an intentionally unreachable database.

Observed results:

- `/health/live` returned `200` and `Healthy`;
- `/health/ready` returned `503`, named the database check, and reported `Unhealthy`;
- `/api/creators` returned `401` without contacting OIDC discovery;
- the configured Data Protection directory was created and received a key file;
- CSP, MIME-sniffing, and referrer headers were present;
- no `Server` response header was emitted.

The intentionally unreachable database proves that readiness fails independently
while liveness remains healthy. Positive database readiness is covered by the
automated in-memory acceptance test.

## Fail-closed verification

Starting in Production without `Runtime:DataProtectionKeysPath` terminated with:

```text
Runtime:DataProtectionKeysPath is required outside Development so OIDC sessions survive restarts and replicas.
```

## Deployment boundary

The deployment platform remains responsible for:

- creating and mounting the access-controlled shared key volume;
- encrypting the key ring at rest through platform storage controls or a future KMS/certificate integration;
- supplying the real database and OIDC secrets;
- listing only deployment-owned trusted proxy addresses;
- enforcing broader edge/CDN denial-of-service controls.
