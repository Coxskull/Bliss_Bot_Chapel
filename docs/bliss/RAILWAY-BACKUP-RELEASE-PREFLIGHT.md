# Bliss Bot: production backup evidence and source-parity preflight

**Classification:** operational runbook, not public-release approval.
**Applies to:** Bliss Bot, Creative Academy, Wedding Planner in `Coxskull/Bliss_Bot_Chapel`.
**Production controls:** Keep `Runtime__RestrictedPreview=true`. Do not trigger a migration, redeploy, restore, purchase, outreach, campaign, payment, or payout while gathering the evidence below.

## 1. Establish the real backup policy

In Railway, select the **bliss-bot project > production > Postgres service > Backups** tab. Record the actual enabled schedule, latest completed snapshot timestamp, retention, last failure, coverage for the Postgres data volume, and whether a manual backup is available. Sanitize account and database details in any screenshot.

Railway's documentation says daily backups are retained for 6 days, weekly for ~1 month and monthly for ~3 months; these are product options, **not proof that this particular service has enabled backups**. Each volume backup is incremental and is billed according to its unique stored bytes. Ask the owner before any paid upgrade or storage-consuming operation.

**Critical:** Railway volume restores are restricted to the same Railway project and environment. A production `Restore` is not an isolated rehearsal: it stages a different volume for the existing service and requires a deploy; do not click Restore or Deploy on production to prove recovery. Wiping a volume destroys its backups. Do not confuse a mounted volume with a verified backup.

Reference: https://docs.railway.com/guides/postgres-backups-restores and https://docs.railway.com/volumes/backups

## 2. Prove recoverability in an isolated environment

An approved operator must perform a **separate, isolated** logical PostgreSQL backup-and-restore exercise, with least-privilege credentials, encryption in transit/at rest, and a dedicated disposable target that cannot reach production writes. Prefer a synthetic or empty approved fixture; production data copying requires explicit data/security authorization. Never paste a connection string, database password, bearer token, PFX password, or database dump in a GitHub issue, build log, spreadsheet, chat, or PDF.

Record a non-secret evidence reference for: the authorized source DB identity (fingerprint only); backup timestamp, tool, status, SHA-256/checksum and size; isolated restoration target identity; restoration execution result; verified EF migration history count/model compatibility; representative data integrity checks; rollback method; independently observed start/end times for recovery-time objective; reviewer identity and acceptance. Redact customer data and credentials.

The CI jobs in `.github/workflows/initial-migration-rehearsal.yml` verify **disposable PostgreSQL 16 and 18 synthetic fixtures only**. Their successful `pg_dump`/`pg_restore` does not prove a hosted Railway backup schedule, live data recovery, or production RPO/RTO.

## 3. Read-only source/configuration identity check

Use `scripts/bliss-operational-preflight.py` with two locally prepared **non-secret** files.

`variable-names.json` contains only the list of variable **names** returned from the authorized Railway project. Never include variable values.

`source-state.json` should contain only the four allowlisted metadata fields:

```json
{
  "main_repo": "Coxskull/Bliss_Bot_Chapel",
  "main_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "configured_repo": "Coxskull/Bliss_Bot_Chapel",
  "configured_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
}
```

Those hashes are placeholders: replace them with freshly fetched real GitHub main and Railway service **configured** SHA. Execute locally:

```bash
python scripts/bliss-operational-preflight.py variable-names.json --source-state source-state.json
python -m unittest discover -s scripts -p 'test_bliss_operational_preflight.py' -v
```

The command returns `CONFIG_DRIFT`, `CONFIG_MATCH`, or `NOT_PROVIDED`. It refuses unrecognized/malformed source payloads and never prints input identities or values. **Even `CONFIG_MATCH` is not proof of the bytes served by an existing deployment**. The script reports independent hosted backup and restore acceptance as false and never authorizes publication. Its exit code 0 means only name-inventory/source-metadata checks passed; **it does not mean release readiness**.

## 4. Release gates still requiring independent acceptance

- Real Railway backup enabled and independently evidenced, plus isolated logical recovery with reviewer approval
- Exact intended Railway PostgreSQL target and migration SQL reviewed, backup captured, owner-authorized one-time guarded migration and schema readback
- Trusted ingress/HTTPS observed on actual deployed topology, exact live source SHA verified
- Real signed OIDC PKCE, approved immutable subjects, role/tenant denial, CSRF, logout and session persistence validated against hosted environment
- End-to-end Bliss Bot, Creative Academy, Wedding Planner operations tested using approved synthetic tenants and browser evidence
- Legal/privacy/consent, advertiser and creator rights, and independently approved commerce decisions

