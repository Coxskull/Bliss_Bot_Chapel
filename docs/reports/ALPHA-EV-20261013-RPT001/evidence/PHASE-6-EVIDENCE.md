# Phase 6 — Measurement and operating readiness evidence

Date: 2026-10-12 (Asia/Taipei). Status: implementation and local validation
complete. Hosted acceptance remains outstanding. Contract:
[Phase 6](PHASE-6-CONTRACT.md). Recommended deployment and recovery procedure:
[Azure hosted runbook](PHASE-6-HOSTED-RUNBOOK.md).

The user authorized Phase 6 and selected our recommendation: Azure App Service,
Microsoft Entra External ID, and Azure Database for PostgreSQL Flexible Server.
Initial targets are 14-day retention, RPO ≤ 1 hour and RTO ≤ 4 hours. They are
operational targets awaiting hosted validation. No Azure resources were created,
and Phase 4's deferred live AI check was not activated.

## Delivered

- `/campaign-measurement.html`, linked from the homepage and handoff workspace,
  resumes handoffs, records observations, proposes experiments, retains research
  review history, and exposes operating readiness to authorized staff.
- Observations copy the immutable handoff snapshot and its SHA-256, preserving
  asset, Brand DNA/reference, assessment, human approvals, match/rule, inventory
  and accepted quote lineage even after current Brand DNA changes.
- LOCAL_VALIDATION and ADVERTISER_FEEDBACK accept no campaign traffic metrics.
  EXTERNAL_REPORT requires a source, evidence, UTC window, nonnegative counts
  and explicit acknowledgment, and remains UNVERIFIED_REPORT. Blank metrics
  remain unknown; no revenue, ROI, performance verification or delivery is inferred.
- Owning advertisers can record feedback; staff can record other observations
  and propose research. Research reviews require reviewer/admin authority,
  workspace access and a different authenticated subject from the author.
  ACCEPT_FOR_RESEARCH, REQUEST_REVISION and REJECT are append-only decisions.
  Applying proposals to production is refused; original records stay PROPOSED
  and review history supplies the latest research decision.
- Observation/proposal/review retry keys bind normalized input and server
  identity. Exact retries replay; changed input and stale versions conflict.
  Record and audit commit together. Stored history cannot be edited or deleted
  through EF. The services do not mutate matching, pricing or delivery authority.
- Staff-only `/api/operations/campaign-readiness` checks the current database,
  pending migrations and PostgreSQL connection TLS. It reports existing OIDC,
  certificate policy, session key, proxy and backup declarations separately.
  Development exemptions and configured declarations never claim hosted acceptance.
  The report contains no raw connection string, credential or secret-file path.

## Automated validation

With `/workspace/.bliss-tools/activate.sh` activated and local PostgreSQL running:

```bash
dotnet build BlissBotChapel.sln -c Release --no-restore
dotnet test BlissBotChapel.sln -c Release --no-build --no-restore \
  --logger 'console;verbosity=minimal'
dotnet ef migrations has-pending-model-changes --project Bliss.Infrastructure \
  --startup-project Bliss.Api --no-build --configuration Release
bash scripts/phase6-recovery-check.sh
```

Release build: **0 warnings, 0 errors**.
Focused Phase 6 tests: **19 passed**.
Full regression: **673 passed, 0 failed, 0 skipped** (654 prior cases plus 19 new
cases). PostgreSQL test connections were present; the concurrency paths used
real isolated PostgreSQL schemas.

New cases verify frozen attribution across later Brand DNA changes; replay and
changed-input conflicts; independent research review; persistent unverified
metric status; unchanged matching rules, scores, recommendation amounts, quote
state and draft campaign/delivery state; missing consent/window/counts; reversed
windows; negative/excess counts; future observation times; local traffic refusal;
required sources; tenant/role/anonymous/CSRF boundaries; staff-only readiness;
immutable records; and concurrent observation/proposal/review writes with exactly
one record and audit per accepted intent. Different concurrent review keys cannot
claim the same decision version.

Existing production-posture, OIDC, TLS-policy, protected-key, request logging,
health, throttling and provider privacy tests ran in the full regression suite.
Their configuration/test-double evidence is not a login or TLS connection to the
recommended Azure services.

## Browser and recovery drill

The drill starts a fresh application database with all migrations and fictional
Phase 5 handoff data. It uses the explicitly labeled TEST_ONLY overlay Economics
benchmark in the disposable database; production seed data and formulas are
unchanged. The 32 × 16 image and manual review/approval records are synthetic
workflow fixtures, not evidence of finished creative quality or actual traffic.

Chromium passed the complete Phase 5 handoff flow, followed by the Phase 6
observation → proposal → research review flow. It verified frozen lineage,
unknown traffic values, reload persistence, script-shaped text rendered safely,
staff readiness, UP_TO_DATE migrations, pending hosted acceptance, and a 390-pixel
viewport without overflow. No page JavaScript errors occurred. Local Development
identity was used; independent hosted identities are covered by API tests.

After stopping fixture writes, the drill made a private PostgreSQL custom-format
dump and restored into a separately created local database. Counts and ordered
record digests matched across **15 tables**: Brand DNA, production briefs,
candidates, instructor reviews, candidate approvals, handoffs, placements,
placement runs, quotes, quote versions, quote lines, observations, learning
proposals, learning reviews and Wedding Planner audit events. Restored candidate
bytes are included in the table digest. PostgreSQL restore preserved schema and
foreign-key constraints.

The application restarted against the restored database and the browser resumed
the same observation, frozen lineage, proposal and ACCEPT_FOR_RESEARCH decision.
Database and migration readiness were healthy. Measured local recovery from
creation of the restore target through the restored browser check: **10 seconds**.
There were no writes after the source snapshot; this proves fixture recovery,
not hosted PITR, production volume, session-key recovery or a measured hosted RPO.

The script refused a remote PGHOST with exit code 2 before database operations.
Only its own newly created databases were removed, its dump was deleted, and
logs/manifests were retained with private permissions. No external AI/provider
calls were made and external AI spend remained **$0**.

## Migration and remaining hosted work

Migration `20261012075444_CampaignMeasurementLineage` adds observations, proposals
and review tables with restricted lineage foreign keys, unique request keys and
unique proposal/review versions. Fresh application databases accepted the full
migration chain. The existing local development database was upgraded and repeat
application was checked. EF reports no pending model changes.

The final development instance also served the measurement page and scoped
history successfully. Database readiness was Healthy, schema UP_TO_DATE,
request correlation headers were present, and liveCallsEnabled=false. PostgreSQL
reported no TLS on the local development connection; the readiness report
retained hostedAcceptanceClaimed=false.

The current process had no Azure subscription/tenant/app bindings, hosted OIDC
bindings, hosted database secret-file binding, mounted key certificate, or backup
declaration. The runbook documents the exact configuration and secure secret
bindings required. Hosted OIDC, ingress, VerifyFull TLS, persistent session keys,
durable logs/alerts, actual backup retention and measured RPO/RTO remain unrun
until those services are available. Infrastructure costs have not been approved
or incurred by this implementation. The user's future $200 OpenAI cap remains a
separate AI setting, not an Azure infrastructure budget.

Logs outside the checkout: `/workspace/.bliss-runtime/phase6-build.log`,
`phase6-focused.log`, `phase6-regression.log`, `phase6-model-check.log`,
`phase6-migrate.log`, `phase6-migrate-repeat.log`, `phase6-recovery.log`, and
`phase6-fixture-pYR3wa/` (source/restored manifests and API/migration logs).
The final development instance writes `phase6-api.log`.
