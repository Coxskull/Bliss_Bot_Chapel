# Phase 5 — Creative approval and Bliss handoff evidence

Date: 2026-10-12. Status: implementation and local validation complete;
production deployment and human acceptance are not claimed.
Contract: [Phase 5](PHASE-5-CONTRACT.md).

The user explicitly authorized this increment while live AI validation remains
deferred. Manually submitted candidates can complete this workflow without
activating a provider. Phase 4's outstanding live check remains outstanding.

## Delivered

- `/campaign-handoff.html`, linked from the homepage and Creative Academy,
  resumes private candidates, shows instructor assessments, records human
  approval/withhold decisions, selects approved matching inventory and accepted
  quote lines, and explicitly creates draft campaigns and planned handoffs.
- Candidate-bound append-only decisions supplement existing generic creative
  approvals. Advertiser approval requires the owning advertiser's authenticated
  principal; Alpha QA and inventory fit require review authority. Hosted
  advertiser/QA subjects must differ, even for someone with both roles.
- Exact latest candidate, instructor PASS assessment, confirmed Brand DNA,
  stage approvals, and slot-specific fit evidence are checked before planning.
  FIT records PNG dimensions and requires explicit human dimension/format/safe
  area checks. One-pixel fixtures cannot receive fit approval. No slot geometry
  or reference quality is invented.
- Economics remains the price authority. The current accepted quote must carry
  approval/acceptance evidence and a quantity-one line linked to this exact
  match, opportunity, creator, content and slot. The API takes no price fields.
- The existing placement service stages its validated intent for a single atomic
  commit with placement run, immutable creative lineage and audit. Serializable
  transactions and unique constraints protect concurrent planning and one-time
  quote-line use. Exact retries replay; changed intent conflicts.
- Handoff snapshots retain asset hash, DNA/reference provenance, review/stage
  decisions and subjects, fit dimensions, match/rule evidence, and the accepted
  Economics quote/version/line/amount. Later revocation blocks new planning and
  preserves historical evidence. All handoffs remain PLANNED / NOT_SENT;
  campaigns remain DRAFT, inventory stays available, and dates stay unset.

## Verification

With the retained cloud toolchain activated and PostgreSQL running:

```bash
source /workspace/.bliss-tools/activate.sh
bash /workspace/.bliss-tools/start-db.sh
dotnet build BlissBotChapel.sln -c Release --no-restore
dotnet test BlissBotChapel.sln -c Release --no-build --no-restore \
  --logger 'console;verbosity=minimal'
dotnet ef migrations has-pending-model-changes --project Bliss.Infrastructure \
  --startup-project Bliss.Api --no-build --configuration Release
bash scripts/phase5-free-browser-check.sh
```

Final Release build: **0 warnings, 0 errors**.
Final full regression: **654 passed, 0 failed, 0 skipped** (633 prior cases plus
21 Phase 5 cases). PostgreSQL test connections were present, so database
concurrency tests executed their real database paths.

New cases cover the complete authorized handoff, persisted Economics amounts,
exact retries and conflicting inputs, independent principals, owner/staff/review
permissions, tenant isolation, anonymous and CSRF refusal, missing approvals,
unreviewed/pixel assets, candidate/review/Brand DNA revisions, revocation,
unavailable inventory, incompatible slot and quote lineage, unaccepted/revised
quotes, duplicate quote-line use, draft campaign retries, and immutable evidence.
PostgreSQL races verify one complete placement/run/handoff/audit and no orphan
placement under identical or different planning keys. Concurrent revocations
retain one decision revision and audit while preserving earlier handoff history.

The first full run exposed a Brand DNA read race: an identical writer could
commit between retry-key lookup and version lookup, producing a false conflict.
Version-conflict paths now reconcile a matching committed key. Academy candidate
and assessment writes and Phase 5 decisions use the same reconciliation pattern.
Different input still conflicts. No assertions were relaxed. The subsequent full
Debug run and final full Release run both passed all 654 cases.

## Browser and database evidence

Chromium with a fresh disposable PostgreSQL database passed: private candidate
preview; missing-gate refusal; draft campaign creation; three explicit approval
decisions; explicit PLANNED/NOT_SENT handoff; exact Economics amount; persisted
history after reload; revocation refusal; safe rendering of script-shaped text;
and a 390-pixel viewport without horizontal overflow. No page JavaScript errors
occurred. Temporary API processes and fixture databases were removed.

The image is a synthetic 32 × 16 PNG. Its instructor and approval records are
explicitly fictional authority fixtures, not evidence of finished advertisement
quality. The browser runs with local Development identity; independent hosted
subjects and role boundaries are exercised by authenticated API test identities.
Real OIDC deployment and actual client/QA visual acceptance remain separate.

Stock Economics fixtures contain no overlay pricing anchor. The browser script
adds a clearly labeled TEST / TEST_ONLY overlay benchmark only to its disposable
database, using an existing fixture range. API/concurrency tests use the same
explicit fictional fixture. Production seed data and pricing formulas are
unchanged. Real handoffs require a supported Economics recommendation and an
approved, accepted quote; the handoff cannot fill a missing benchmark.

Migration `20261012071804_CreativeCampaignHandoff` adds two tables with lineage
foreign keys, decision version/retry indexes, and a unique quote-line constraint.
It was applied to the existing local development database and fresh browser
databases. Reapplying reported no pending migrations; EF's model check reported
no pending model changes. Production migration has not occurred.

The final development API was also checked for healthy database readiness,
the handoff page and scoped options, NOT_SENT delivery, and liveCallsEnabled=false.
External AI requests: **0**. External spend: **$0**.

Logs are retained outside the checkout under `/workspace/.bliss-runtime/`:
`phase5-build.log`, `phase5-regression-final.log`, `phase5-model-check.log`,
`phase5-migrate.log`, `phase5-migrate-repeat.log`, `phase5-browser-final.log`,
and `phase5-fixture-39IOzU/`. The final development instance writes `phase5-api.log`.

## Remaining scope

Manual advertiser and independent QA acceptance of actual creative, real slot
geometry checks, supported real Economics anchors, and hosted OIDC/deployment
validation are not established by fictional local tests. Phase 4 paid provider
activation remains deferred under the user's OpenAI-later preference. This phase
does not reserve, schedule, activate or send campaigns. Proposed Phase 6 covers
measurement lineage and operating readiness under a separate authorization.
