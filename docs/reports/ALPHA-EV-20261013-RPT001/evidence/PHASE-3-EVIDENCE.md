# Phase 3 — Academy production workspace evidence

Date: 2026-10-12. Status: implemented and locally validated; review/acceptance
pending before Phase 4. Contract: [Phase 3](PHASE-3-CONTRACT.md).

## Delivered

`/production.html` is linked from the homepage and Brand intake. It opens an
authorized planning workspace, shows the current Brand DNA status, prepares
and resumes saved briefs, accepts private candidate revisions, and displays
human assessment history. New briefs require the latest Brand DNA revision to
be confirmed. Old briefs retain their original direction after later edits.

The existing Academy brief builder supplies production instructions and the
Quality DNA version. Reference selection is restricted to physically available,
ACTIVE records with learning/do-not-copy instructions for the selected family.
An unsupported family is refused rather than mapped to an unrelated teacher.
Each saved brief holds the exact Brand DNA ID and snapshot, reference ID and
metadata snapshot, reference file hash, production instructions, and recipe hash.
No reference approval status or advertiser identity is invented.

Candidate PNG bytes, labels, notes, image hashes, and revision numbers are
durable database records. Images are returned only by workspace-authorized
routes with private/no-store caching, not public file hosting. List/history JSON
does not include image bytes. PNG parsing verifies bounded size/dimensions,
chunk CRCs, header format, compressed scanline lengths, filters, and end marker;
it does not judge visual production quality.

Reviewers can append PASS/REVISE/FAIL observations with four independent gates,
critical-defect flag, rationale, and preserve/repair instructions. PASS is refused
with a failed gate or critical defect. REVISE needs a repair instruction. A new
candidate starts without assessments; previous candidate judgments stay intact.
Advertisers cannot record instructor assessments. Review policy and existing
workspace ownership both apply. Reviewer-only identities without staff access
or an advertiser binding cannot browse other advertisers' workspaces.

Idempotent retries and changed/stale inputs are distinguished. Candidate and
review version indexes arbitrate races. Records and corresponding audit events
commit together. EF update/delete protection covers briefs, candidates, and
reviews. API results always keep campaignReady false and delivery NOT_SENT.

## Automated verification

After sourcing `/workspace/.bliss-tools/activate.sh` and starting local
PostgreSQL, from `/workspace/Bliss_Bot_Chapel`:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj -c Release --no-restore \
  --filter 'FullyQualifiedName~AcademyProduction|FullyQualifiedName~CandidatePng' \
  --logger 'trx;LogFileName=phase3-focused.trx' \
  --results-directory /workspace/.bliss-runtime/development-results

dotnet test Bliss.Tests/Bliss.Tests.csproj -c Release --no-build --no-restore \
  --logger 'trx;LogFileName=phase3-regression.trx' \
  --results-directory /workspace/.bliss-runtime/development-results
```

- Release compilation succeeded.
- Focused suite: **10 passed, 0 failed, 0 skipped**.
- Full regression: **612 passed, 0 failed, 0 skipped**.
- PostgreSQL connection was set. Same-key and different-key candidate/review
  races executed against real isolated schemas, proving single revision and
  single audit outcomes; schemas were removed afterward.
- API coverage: original DNA/reference hash and recipe persistence, reload after
  brand edits, unconfirmed/unsupported refusal, retries/conflicts, private image
  bytes/cache policy, stale candidate/review versions, failed PASS gates,
  advertiser review refusal, cross-tenant/anonymous refusal, invalid uploads,
  CSRF, and preserved candidate/review/audit history.
- Persistence cases reject update/delete for all three production record types.
- PNG cases accept a valid pixel fixture and reject corrupt CRCs, truncation,
  and oversized data. The pixel is a test artifact, not a commercial creative.

The first API test run shared one authenticated rate-limit identity and hit the
existing write quota. Giving each client its own identity fixed test isolation;
the rate-limit configuration and production protections were unchanged.

## Migration validation

Reviewed migration `20261012034137_AcademyProductionWorkspace`: three new tables,
restrict-delete foreign keys, operation-key and revision-number unique indexes.
Applied successfully to the existing fictional development database and as part
of the full migration chain on a fresh temporary PostgreSQL database. The fresh
database was removed after success. EF's pending-model-change check found none.
Production migration has not occurred.

## Real-browser validation

Using the installed Chromium and local Development API backed by PostgreSQL:

```bash
# Start API separately from Bliss.Api after activating tools and applying migration:
# ASPNETCORE_URLS=http://127.0.0.1:5083 dotnet bin/Release/net8.0/Bliss.Api.dll
NODE_PATH=/workspace/.bliss-tools/browser-tests/node_modules \
  CHROMIUM_PATH=/usr/bin/chromium BLISS_SMOKE_BASE=http://127.0.0.1:5083 \
  node scripts/phase3-production-smoke.cjs
```

Passed homepage entry, confirmed Brand DNA brief creation, displayed recipe and
reference provenance, private candidate image decoding, refusal of PASS with a
failed quality gate, explicit REVISE recording, new candidate revision, preserved
old review history, two-tab conflict with input retained, refresh, reload/resume,
safe text rendering, and mobile layout without horizontal overflow. No page
JavaScript errors occurred. The script refuses non-loopback destinations and
enabled hosted authentication; it adds fictional local brand/production records.
Hosted OIDC was not browser-tested; authorization used the existing OIDC test
factory for automated API checks. Playwright tooling is installed outside the
checkout, as documented in the Phase 2 evidence.

## Limits

No AI provider, paid generation, visual scoring model, production deployment,
campaign activation, or outreach was used. Human assessments do not replace
advertiser approval or Alpha campaign QA. Original reference file bytes are not
duplicated into each brief; metadata and hashes record the source used. PNG-only
candidate intake is intentionally bounded; other media formats need a later
contract. Immutability applies to EF application writes, not database admin
access. Local TRX results and logs are under `/workspace/.bliss-runtime`.
