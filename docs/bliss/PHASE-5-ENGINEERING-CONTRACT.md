# PHASE 5 — CONTROLLED BLISSMATCH FORMATION

**Status:** Authorized  
**Boundary:** Bliss Bot Chapel only  
**Prerequisites:** Phases 1–4 accepted

## Objective

Close the controlled workflow gap between a canonical Creator and the existing
deterministic matching pipeline. An operator may form a `BlissMatch` for a
known Creator, AdvertiserOpportunity, and RuleVersion, with immutable audit
evidence and idempotent replay.

This phase does not discover candidates. It accepts explicit identifiers and
does not make external calls.

## Requirements

| ID | Requirement |
| --- | --- |
| BLC-P5-MATCH-001 | `POST /api/bliss/matches` creates a `BlissMatch` from explicit Creator, AdvertiserOpportunity, and RuleVersion identifiers. |
| BLC-P5-MATCH-002 | `(SourceSystem, IdempotencyKey)` is unique; exact replay returns the original run and match without duplicate writes. |
| BLC-P5-MATCH-003 | All referenced rows must exist. The opportunity and RuleVersion must be active. |
| BLC-P5-MATCH-004 | Creator cardinality remains one-to-many. Do not add uniqueness to `BlissMatch.CreatorId` or to the creator/opportunity pair. |
| BLC-P5-MATCH-005 | `EvaluateOnCreate=true` requires a RuleVersion document and invokes the existing deterministic evaluator. |
| BLC-P5-MATCH-006 | Every accepted request writes an immutable `MatchFormationRun.InputSnapshot`. |
| BLC-P5-MATCH-007 | Formation runs are visible through collection and detail GET APIs. |
| BLC-P5-MATCH-008 | The dashboard can form and evaluate a match for a Phase 4 ingested creator. |
| BLC-P5-SAFE-001 | Invalid input creates no match or formation run. UNKNOWN creator fields remain null and are never coerced to zero. |

## Persistence contract

`MatchFormationRun` records:

- `BlissMatchId`, `CreatorId`, `AdvertiserOpportunityId`, `RuleVersionId`;
- normalized `SourceSystem` and caller-provided `IdempotencyKey`;
- `Status`, `Outcome`, `EvaluateOnCreate`;
- `StartedAt`, `CompletedAt`, and the normalized JSON `InputSnapshot`.

Foreign keys use `ON DELETE RESTRICT`. The unique idempotency index is only
`(SourceSystem, IdempotencyKey)`. Formation does not prohibit multiple match
certificates for one creator or repeated creator/opportunity combinations.

## API behavior

- First accepted request: HTTP 201 and `isReplay=false`.
- Idempotent replay: HTTP 200 and `isReplay=true`.
- Invalid/missing/inactive references: HTTP 400 and no writes.
- `EvaluateOnCreate=false`: match remains `CREATED`.
- `EvaluateOnCreate=true`: response reflects deterministic evaluation and one
  append-only `MatchEvaluationRun`.

## Acceptance

1. A controlled Phase 4 creator can receive a new match certificate.
2. Replay leaves match, formation-run, evaluation-run, eligibility, and score
   counts unchanged.
3. A second idempotency key may form another match for the same creator.
4. Missing references and a document-less evaluation request persist nothing.
5. Existing Phase 1 Match A and all Phase 1–4 rows remain intact.
6. Automated tests, PostgreSQL migration, API requests, and dashboard evidence
   pass.

## Forbidden

- No Fishing Fleet, scraping, background crawler, provider API, n8n,
  Firecrawl, Tavily, or live affiliate call.
- No AI identity resolution, matching, scoring, or decision authority.
- No automatic candidate selection or bulk creator re-evaluation.
- No human review decision workflow; that remains a later phase.
- No campaign execution, ad delivery, measurement, ledger, payout, or payment.
- No Alpha Auto import, schema, credentials, integration, or merge.
- No destructive migration or deletion/rewriting of Phase 1–4 audit history.
