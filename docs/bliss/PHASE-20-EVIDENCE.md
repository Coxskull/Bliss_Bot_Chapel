# Phase 20 Production Posture Evidence

> **Evidence reconciliation — 2026-10-10:** The production-shaped capture below is historical and must not be treated as current production-posture acceptance. It used `SSL Mode=Require`, which does not verify the server certificate. The current `ProductionPostureEvaluator` requires `SSL Mode=VerifyFull` with `Trust Server Certificate=false`, and `Phase20ProductionPostureTests` explicitly reject `Require`, `VerifyCA`, and `VerifyFull;Trust Server Certificate=true`. Consequently, the historical capture's `productionGatesApplied: true` is inconsistent with the current evaluator and is not valid evidence that the current source passes the hosted gate. A fresh production-shaped run must use `VerifyFull` and the provider CA before deployment posture can be signed off. No hosted environment is asserted to have been tested by this correction.

## Acceptance result

Phase 20 adds a fail-closed production posture on top of the BLISS-SYS-NEGREG-001
acceptance pass. It does not declare the hosted production deployment finished.
The PostgreSQL database in the production-shaped probe was a non-routable
documentation address, not Alpha's hosted database. The identity provider was
not contacted. The backup drill ran on local PostgreSQL 16.

## Automated verification

Command:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Recorded evidence snapshot: **200 passed, 0 failed, 0 skipped**. This is the historical Phase 20 test record, not a claim about a fresh local run or the current source head. Current source validation should be taken from the corresponding exact-head GitHub Actions run.

The previous regression total was 172. Phase 20 adds the production-posture,
role-claim, request-log, certificate, startup-rejection, and backup-drill
refusal tests. Summary: `docs/bliss/evidence/phase20/tests/dotnet-test.txt`.

Dedicated tests prove:

- Development does not apply the hosted gates and still reports secrets outside appsettings;
- a complete hosted declaration passes, including `VerifyFull` server-certificate posture;
- `SSL Mode=Require` is transport encryption without server-certificate verification;
- loopback hosts, disabled SSL, weak database passwords, non-HTTPS authorities, loopback identity hosts, placeholder secrets, secrets stored in appsettings, duplicate roles, secret-like backup providers, loopback-only proxies, and a missing key certificate are rejected;
- rejection text does not echo the secret values;
- committed `appsettings.json` and `appsettings.Development.json` keep the connection string, client secret, and certificate password empty;
- a wrong certificate password fails without echoing either password;
- request completion logs record the path and omit the query string;
- a userinfo role array grants admin write and review;
- a userinfo reviewer string can review and cannot write;
- a JSON-array role claim is normalized before authorization;
- cookie alignment honors framework role claims through the configured `roles` claim;
- the OpenID Connect userinfo event applies those roles;
- a production-shaped host returns live 200, ready 503, posture 200, and anonymous 401 for `/api/creators` and `/api/runtime/status`;
- the persisted key file contains `EncryptedData`, does not contain a plaintext `<value>`, and round-trips through a second process using the same certificate;
- 48 concurrent liveness reads on that host returned 200;
- production startup without the certificate, with a loopback database and SSL disabled, exits before serving;
- the backup drill script exits 2 unless the local opt-in is set.

## Production-shaped process

A Production process was started with a hosted-shaped connection string
(`203.0.113.10`, `SSL Mode=Require`, one-second timeout), HTTPS authority
`identity.example.test`, a PKCS#12 data-protection certificate, proxy
`10.0.0.10`, and a platform-managed daily backup declaration. Secrets were
process environment values only.

| Probe | Result |
| --- | --- |
| `GET /health/live` | 200, process Healthy |
| `GET /health/ready` | 503, database Unhealthy, description `Database connection failed.` |
| `GET /health/posture` | 200, `productionGatesApplied` true, `keysEncryptedAtRest` true |
| `GET /api/creators` | 401 |
| `GET /api/runtime/status` | 401 |

Posture also reported hosted database, database TLS, HTTPS identity, distinct
role claims, a deployment proxy, backup retention of 14 days, and secret
material outside appsettings. Server-certificate verification stayed false
because the connection used `SSL Mode=Require` rather than `VerifyFull`.
Response bodies did not contain the database password, client secret, or
certificate password. The key file was encrypted. Captures:

- `docs/bliss/evidence/phase20/api/production-health-live.json`
- `docs/bliss/evidence/phase20/api/production-health-ready.json`
- `docs/bliss/evidence/phase20/api/production-health-posture.json`
- `docs/bliss/evidence/phase20/api/production-anonymous-creators.hdr`
- `docs/bliss/evidence/phase20/api/production-anonymous-status.hdr`
- `docs/bliss/evidence/phase20/api/production-startup-rejected.txt`

The rejected startup used a loopback host, `SSL Mode=Disable`, and no
certificate. The process exited 134 with `Production posture is incomplete`
and did not print the password or client secret.

## Development browser

`http://127.0.0.1:5088/operations#/status` rendered **Deployment posture** with
production gates **Not applied**, role claim `roles`, and the five configured
roles. Process was Healthy. Database was Unhealthy because this process had an
empty connection string. Overview remained reachable, and returning to Status
kept the posture panel. Business reads returned HTTP 500 while the connection
string was empty; Status still rendered from `/api/runtime/status`.

The request log for `GET /health/live?access_token=...` recorded
`Request completed GET /health/live 200` and did not record the query token.

Screenshot: `docs/bliss/evidence/phase20/status-deployment-posture.webp`.

## Local backup drill

PostgreSQL 16.15 on loopback, with `BLISS_BACKUP_DRILL=1` and
`BLISS_BACKUP_DRILL_TARGET=local`, created a scratch database, dumped it,
dropped it, restored it, and matched the marker:

```text
Backup drill restored marker marker-20260929T040401Z-6156.
```

Recorded in `docs/bliss/evidence/phase20/backup-drill.txt`. The script refuses
to run against a non-loopback `PGHOST` and refuses to run without both opt-in
variables. This is a procedure proof, not a restore of a hosted Alpha backup.

## Boundaries that remain

- No hosted Alpha PostgreSQL database was migrated or queried.
- No organizational identity provider issued a token. Role-claim behavior was
  proven against userinfo and ID-token claim shapes in tests.
- `SSL Mode=Require` does not verify the server certificate. `VerifyFull` is
  the reported verified posture once the provider CA is available.
- The certificate password still has to live in the platform secret store.
  This phase encrypts the key XML; it does not add a cloud KMS.
- The backup declaration is configuration. The drill was local.
- The 48-request liveness probe is a single-process guard, not production capacity.
