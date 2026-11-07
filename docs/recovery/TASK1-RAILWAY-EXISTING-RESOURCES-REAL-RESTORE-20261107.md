# ALPHA Task #1 — Existing Railway Recovery Path | 2026-11-07
**Status:** REAL BACKUP RESTORE **NOT STARTED**. PREPARED / OWNER COST + SECURITY + DATA-TRANSFER APPROVAL REQUIRED.
**Scope:** Reuse existing Railway account/projects and Bliss Bot recovery tools. No project/service/volume created. No paid allocation, decryption, database transfer, restore, CI artifact upload or production configuration change.
**Global STOP:** unchanged / engaged. **Additional spending authorized:** $0.

## Hobby Plan evidence — owner-provided screenshot, November 7, 2026
The owner's Railway **Settings → Plans** screenshot confirms the existing **`alpha7auto's Projects` Hobby workspace**, rather than a need to buy a new plan. It displays:
- **$5 of included monthly usage credits** (this is a monthly allowance, **not an extra unspent balance**).
- **Up to 5 replicas, 8 vCPU / 8 GB RAM per replica**; a separate plan tile advertises a higher maximum **48 vCPU / 48 GB RAM per service**. These are upper limits, **not reserved recovery resources**.
- **Up to 5 GB storage** on the plan. This does **not** confirm a free, empty 5 GB allocation or permission to add another volume; the separate live Bliss Bot PostgreSQL 5,000 MB volume and application 500 MB volume already exist.
- An active platform banner: **"External network blocks affecting access to Railway"**. This is an access-risk notice, not proof the existing running services or private database were unreachable. Verify the incident is resolved before any time-limited transfer.
- The screenshot **does not show** workspace usage, consumption-to-date, remaining allowance, projected restore workload cost, payment limit or whether the existing volumes consume the storage allowance.

Railway's own pricing FAQ says usage **exceeding the included $5 is billed separately**, even on Hobby:
https://docs.railway.com/pricing/faqs#why-am-i-charged-for-more-than-5-on-the-hobby-plan

**Budget gate:** NO-GO for provisioning pending the **Railway Usage** view for this **specific workspace**, current month, including consumption to date, period end, running resource commitments and projected short-lived compute/storage/egress. Under the Captain's **$0 additional spending** instruction, do not assume one can create a database without a charge merely because plan resources allow it. No service/volume or additional plan has been purchased.

## Verified existing Railway capacity (read-only)
All values below are connected Railway provider readbacks (24-hour resource metrics), not dedicated recoverable allocations.

| Existing resource | Actual environment | Most recent RAM use / reported RAM limit | Storage and risk | Suitability today |
| --- | --- | --- | --- | --- |
| `alpha-henry-ford-factory-staging` / `alpha-hf-factory-engine` | staging / iad, 1 replica | ~0.060 GB / 8 GB | No persistent volume; **has public HTTPS domain, active worker/provider database credentials and outbound IPv6 egress enabled** | **NO** for real sensitive restore in-place; co-location would expose factory/restore to each other's privileges |
| same project / `alpha-hf-staging-health` | staging / iad, 1 replica | ~0.004 GB / 8 GB | No persistent volume; public HTTPS domain | **NO** — shared monitoring workload, no isolated PostgreSQL service |
| `bliss-bot` / production application | production / iad | ~0.119 GB / 8 GB | 500 MB application volume used; service is live | **NO** — do not redeploy or use application mount for real backup |
| `bliss-bot` / `Postgres` | production / iad, PostgreSQL **18** | ~0.043 GB / 8 GB | 5,000 MB mounted volume, ~0.138 GB measured disk usage; existing production DB | **NO** — never restore into this live cluster or risk interfering with existing tenants, storage, roles and backups |
| legacy `alpha-backend` | production / sfo | metrics all zero; latest deployment FAILED | No volume; not a known healthy isolated database | **NO** |
| two additional alpha-backend app projects | production | Running application services | No mounted volumes, not assigned for recovery | **NO** |

**Important:** The 8 GB / 8 vCPU Railway metrics are reported runtime limits, not guaranteed no-charge capacity. Spare memory on a public or credentialed service is **not** proof of secure isolation. The current recovery-ready, unused private PostgreSQL service count is **zero**.

## Real Supabase targets — avoid guessing which archive belongs where
Read-only aggregate `pg_catalog` queries performed November 7, 2026; not copied customer records:

| Supabase environment | PostgreSQL | Current size (bytes) | User tables | RLS tables | Foreign keys |
| --- | --- | ---: | ---: | ---: | ---: |
| Bliss Bot `bkutbglzivfdnoerigyb` | 17.6 | 14,273,683 | 75 | 60 | 72 |
| Alpha Lead Generation `yyesqcyjhqjryaskrqip` | 17.6 | 13,118,611 | 63 | 48 | 57 |
| Alpha `giujjtscdvtpyjpnceag` | 17.6 | 75,893,907 | 113 | 79 | 97 |
| Existing persistent lead-gen staging `wdlmcriajgsretgyusmo` | 17.11 | 18,978,483 | 101 | 82 | 107 |

These are **source metadata baselines**, not proof of a valid dump, a restore, live row equivalence or Auth/MFA recovery. Logical dump/WAL/index/temp storage needs extra headroom. PostgreSQL version and managed Supabase extensions/roles must be reviewed, preferably in a separate PostgreSQL 17 target for compatibility.

## Existing backups located — metadata only, no decryption or transfer
- Owner-restricted Drive folder `ALPHA Encrypted Recovery Backups`: `ALPHA_Recovery_Encrypted_20261105.7z` (321,472 bytes), `STAGE7_AUTH_20261105T053647Z.7z` (355,040 bytes). These are **encrypted containers**; bytes alone do **not** prove a whole-database logical dump or which Supabase project they represent.
- Drive `BLISS BOT CHAPEL - ENCRYPTED POSTGRES BACKUPS`: a `bliss-backup-20261106T022007Z-*.age` file (1,086 bytes) and checksum sidecar (109 bytes). The tiny file is **not accepted as verified real full DB** without internal format/source manifest; another archived file is explicitly named SYNTHETIC.
- The document `ALPHA-SDV-PRIVATE-VERIFIED-LOGICAL-BACKUP-20261028-0000003e` has format `ALPHA_D1_LOGICAL_GZIP_B64_V2` for **Cloudflare D1**, not a PostgreSQL `PGDMP`. Never feed it to `pg_restore`.
- Owner's recovery README instructs that the **private decryption key never leaves the owner's device**; a cloud upload or successful encryption alone does **not** pass restoration acceptance.

Do **not** fetch backups to CI, run their contents through logs, print customer data, upload private keys to GitHub/Railway, or infer provenance from filenames. First match target project ref, archive format, timestamp, manifest, authenticated dump procedure and SHA-256 in a **restricted owner-controlled environment**.

## Existing recovery work reused (NOT rerun)
- `scripts/postgres-backup-restore-drill.sh`: documents custom `pg_dump -Fc` / `pg_restore --no-owner` and cleanup, but is **loopback-only synthetic** and refuses remote/hosted use.
- `scripts/phase4c-nonprod-backup.sh`: records encryption and checksum permissions; hard-fails unless a specific **GitHub CI fixture** identity matches. Never broaden its allowlist to production.
- `scripts/postgres-hosted-verifyfull-acceptance.sh`: reusable read-only TLS `verify-full` method with explicit private CA and host identity; requires protected credentials/approved endpoint, and is **not** restore authorization.
- `scripts/postgres-verifyfull-rehearsal.sh`, `scripts/verify-initial-recovery-fixture.sh`, `scripts/phase6-recovery-check.sh`: already built synthetic drills; **not rerun** by this directive.
- **New source-only preparation**: `scripts/task1-railway-real-archive-preflight.sh` inspects a previously decrypted, owner-approved **real custom-format** PG archive header/TOC/checksum without database/network calls, and `scripts/task1-post-restore-metadata-audit.sql` produces only schema/constraint/extension/RLS metadata after a future approved restore.

## Conditional safest Railway execution design
**Candidate:** a separately permissioned, **private, temporary PostgreSQL 17 restoration service inside an EXISTING authorized Railway project**, not a new Railway project, with bounded CPU/RAM/storage and no persistent volume if a disposable ephemeral restoration suffices (a volume requires separate cost confirmation).
**DO NOT CREATE THIS SERVICE YET** until exact incremental billing, source-to-target security controls and owner approval are documented. Railway uses resource-consumption billing; creating a service without an itemized, zero-new-spend confirmation is not permitted.

1. Select the source Supabase project and correct real backup by verified private manifest and exact snapshot time.
2. Owner/Finance obtains Railway workspace **Usage** readback, including remaining included credit, per-service CPU/RAM/disk/egress rates, expected capped runtime and any storage cost; obtain explicit owner approval if incremental charge is possible. **$0 is not yet proven.**
3. Prepare a private PostgreSQL 17 target, with **no Railway public domain, no TCP proxy**, no factory or CRM variables, no app credentials, separate least-privilege role/database, non-superuser restore account, restricted admin entrypoint and only a reviewed operator access method. Do not connect it to a production service or persistent factory branch.
4. Prove **outbound networking is denied or constrained** so restored functions/extensions/triggers/subscriptions cannot contact external systems; private **ingress alone does not prove outbound isolation**. Inhibit background workers, subscriptions, event triggers, external FDWs, nonessential extensions and any job schedulers before restore. An unverified isolation claim blocks actual data.
5. Check real `PGDMP` archive with the new **read-only preflight**, verify checksum/private manifest and PostgreSQL 17/Supabase extension/role compatibility. Plain SQL and encrypted archives require a separate audited method; never improvise `pg_restore` on D1, .age or .7z bytes.
6. After separate explicit **real confidential data transfer + restoration approval**, deliver only the authorized data through an approved private channel with transport protection; owner private key stays on owner device. Do not keep plaintext in public repos, CI outputs or shared services. A low-RAM client can **stream** locally decrypted input to a secured remote instance if approved, rather than materializing the full DB on the laptop.
7. Restore into a **fresh empty** isolated database (never `--clean` against an existing live DB). Use pre-reviewed `pg_restore` TOC; `--no-owner --no-acl --exit-on-error`; confirm every function/extension/role/policy exception before execution. Exact commands, credentials and options must be authorized based on the actual dump format.
8. Run read-only source/restored manifest comparison and `scripts/task1-post-restore-metadata-audit.sql`, plus **separate** exact table/row counts, relation integrity, receipt/assignment hashes, RLS access-denial tests and proof of zero outbound operations. Store redacted evidence privately.
9. Shut down isolated restoration runtime and delete the confidential temporary copy securely under documented retention after required acceptance. Do **not** destroy a proof copy until owner approves the evidence/retention decision.

## Alternative existing resources (no new subscription automatically assumed)
- Existing **persistent Supabase branch** `alpha-first-five-staging` under Alpha Lead Generation is healthy and PG17, but contains real factory structure and records (101 tables). **It is not empty or disposable. Do not overwrite it.**
- Existing **GitHub Actions** runners have passed disposable PostgreSQL 18 recovery CI, but a real confidential backup would require a distinct approved secure data/secret path and assurance of network isolation, job artifact retention, repository access and included-minute cost. **Not approved as the real-data target** and no real data sent to CI.
- Existing **Bliss Bot Railway production PostgreSQL** has physical space but shares a live production cluster and roles; not acceptable as a recovery sandbox without an explicit narrowly documented risk exception, which is **not recommended**.

## Explicit approval gate before any confidential bytes move
**Captain must separately approve ALL of:** exact source Supabase ref and correct encrypted archive/manifest; private target service and identity; quoted additional recurring/one-off cost (or verified zero-cost headroom); no-public-ingress and proven restricted-egress isolation; owner-key handling and protected data transfer; exact PostgreSQL 17 role/extension restoration procedure; evidence retention/cleanup; operator with authority; permission to **transfer and restore real confidential data**. Earlier synthetic/staging approvals and Global STOP instructions do not waive this gate.

**Task #1:** IN PROGRESS. **Real logical restoration:** NOT PERFORMED. **Auth/MFA:** NOT VERIFIED. **Physical backup recovery:** NOT VERIFIED. **No incremental spend authorized or incurred by this engineering inspection.**
