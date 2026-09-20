# PHASE 6 — HUMAN REVIEW QUEUE

**Status:** Authorized  
**Boundary:** Bliss Bot Chapel only  
**Prerequisites:** Phases 1–5 accepted

## Objective

Give an operator an explicit, auditable way to decide `REVIEW_REQUIRED`
match certificates. The deterministic evaluator remains the scoring
authority. Human review records a labelled operator decision; it does
not invent scores, discover candidates, or execute campaigns.

## Requirements

| ID | Requirement |
| --- | --- |
| BLC-P6-QUEUE-001 | `GET /api/match-reviews/queue` lists current `REVIEW_REQUIRED` matches. |
| BLC-P6-DEC-001 | `POST /api/match-review-decisions` records `APPROVE`, `REJECT`, or `HOLD`. |
| BLC-P6-DEC-002 | `(SourceSystem, IdempotencyKey)` is unique; exact replay writes nothing. |
| BLC-P6-DEC-003 | `APPROVE` sets match status `APPROVED`. `REJECT` sets `INELIGIBLE`. `HOLD` keeps `REVIEW_REQUIRED`. |
| BLC-P6-DEC-004 | Only `REVIEW_REQUIRED` matches accept a new decision. |
| BLC-P6-DEC-005 | `APPROVE` and `REJECT` require an existing `MatchEvaluationRun` for that match. |
| BLC-P6-DEC-006 | Prior decision rows are never deleted or overwritten. A later distinct key may decide the match again only after it returns to `REVIEW_REQUIRED`. |
| BLC-P6-DEC-007 | Every accepted request stores reviewer label, rationale, and an input snapshot. |
| BLC-P6-DEC-008 | Collection and detail GET APIs expose decisions. |
| BLC-P6-UI-001 | The dashboard shows the review queue and can submit a labelled decision. |
| BLC-P6-SAFE-001 | Invalid input creates no decision and does not change match status. AI is not a reviewer. |

## Persistence contract

`MatchReviewDecision` records:

- `BlissMatchId`, `CreatorId`, optional `MatchEvaluationRunId`;
- normalized `SourceSystem` and caller-provided `IdempotencyKey`;
- `ReviewerLabel`, `Decision`, `ResultingMatchStatus`, `Rationale`;
- `Status`, `StartedAt`, `CompletedAt`, and JSON `InputSnapshot`.

Foreign keys use `ON DELETE RESTRICT`. The unique index is only
`(SourceSystem, IdempotencyKey)`.

## API behavior

- First accepted request: HTTP 201 and `isReplay=false`.
- Idempotent replay: HTTP 200 and `isReplay=true`.
- Match not `REVIEW_REQUIRED`, missing match, missing evaluation for
  approve/reject, or unknown decision: HTTP 400 and no writes.

## Acceptance

1. A `REVIEW_REQUIRED` match appears in the queue.
2. Approve moves that match to `APPROVED` and appends one decision.
3. Exact replay leaves decision count and match status unchanged.
4. Reject of a second `REVIEW_REQUIRED` match sets `INELIGIBLE`.
5. Created/unevaluated Match A cannot be approved.
6. Phase 1–5 rows remain intact.
7. Automated tests, PostgreSQL migration, API requests, and dashboard
   evidence pass.

## Forbidden

- No AI reviewer, scoring override engine, or automatic bulk decisions.
- No Fishing Fleet, scraping, provider API, n8n, or live affiliate call.
- No campaign execution, ad delivery, measurement, ledger, payout, or payment.
- No Alpha Auto import, schema, credentials, integration, or merge.
- No destructive migration or rewrite of evaluation/formation history.
- No authentication product (reviewer is a labelled TEST operator string).
