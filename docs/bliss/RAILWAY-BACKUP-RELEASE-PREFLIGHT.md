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
