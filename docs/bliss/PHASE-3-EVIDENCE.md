# PHASE 3 — EVIDENCE

**Date:** 2026-09-20  
**Status:** PASS  
**Tests:** 43 / 43 passed (0 failed, 0 skipped)  
**Migration:** PASS (additive `MatchEvaluationRuns` columns + Restrict FK on `BlissMatchId`)  
**Phase 1 preservation:** PASS (Match A still CREATED on RuleVersion 1 / Opportunity A; no evaluation run)

Snapshots: `docs/bliss/evidence/phase3/`

## What Phase 3 is

Historical evaluation auditability. Each successful deterministic evaluation **inserts** a `MatchEvaluationRun` with input/output JSON snapshots. Re-evaluation replaces live `EligibilityCheck` / `MatchScoreComponent` rows but **does not delete** prior runs.

## What Phase 3 is not

AI matching, n8n, live affiliates, payments, Fishing Fleet, campaign execution, ad rendering, measurement, ledger.

## TEST database

Local `bliss_phase1_test` only. Applied `20260920073126_Phase3EvaluationAudit` (`AddColumn` + non-unique index + Restrict FK). No DROP in `Up()`.

Connection string is not committed. Operator used `--connection` / env `ConnectionStrings__DefaultConnection` against TEST only.

After seed (before extra live POST):

| Table | Count |
| --- | --- |
| Creators | 3 |
| Advertisers | 2 |
| Opportunities | 5 |
| BlissMatches | 8 |
| AdInventorySlots | 8 |
| RuleVersions | 2 |
| EligibilityChecks | 21 |
| MatchScoreComponents | 13 |
| MatchEvaluationRuns | 5 |

Match A (`…7771`) remains Opportunity A + RuleVersion 1.0.0 + CREATED + **zero** evaluation runs.

Seeded runs on RuleVersion 2.0.0:

| Run | Match | Status |
| --- | --- | --- |
| …eee1 | …7775 PH→BR | INELIGIBLE |
| …eee2 | …7776 BR→BR | APPROVED (first history) |
| …eee3 | …7777 unknown demographics | REVIEW_REQUIRED |
| …eee4 | …7778 PH→Harbor PH | APPROVED |
| …eee5 | …7776 BR→BR | APPROVED (second history) |

`IX_MatchEvaluationRuns_BlissMatchId` is **not unique**. FKs use `ON DELETE RESTRICT`.

Live POST `evaluate-rules` on geo-fail (`…7775`) appended a sixth run. Brazil still has two runs. Seeded run `…eee1` remains.

## API

- GET `/api/match-evaluation-runs`
- GET `/api/match-evaluation-runs/{id}` (includes `inputSnapshot` / `outputSnapshot`)
- GET `/api/bliss/matches/{id}/evaluation-runs`
- POST `/api/bliss/matches/{id}/evaluate-rules`
  - Match A → HTTP 400 (no DocumentJson on v1)
  - Geo-fail → HTTP 200 INELIGIBLE; run count 1 → 2

JSON: `docs/bliss/evidence/phase3/api/`  
Screenshots: `docs/bliss/evidence/phase3/screenshots/`

## Tests

Command: `dotnet test BlissBotChapel.sln`  
43 passed. Log: `docs/bliss/evidence/phase3/tests/dotnet-test.txt`

## Recommended next

Do not start Fishing Fleet, live money, AI engines, measurement/ledger, or Alpha Auto merge until a later contract is issued.
