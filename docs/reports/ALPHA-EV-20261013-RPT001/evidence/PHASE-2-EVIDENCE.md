# Phase 2 — Brand DNA and resumable intake evidence

Date: 2026-10-12. Status: development and local validation complete;
review/acceptance pending before Phase 3.
Contract: [Phase 2](PHASE-2-CONTRACT.md). Roadmap: [delivery phases](ROADMAP.md).

## Delivered behavior

- The public homepage links to `/brand-intake.html`. Authenticated advertiser
  accounts use their bound advertiser; chapel staff can select an advertiser.
  The backend independently checks workspace ownership on every request.
- Partial brand intake saves as a new DRAFT revision and resumes from the
  database. History displays every previous version using text rendering.
- A separate checkbox and confirmation action records CONFIRMED brand
  direction. Required fields are validated by the server. Further edits append
  new drafts; they do not erase or change confirmed versions.
- Expected-version checks prevent stale saves and confirmations. The browser
  preserves unsaved text on conflicts and requires reload/review. Identical
  retry keys replay; changed payloads conflict. Concurrent PostgreSQL writers
  are resolved by unique workspace/version and workspace/key constraints.
- Revision and audit insertion use one EF SaveChanges transaction. Audit actor
  identity is server-resolved. Revision update/delete is refused by EF, and
  the API exposes only append and read operations.

## Validation

From `/workspace/Bliss_Bot_Chapel`, source
`/workspace/.bliss-tools/activate.sh` and start the local PostgreSQL cluster.

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj -c Release --no-restore \
  --filter 'FullyQualifiedName~BrandDna' \
  --logger 'trx;LogFileName=phase2-focused.trx' \
  --results-directory /workspace/.bliss-runtime/development-results

dotnet test Bliss.Tests/Bliss.Tests.csproj -c Release --no-restore \
  --logger 'trx;LogFileName=phase2-regression.trx' \
  --results-directory /workspace/.bliss-runtime/development-results
```

Focused tests: **6 passed, 0 failed, 0 skipped**. Full regression: **602 passed,
0 failed, 0 skipped**. Release compilation succeeded. PostgreSQL connection
was set, so both concurrent brand-save cases and the existing concurrency test
executed their database paths. API tests cover draft/confirmation history,
required fields, retries, stale versions, field length,
server-resolved actor, audit counts, anonymous/cross-tenant rejection, CSRF,
and read-only viewer refusal. Persistence tests cover forbidden update/delete.

Migration `20261012032330_WeddingPlannerBrandDna` was reviewed and applied to
the existing fictional development database. All migrations also succeeded on
a fresh temporary PostgreSQL database, which was then removed. EF's
`migrations has-pending-model-changes` reported no outstanding model changes.
The migration is additive: a new table and unique indexes, with a restricted
workspace foreign key. No existing data was rewritten. Production application
of this migration has not occurred.

## Browser workflow

The checked-in smoke test ran against the local Development API and real
PostgreSQL using the installed Chromium browser. Example reproduction:

```bash
# Install browser tooling outside the checkout if unavailable.
npm install --cache /workspace/.bliss-tools/npm-cache \
  --prefix /workspace/.bliss-tools/browser-tests playwright

# Start the API separately from Bliss.Api after migration and activation.
# ASPNETCORE_URLS=http://127.0.0.1:5082 dotnet bin/Release/net8.0/Bliss.Api.dll
NODE_PATH=/workspace/.bliss-tools/browser-tests/node_modules \
  CHROMIUM_PATH=/usr/bin/chromium BLISS_SMOKE_BASE=http://127.0.0.1:5082 \
  node scripts/phase2-brand-intake-smoke.cjs
```

Passed: homepage entry, partial save, reload/resume, required-field confirmation
refusal, complete save, explicit confirmation, ordered version history, user
content rendered as text, two-tab stale conflict, preservation of entered text,
reload of newer saved direction, and mobile layout without horizontal overflow.
No browser JavaScript errors occurred. The smoke test adds fictional brand
revisions to the selected local development advertiser; it refuses remote hosts
and enabled hosted authentication.

Playwright's browser download was blocked by network policy. Existing
`/usr/bin/chromium` successfully ran the test, so no policy change was needed.
OIDC and tenant boundaries were exercised in automated API tests with the
existing test authentication handler; a hosted identity-provider browser login
was not exercised.

## Limits

Brand confirmation approves direction only. It does not approve a creative,
select a paid provider, activate a campaign, or send anything. No AI calls or
production deployment occurred. Academy production integration belongs to
Phase 3. Append-only protection applies to application EF writes; database
administrator access is not restricted by this change. TRX and diagnostic logs
are retained under `/workspace/.bliss-runtime` outside the source checkout.
