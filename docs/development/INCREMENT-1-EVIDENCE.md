# Delivery increment 1 — message integrity evidence

Date: 2026-10-12. Status: Phase 1 development and local validation complete;
acceptance pending review.
Contract and next increments: [development roadmap](ROADMAP.md).

## Change

Wedding Planner used to replay an old message when a caller reused its source
system and idempotency key with different text or actor type. The caller could
receive success even though their changed note was not saved. The service now
raises a dedicated conflict and the API returns HTTP 409 with an instruction
to use a new key. Identical normalized requests still replay successfully.

Existing ownership and actor validation run first. A key belonging to another
session still returns 404. No migration or matching changes were necessary.

## Verification

Executed from `/workspace/Bliss_Bot_Chapel` after sourcing
`/workspace/.bliss-tools/activate.sh` and starting the local PostgreSQL cluster.

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj -c Release --no-restore \
  --filter 'FullyQualifiedName~WeddingPlannerPhase1' \
  --logger 'trx;LogFileName=phase1-integrity.trx' \
  --results-directory /workspace/.bliss-runtime/development-results

dotnet test Bliss.Tests/Bliss.Tests.csproj -c Release --no-build --no-restore \
  --logger 'trx;LogFileName=development-regression.trx' \
  --results-directory /workspace/.bliss-runtime/development-results
```

- Release compilation succeeded.
- Focused suite: 9 passed, 0 failed, 0 skipped.
- Full suite: 596 passed, 0 failed, 0 skipped. This includes existing Bliss,
  Creative Academy, Wedding Planner tenant-isolation, and PostgreSQL proofs.
- `BLISS_TEST_POSTGRES_CONNECTION` was set to the local test database, so the
  existing PostgreSQL concurrency test executed its database path.
- Four added cases cover changed text and changed actor at persistence and
  HTTP boundaries. They verify preserved message contents, unchanged session
  timestamp and audit count, normalized exact replay, new-key append sequence,
  and 404 for a key used in another session.
- New API fixtures use independent advertisers. The initial focused run
  exposed a shared-fixture dependency; after isolating their records the
  focused and full suites passed.
- Diff whitespace check passed with `core.whitespace=cr-at-eol`, preserving
  the existing C# files' CRLF line endings.

TRX results and command logs are local generated evidence outside the checkout.

## Phase 1 completion check

After the user's instruction to proceed to Phase 1, the current checkout was
rebuilt and its full regression suite rerun: **596 passed, 0 failed, 0 skipped**.
Results: `/workspace/.bliss-runtime/development-results/phase1-final.trx`.

A separate Development API process connected to the local PostgreSQL database
was exercised using a new fictional planning session. Direct HTTP requests
verified database readiness, initial message creation (201), changed-text and
changed-actor conflicts (409), identical replay (200), preservation of the
original message, and unchanged audit count after the rejected retries and
replay. Results: `/workspace/.bliss-runtime/development-results/phase1-live.json`.
This validates the real Npgsql/database path in addition to the in-memory API
and persistence regression cases. The local smoke check adds fictional session
and message records to the development database only.

No live provider calls, production deploy, campaign activation, or outreach
were performed. This change does not solve concurrent message append retries;
that needs a separate transaction/recovery contract. Review and acceptance of
this increment precede implementation of the next roadmap increment.
