# Phase 4 — Controlled AI workers evidence

Date: 2026-10-12. Status: implementation and zero-cost local validation complete.
**Live-provider acceptance is outstanding**, deferred by the user's free-now,
OpenAI-later preference. Contract: [Phase 4](PHASE-4-CONTRACT.md).
Configuration and future requirements: [provider setup](PHASE-4-PROVIDER-SETUP.md).

## Delivered

- `/ai-workspace.html` exposes provider readiness, session creation/selection,
  sharing consent, text/image requests, private run history, cancellation, and
  explicit image attachment to Creative Academy.
- Concierge context is confirmed Brand DNA, one current human message, and at
  most one explicitly selected previous successful reply from that session.
  Production Artist context is the saved brief recipe only. No tools execute,
  reference images are not sent, and prices/approval/delivery remain outside
  worker authority. Text replies remain draft run outputs; the human-message
  endpoint still refuses fabricated PLANNER actors.
- Gateway and OpenAI adapters enforce HTTPS configuration, disabled redirects,
  bounded requests/output, and safe error codes. They do not retry HTTP calls.
- Runs, events, daily call/USD reservations, and lifetime USD reservations are
  durable. Serializable reservation transactions and concurrency-protected
  claims prevent duplicate charges under the same key. Changed inputs conflict.
  The browser preserves keys across reload using hashed fingerprints, and a
  fresh request requires an explicit action and renewed sharing consent.
- Cancellation, timeout, provider failure, interrupted hosts, and expired leases
  have distinct outcomes. Unknown costs stay unknown; reservations are retained.
  Returned receipts are preserved even if cancellation discards the output.
  Over-reservation reports block further queueing and queued dispatch.
- Images stay private run drafts until a human attaches one as an unreviewed
  candidate. This does not perform instructor assessment or approve a campaign.
- Stored input/context/reservation fields cannot be edited or deleted through
  EF. Events are append-only; usage counters cannot be deleted or reduced.

## Verification

After activating `/workspace/.bliss-tools/activate.sh` and starting PostgreSQL:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj -c Release --no-restore \
  --filter 'FullyQualifiedName~ControlledAi' \
  --logger 'trx;LogFileName=phase4-focused.trx' \
  --results-directory /workspace/.bliss-runtime/development-results

dotnet test Bliss.Tests/Bliss.Tests.csproj -c Release --no-restore \
  --logger 'trx;LogFileName=phase4-regression.trx' \
  --results-directory /workspace/.bliss-runtime/development-results
```

Full regression: **633 passed, 0 failed, 0 skipped**. This includes 21 new
Controlled AI cases: text/image adapter contracts for both protocols, safe
provider errors/tool refusal, context privacy, idempotent dispatch, daily/total
budget and call limits, cancellation, timeout, failure, crash recovery, immutable
inputs/usage, reported-cost anomaly, cancellation receipt preservation, tenant/
anonymous/CSRF boundaries, private image output, explicit candidate attachment,
disabled-provider refusal, and real PostgreSQL reservation/claim races.

PostgreSQL was enabled for the concurrency cases. Tests exposed wrapped
serialization errors and an event-index collision before the run's conditional
update; both now reconcile as reservation/claim conflicts without a second call.
The first full run found a stopped local PostgreSQL process; it was restarted.
A subsequent run exposed EnsureCreated's table-presence shortcut across parallel
test schemas. Known-empty isolated schemas now use the provider's explicit
CreateTables operation in all PostgreSQL fixtures; no assertions were disabled.
The final full suite passed with the actual database path enabled.

## Real browser and TLS fixture

`bash scripts/phase4-free-browser-check.sh` passed with Chromium, real PostgreSQL,
and an explicitly labeled local HTTPS gateway test double. Checked consent,
durable text/image outputs, reload-safe replay, candidate attachment, running
cancellation, retained reservations, fresh-request consent reset, and mobile
layout. No page JavaScript errors occurred. Test-local CA trust preserved TLS
verification. Fixture output is not a real model result.

External model requests: **0**. External model spend: **$0**. The local fixture
reported zero USD; cancellations without receipts remained UNRECORDED.
Temporary test databases and processes were removed. Logs are retained under
`/workspace/.bliss-runtime/phase4-fixture-*` outside the checkout.

Migration `20261012040219_ControlledAiWorkers` was applied to the existing local
development database and to fresh isolated browser-test databases. It adds four
tables with foreign keys, request/event indexes, and concurrency fields. EF's
pending-model-change check passed. Production migration has not occurred.
The normal development instance was separately checked: healthy database,
liveCallsEnabled=false, textReady=false, imageReady=false.

## Saved configuration and remaining requirement

Saved a configuration draft containing inactive future OpenAI endpoints/provider
and a **$200 lifetime reservation cap**. Enabled remains false. No credential or
model selection was added, no runtime activation was performed, and publication
was not claimed. Review/save and publication occur through environment settings.

Cursor's free IDE usage does not supply a callable runtime provider here.
An authorized live AI check therefore remains blocked/deferred: future OpenAI
model selection, secure token binding/network destination, justified per-call
and daily limits, independent provider billing cap, and explicit activation are
still needed. No key should be supplied in chat. Phase 4 is not fully accepted
until that check succeeds; this document does not claim live AI behavior.
