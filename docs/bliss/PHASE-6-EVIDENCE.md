# PHASE 6 — HUMAN REVIEW QUEUE EVIDENCE

**Date:** 2026-09-20  
**Status:** PASS  
**Automated tests:** 70 / 70 passed  
**Hosted migration:** PASS on the dedicated Bliss Supabase PostgreSQL database  
**Alpha Auto coupling:** NONE

Raw evidence: `docs/bliss/evidence/phase6/`

## Delivered boundary

```text
REVIEW_REQUIRED BlissMatch
    → labelled operator decision (APPROVE | REJECT | HOLD)
    → immutable MatchReviewDecision
    → live match status APPROVED | INELIGIBLE | REVIEW_REQUIRED
```

The deterministic evaluator remains the scoring authority. Human review
does not invent scores, discover candidates, run campaigns, or call
Alpha Auto.

## Automated verification

```text
Passed: 70
Failed: 0
Skipped: 0
```

Coverage includes approve, idempotent reject replay, blocked CREATED
Match A, unknown decision codes, queue visibility, routes, and Restrict
foreign keys.

## Migration

Applied:

```text
20260920130043_Phase6HumanReview
```

`Up()` creates `MatchReviewDecisions` only; it has no DROP. Six
migrations are recorded. Regenerated `docs/sql/bliss-supabase-full.sql`
applied twice to a clean PostgreSQL database: six migrations, zero
review decisions in the fictional seed.

## Hosted API proof

| Operation | Result |
| --- | --- |
| Queue before | Two `REVIEW_REQUIRED` matches |
| Approve `Supabase Live Test Creator` match | HTTP 201, status `APPROVED` |
| Exact replay | HTTP 200, same decision ID |
| Approve Phase 1 Match A | HTTP 400; Match A remains `CREATED` |
| Queue after API approve | One remaining item |

## Dashboard proof

The remaining queue item (`Unknown Demographics Creator`) was approved
from the Review queue view against the same hosted API. After the
decision:

- queue count 1 → 0;
- decision ledger holds two `APPROVE` rows;
- match certificate shows `Approved` and score `0.70`;
- PostgreSQL: 2 decisions, 0 `REVIEW_REQUIRED` matches, Match A still `CREATED`.

## Deferred

- authentication / RBAC for reviewers;
- Fishing Fleet / live providers;
- campaign execution, ad delivery, measurement, ledger, payouts;
- Alpha Auto integration or merge.