Until all applicable gates are accepted: **public content may remain visible, but private Bliss application must remain restricted; no live campaigns, payouts, payment activation or external messaging.**

## 5. Current hosted readiness checkpoint (read-only, 2026-10-24 UTC)

This is a non-secret acceptance matrix from **connected live Railway and GitHub metadata**. It is not a deployment, browser login, production backup test, migration, or independent runtime attestation. Re-check after every deployment.

| Gate | Observed evidence | Acceptance |
| --- | --- | --- |
| Application availability | Railway `bliss-bot` production app Online, 1/1 replica, active SUCCESS deployment `2e8a1f15-9aeb-4e08-b82e-297bdfc31db9` | INFRASTRUCTURE ONLY |
| Database availability | Railway `Postgres` PostgreSQL 18 Online, 1/1 replica, volume `postgres-volume` (5,000 MB) mounted at `/var/lib/postgresql/data` | INFRASTRUCTURE ONLY |
| Current GitHub main | `f77b40b3a94f78004d9012cd2ac2c945feabc986` | SOURCE HEAD ONLY |
| Railway configured source | `Coxskull/Bliss_Bot_Chapel`, branch `main`, configured SHA `121fe37a5d2d9b26281324f1af8306cfaed509ee` | CONFIG_DRIFT |
| Actual active deployment metadata | Active deployment reports commit `7751d20be61b97cc89309bf2e11f80e6bf20e5d2`. GitHub compare main is **47 commits ahead** of this commit and **54 ahead** of configured SHA | FAIL_DRIFT; deployed bytes unverified |
| Preview restriction | Railway recorded `/health/live` HTTP 200 and deliberately gated `/login.html`, `/api/auth/session`, private routes at HTTP 503 under restricted preview; no live OIDC acceptance | KEEP RESTRICTED |
| Proxy policy | Current variable-name inventory did not expose `Runtime__LoopbackIngress__Enabled`, `Runtime__KnownProxies__0`, or `Runtime__Backup__Provider/Schedule/RetentionDays`. Presence/absence of names does not validate configured values | NOT VERIFIED |
| Production backups | Attached Postgres volume is not evidence of an enabled snapshot schedule. Connected Railway tools **do not expose** a snapshot schedule, timestamp, retention, PITR or restore history | BLOCKED |
| Isolated synthetic migration + restore | [CI PG16 and PG18 #40068210718](https://github.com/Coxskull/Bliss_Bot_Chapel/actions/runs/40068210718) succeeded, with separately retained synthetic `pg_dump` and `pg_restore` evidence artifacts | SYNTHETIC ONLY |
| Auth, HTTPS ingress and tenant isolation | No approved real hosted issuer-subject OIDC PKCE / CSRF / workspace-denial / session-persistence evidence in the current provider reads | BLOCKED |

The [three-way offline provenance checker](deployment-provenance-operator-checklist.md) should classify the exact source metadata above as `FAIL_DRIFT`. A matching source triple, if achieved later, would still be **MATCH_UNATTESTED**, not a guarantee of running image identity.

### Safe operator sequence — no cost or production changes authorized here

1. Use the owner-controlled Railway **Postgres > Backups** UI to record whether backup schedules are actually enabled, latest **successful** snapshot date, retention, failure status, incremental cost, and PITR status. Produce a sanitized read-only evidence reference. **Do not press Enable, Backup, Restore, Deploy, or purchase a plan as part of this inspection**. Current [Railway documentation](https://docs.railway.com/volumes/backups) notes storage-based charges and same-project/environment volume restore.
2. Review an independent isolated **logical** restore plan and scope for sensitive data before executing anything. Existing synthetic GitHub CI demonstrates the tooling, not hosted source recovery. Never use the Railway production volume Restore command as an isolation test.
3. Once backup/recovery has independent approval, identify the exact Railway database source-of-truth, inspect sanitized migration history/schema (no secrets or customer data), review the full migration chain and target-specific rollback, and obtain separate authority for a guarded maintenance-window write.
4. Only after the database gate is accepted, review the exact code SHA/image provenance and a **staged** version of the loopback ingress policy from [the current design](railway-ingress-policy-review.md): `Runtime__LoopbackIngress__Enabled=true`, `PublicOrigin=https://bliss-bot-production-5eb8.up.railway.app`, `PublicPort=8080`, `BackendPort=8081`. The public Railway service must remain on 8080; 8081 stays loopback-only. This is a **configuration proposal**, not an instruction to set variables now. Keep `Runtime__KnownProxies__*` empty under loopback mode; forbid retired broad Railway proxy trust and unverified forwarded headers.
5. Independently test approved immutable issuer-subject mappings, real signed OIDC PKCE, session/CSRF/logout and per-tenant denials on an isolated/staged environment. **Do not** log client secrets, authorization codes or real identities. Require approved synthetic data and audited reviewer signoff.
6. Release only with explicit owner approval, tested rollback and separate evidence that `/health/live`, `/health/ready`, authenticated workspace CRUD and negative authorization checks pass. Until then preserve `Runtime__RestrictedPreview=true`, no public operator login, campaign sends, subscription/payment activation, user outreach, or new charges.

**Hard rule:** `ProductionPostureDocument.BackupDeclared` checks configuration declarations; it is **not** evidence that Railway created a successful backup or that one can be restored. No provider-sourced backup verification means the P0 release gate remains BLOCKED even when offline posture/CI checks report success.

## 6. Read-only backup metadata via the documented Railway Public API

Railway's [public API documentation](https://docs.railway.com/integrations/api/manage-volumes#volume-backups) exposes two **query-only** GraphQL operations that can independently resolve the *metadata* missing from the connected Railway service inventory: `volumeInstanceBackupList` (backup ID/name/creation/expiration/size) and `volumeInstanceBackupScheduleList` (configured cron/kind/retention/creation). They require the **volume instance ID**, not merely the project volume ID. Railway's app/service inventory does not return the backup lists through the connector currently used for this inspection. Do not invent an instance ID or assume an empty list from an unsupported tool.

An authorized owner may run the following **read-only** queries through Railway's authenticated Public API explorer using their own existing session, or simply inspect **production > Postgres > Backups**. Do not paste API tokens or customer data into evidence:

```graphql
query BlissBackupMetadata($volumeInstanceId: String!) {
  volumeInstanceBackupList(volumeInstanceId: $volumeInstanceId) {
    id name createdAt expiresAt usedMB referencedMB
  }
  volumeInstanceBackupScheduleList(volumeInstanceId: $volumeInstanceId) {
    id name cron kind retentionSeconds createdAt
  }
}
```

- Before accepting this as provider proof, cross-check the returned `volumeInstanceId` resolves to `postgres-volume` of the **correct** project, `production` environment and `Postgres` service. Never compare with the unrelated `bliss-runtime` application volume.
- A list of existing backups/schedules is **read-only snapshot metadata**, not proof of successful isolated restoration or integrity. A restore drill requires separate approval and a truly isolated target.
- **Never** invoke `volumeInstanceBackupCreate`, `volumeInstanceBackupRestore`, `volumeInstanceBackupDelete`, `volumeInstanceBackupLock`, or any `volumeInstanceBackupSchedule` mutation to carry out this inspection. Volume restore swaps attached volumes on the original service after deploying and may interrupt production.
- Railway also documents [PostgreSQL PITR](https://docs.railway.com/volumes/point-in-time-recovery), which can restore into a *new* sibling service when previously enabled. It is **not** automatically active because PostgreSQL 18 is running. Enabling PITR creates storage/egress costs and redeploys components; the user has not authorized it. An existing PITR configuration and archiver state should be verified separately without enabling it.
- An accepted scheduled backup and a **real separately approved isolated restore** are both required before hosted migration and public unlock; do not promote a green source-level preflight result into production approval.

## 7. Ingress preflight alternate variable-name policy

The offline `bliss-operational-preflight.py` name inventory now accepts **either** `Runtime__LoopbackIngress__Enabled` **or** an indexed `Runtime__KnownProxies__0` name. Both present is `CONFLICTING_POLICY_NAMES` and fails closed; neither present is missing. The source-level reviewed loopback ingress implementation requires **zero** configured external KnownProxies. Presence of a variable name always yields only `*_DECLARED_UNVERIFIED`: the tool cannot attest whether the value is enabled, whether the configured origin is right, or whether forwarding/HTTPS is safe. `ingress_transport_verified` remains `false` in all inventory-only outcomes. Actual Kestrel/YARP hosted negative tests and owner signoff remain mandatory.
