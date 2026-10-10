# Phase 20 Engineering Contract — Production Posture

## Objective

Protect the accepted Bliss Chapel system before another product layer is stacked
on it. Outside Development, the API must refuse to start until the hosted
database, identity provider, secret handling, role claims, encrypted
data-protection key ring, deployment proxy, and backup declaration are
configured. Operators must be able to see that posture without seeing secrets.

This phase does not finish production deployment. A process that satisfies
these gates is ready to be pointed at the hosted environment. It is not, by
itself, evidence that Alpha's hosted database, identity provider, or backup
system has been exercised.

## Required behavior

1. Outside Development, refuse startup when any of these are missing or unsafe:
   - a PostgreSQL connection string supplied outside `appsettings*.json`;
   - a non-loopback database host, username, database name, and secret-store password;
   - `SSL Mode=VerifyFull` with `Trust Server Certificate=false`; `Require` and `VerifyCA` are insufficient because they do not verify the database server name;
   - OIDC enabled, with an `https` non-loopback authority, client id, and secret-store client secret;
   - distinct non-empty roles `bliss.viewer`, `bliss.operator`, `bliss.reviewer`, `bliss.admin`, and `bliss.advertiser`;
   - a persistent data-protection directory and a PKCS#12 certificate whose password is not in appsettings;
   - at least one non-loopback `Runtime:KnownProxies` address;
   - `Runtime:Backup:Provider`, `Runtime:Backup:Schedule`, and retention from 1 to 3650 days.
2. Encrypt persisted data-protection keys with that certificate. A second process
   using the same directory and certificate must unprotect data protected by the first.
3. Apply the configured role claim from the ID token and from the userinfo
   document, including a JSON array, a single string, or a JSON-array string.
   Align cookie identities so `IsInRole` uses the configured role claim type.
4. Publish anonymous `GET /health/posture` and include the same secret-free
   document on `GET /api/runtime/status`.
5. Keep `/health/live` and `/health/ready` as the process and database probes.
   Readiness must not return connection strings, passwords, or exception text.
6. Log completed requests with method, path, status, request id, and duration.
   Do not log the query string, body, tokens, or secrets.
7. Show **Deployment posture** on the Status workspace, including an explicit
   statement when the production gates are not applied.
8. Keep a local backup/restore drill that refuses remote hosts and refuses to
   run without `BLISS_BACKUP_DRILL=1` and `BLISS_BACKUP_DRILL_TARGET=local`.

## Configuration

```bash
export ConnectionStrings__DefaultConnection="Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require"
export Authentication__Enabled=true
export Authentication__Authority="https://identity.example.com"
export Authentication__ClientId="bliss-chapel"
export Authentication__ClientSecret="<OIDC_CLIENT_SECRET>"
export Runtime__DataProtectionKeysPath="/var/lib/bliss/data-protection"
export Runtime__DataProtectionCertificatePath="/var/lib/bliss/data-protection.pfx"
export Runtime__DataProtectionCertificatePassword="<DATA_PROTECTION_CERTIFICATE_PASSWORD>"
export Runtime__KnownProxies__0="10.0.0.10"
export Runtime__Backup__Provider="platform-managed"
export Runtime__Backup__Schedule="daily"
export Runtime__Backup__RetentionDays="14"
```

`SSL Mode=Require` meets the TLS startup rule and is reported as transport
encryption without server-certificate verification. `VerifyCA` or `VerifyFull`
is the verified posture. Prefer `VerifyFull` once the hosted provider's CA is
installed.

The backup declaration records the operator's recovery contract. It does not
execute a backup. The local drill proves the dump/restore procedure only.

## Explicit exclusions

- provisioning hosted PostgreSQL, DNS, or an organizational identity provider
- cloud KMS or HSM integration beyond the PKCS#12 encryptor
- treating a single-host concurrent probe as production capacity
- changes to matching arithmetic, review authority, economics, or Alpha Auto boundaries
- n8n, live affiliates, payments, or crawling

## Acceptance criteria

- production startup fails closed for loopback databases, disabled SSL, non-HTTPS
  or loopback identity providers, placeholder or committed secrets, duplicate
  roles, missing certificates, missing proxies, and missing backup declarations;
- failure text does not echo those secrets;
- a complete production-shaped configuration starts, encrypts the key file, and
  round-trips protected data through a second process;
- anonymous production calls to business and status APIs return 401;
- readiness is unhealthy when the configured database cannot be reached, while
  liveness stays healthy;
- userinfo role arrays grant the same write and review rights as ID-token roles;
- Development still starts with authentication disabled and reports
  `productionGatesApplied: false`;
- existing Bliss and economics tests continue to pass.
