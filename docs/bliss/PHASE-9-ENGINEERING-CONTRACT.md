# Phase 9 Engineering Contract — Production Runtime Hardening

## Objective

Make the OIDC-secured Bliss API safe to operate behind a trusted reverse proxy
and across replaceable or replicated application instances.

## Required behavior

1. Persist ASP.NET Core Data Protection keys to a configured shared path outside Development.
2. Use a stable Bliss application discriminator so replicas can read the same session cookies.
3. Fail closed in non-Development environments when no persistent key path is configured.
4. Process only one forwarded hop and only from explicitly trusted proxy addresses.
5. Expose anonymous machine-readable liveness and database-readiness probes.
6. Rate-limit login initiation and all controlled write endpoints.
7. Partition write limits by authenticated subject, then identity name, then client address.
8. Return `429` JSON responses with `Retry-After`.
9. Return production exceptions through the ASP.NET Core problem-details boundary.
10. Remove the Kestrel `Server` response header and preserve the Phase 8 security headers.

## Configuration

```bash
Runtime__DataProtectionKeysPath=/var/lib/bliss/data-protection
Runtime__KnownProxies__0=10.0.0.10
Runtime__WriteRateLimitPermitLimit=30
Runtime__AuthenticationRateLimitPermitLimit=10
Runtime__RateLimitWindowSeconds=60
```

The data-protection directory must be a persistent, access-controlled volume
shared by every Bliss API replica. Proxy addresses must be deployment-owned
addresses; arbitrary forwarded headers are not trusted.

## Health endpoints

- `GET /health/live` proves the process can serve requests.
- `GET /health/ready` proves the configured database is reachable.

Neither endpoint exposes credentials, exception details, or application data.

## Explicit exclusions

- infrastructure provisioning or volume creation
- certificate/KMS management for key-ring encryption at rest
- database failover orchestration
- general denial-of-service protection at the CDN or load balancer
- changes to business workflows, matching rules, or Bliss/Alpha boundaries

## Acceptance criteria

- liveness and readiness return machine-readable health documents;
- readiness becomes unhealthy when the database cannot be reached;
- the configured write quota produces `429` with `Retry-After`;
- security headers remain present and the server product header is absent;
- existing authentication, CSRF, and business tests continue to pass.
