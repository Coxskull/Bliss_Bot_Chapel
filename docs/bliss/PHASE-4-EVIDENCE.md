# PHASE 4 — CONTROLLED CREATOR INGESTION EVIDENCE

**Date:** 2026-09-20  
**Status:** PASS  
**Tests:** 56 / 56 passed (0 failed, 0 skipped)  
**Migration:** PASS on local `bliss_phase1_test` only  
**Alpha Auto coupling:** NONE

Raw evidence: `docs/bliss/evidence/phase4/`

## Delivered boundary

Phase 4 adds provider-neutral creator observations, not a live crawler:

- canonical identity = normalized platform + provider-owned external profile ID;
- idempotent source request handling;
- create/update of one canonical Creator and CreatorPlatform;
- immutable ingestion-run input snapshots;
- field-level DataProvenance;
- API and dashboard visibility.

There are no live provider, scraping, AI, n8n, affiliate, payment, campaign, measurement, or Alpha Auto calls.

## TEST PostgreSQL migration

Applied:

```text
20260920102946_Phase4CreatorIngestion
```

Additive changes:

- nullable `CreatorPlatforms.IdentityKey`;
- backfill from existing platform/external ID;
- filtered unique index on non-null IdentityKey;
- new `CreatorIngestionRuns`;
- Restrict FKs to Creator and CreatorPlatform;
- unique `(SourceSystem, IdempotencyKey)`.

`Up()` has no DROP operation. Phase 1 Match A remains on its original opportunity and RuleVersion with `CREATED` status.

## Identity and idempotency proof

Dashboard observations:

1. First `YOUTUBE::CRT-P4-ES-001` observation → `CREATED`.
2. Second observation with a new idempotency key and explicit ES/Spanish fields → `UPDATED`.
3. Creator directory still contains exactly one “Phase 4 Creator Spain”.
4. The second immutable input snapshot contains `"countryCode": "ES"` and `"primaryLanguage": "Spanish"`.

API replay:

- exact replay returned HTTP 200 and `isReplay: true`;
- run count remained unchanged;
- returned the original run and creator IDs.

Cross-platform identity:

- `YOUTUBE::CRT-P4-ES-001` and `PODCAST::CRT-P4-ES-001` resolve to different Creator IDs;
- zero duplicate non-null IdentityKey groups;
- podcast observation omitted metrics, which persisted as SQL NULL.

Invalid input:

- negative audience returned HTTP 400;
- run count remained unchanged.

## TEST database snapshot

After evidence requests:

| Table | Count |
| --- | ---: |
| Creators | 5 |
| CreatorPlatforms | 4 |
| CreatorIngestionRuns | 3 |
| DataProvenances | 25 |
| BlissMatches | 8 |
| MatchEvaluationRuns | 7 |

The three ingestion runs are:

- dashboard `CREATED` for YouTube identity;
- dashboard `UPDATED` for the same identity and same Creator;
- evidence fixture `CREATED` for the distinct Podcast identity.

Full SQL: `docs/bliss/evidence/phase4/database.txt`.

## API

- `POST /api/creator-ingestions`
- `GET /api/creator-ingestions`
- `GET /api/creator-ingestions/{id}`

Raw responses under `docs/bliss/evidence/phase4/api/`.

## Dashboard

The Creator ingest view:

- generates an idempotency key;
- submits controlled observations;
- states that it does not crawl or call providers;
- preserves blank values as UNKNOWN;
- lists recent observations;
- exposes canonical identity and immutable input snapshot.

Screenshots: `docs/bliss/evidence/phase4/screenshots/`.

## Product separation

`docs/bliss/evidence/phase4/separation.txt` confirms:

- only Bliss projects are referenced;
- no Alpha namespace appears in C# or project files;
- no Alpha repository, schema, user/order/finance model, or credential is used.

Bliss Bot Chapel and Alpha Auto remain separate products and bounded contexts.

## Not started

- live Fishing Fleet adapters/crawlers;
- human review queue;
- campaign engine extension;
- ad delivery;
- measurement or media ledger;
- live money;
- Alpha Auto integration or merge.
