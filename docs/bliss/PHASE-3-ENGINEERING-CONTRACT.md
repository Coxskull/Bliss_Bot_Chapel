# PHASE-3-ENGINEERING-CONTRACT

## Scope

Phase 3 delivers **historical evaluation auditability**. Re-evaluating a match still updates **live** eligibility and score-component rows. Each evaluation also **inserts** an immutable `MatchEvaluationRun` with input/output snapshots. Prior runs are never deleted or overwritten.

Phase 3 does not introduce AI, live affiliate networks, payments, discovery (Fishing Fleet), campaign execution, measurement, or ledgers.

## Requirements

| ID | Description |
| --- | --- |
| BLC-P3-HIST-001 | Additive columns on `MatchEvaluationRuns`: `BlissMatchId` (Restrict FK, non-unique), `OutputSnapshot`, `MatchStatus`, `OverallScore`, `ConfidenceScore`. No `DROP TABLE`. |
| BLC-P3-HIST-002 | `POST .../evaluate-rules` inserts one `COMPLETED` run per successful evaluation. Existing runs for that match remain. |
| BLC-P3-HIST-003 | Re-evaluating Match B does not change Match A live rows or Match A evaluation runs. |
| BLC-P3-HIST-004 | Live `EligibilityCheck` / `MatchScoreComponent` rows remain the current evaluation only (replaced on re-eval). History lives on `MatchEvaluationRun`. |
| BLC-P3-HIST-005 | Phase 1 Match A stays `CREATED` on RuleVersion 1 with no evaluation run (no `DocumentJson`). |
| BLC-P3-DATA-001 | Phase 3 seed is additive and idempotent. Phase 1 and Phase 2 keys remain. |
| BLC-P3-DATA-002 | Seed records at least two runs for the Brazil APPROVED match (append-only proof). |
| BLC-P3-API-001 | GET `/api/match-evaluation-runs`, GET by id, GET `/api/bliss/matches/{id}/evaluation-runs`. |
| BLC-P3-SAFE-001 | UNKNOWN demographics are still not treated as zero. AI is not used. |

## Exact tasks

1. Extend `MatchEvaluationRun` additively; EF Restrict delete on `BlissMatchId`.
2. Persist runs from `MatchRuleEvaluationService` with deterministic JSON snapshots.
3. `Phase3DataSeeder` backfills Phase 2 match evaluations into history, then re-evaluates Brazil once more.
4. Read APIs for evaluation runs.
5. Tests and evidence snapshots.

## Forbidden changes

- No AI/ML matching.
- No n8n, Firecrawl, Tavily, Awin, Levanta, Wise, PayPal, Stripe.
- No unique constraint on `BlissMatch.CreatorId` or `CampaignPlacement.ContentItemId`.
- No unique constraint on `MatchEvaluationRun.BlissMatchId`.
- No treating UNKNOWN as zero.
- No production payment or live affiliate calls.
- No Alpha Auto merge.
- No Fishing Fleet, measurement, or ledger.

## Acceptance tests

See `Bliss.Tests` Phase 3 classes. Command: `dotnet test`.

## Evidence

`docs/bliss/PHASE-3-EVIDENCE.md` and `docs/bliss/evidence/phase3/`.

## Definition of DONE

- Contract requirements have tests or live TEST-DB proof.
- Phase 1 CRT-TEST-001 matches still exist with original opportunity IDs.
- Snapshots captured (tests, API JSON, screenshots, TEST SQL).
- No secrets committed.
