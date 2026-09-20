# PHASE-2-ENGINEERING-CONTRACT

## Scope

Phase 2 delivers **controlled test data** plus **deterministic, versioned rule evaluation** that persists eligibility and score-component rows. It does not introduce AI, live affiliate networks, payments, discovery, or campaign execution.

## Requirements

| ID | Description |
| --- | --- |
| BLC-P2-DATA-001 | Phase 1 seed rows remain intact after Phase 2 seed (idempotent, additive). |
| BLC-P2-DATA-002 | A second advertiser exists; one creator can match opportunities from two advertisers. |
| BLC-P2-DATA-003 | Test data is global-by-field: at least one non-PH creator/opportunity (country/language as data). |
| BLC-P2-DATA-004 | UNKNOWN demographics remain SQL NULL and are not treated as zero by the evaluator. |
| BLC-P2-DATA-005 | One content item can carry additional independent inventory slot types (overlays) without a delivery engine. |
| BLC-P2-DATA-006 | Creating additional BlissMatches does not overwrite Match A/B/C. |
| BLC-P2-RULE-001 | RuleVersion 2 stores an explicit JSON document. Historical Phase 1 matches keep RuleVersion 1. |
| BLC-P2-RULE-002 | Evaluator writes EligibilityCheck results `APPROVED` / `REVIEW_REQUIRED` / `INELIGIBLE` with reason codes. |
| BLC-P2-RULE-003 | Evaluator writes MatchScoreComponent rows from documented weights. AI is not used. |
| BLC-P2-API-001 | GET visibility for networks, access, provenance, and campaigns. |
| BLC-P2-API-002 | POST evaluate applies stored rules to one match and does not mutate other matches or RuleVersionId. |

## Exact tasks

1. Additive `RuleVersions.DocumentJson` column (nullable).
2. `DeterministicRuleEvaluator` in `Bliss.Domain` (pure function).
3. `Phase2DataSeeder` additive graph + evaluation of new matches only.
4. Read APIs for remaining Phase 1/2 entities.
5. `POST /api/bliss/matches/{id}/evaluate-rules`.
6. Tests and evidence snapshots.

## Forbidden changes

- No AI/ML matching.
- No n8n, Firecrawl, Tavily, Awin, Levanta, Wise, PayPal, Stripe.
- No auth/authorization redesign.
- No destructive migrations (`DROP TABLE`, data deletes of Phase 1 keys).
- No unique constraint on `BlissMatch.CreatorId` or `CampaignPlacement.ContentItemId`.
- No treating UNKNOWN as zero.
- No production payment or live affiliate calls.
- No Alpha Auto merge.

## Acceptance tests

See `Bliss.Tests` Phase 2 classes. Command: `dotnet test`.

## Evidence

`docs/bliss/PHASE-2-EVIDENCE.md` and `docs/bliss/evidence/phase2/`.

## Definition of DONE

- Contract requirements have tests or live TEST-DB proof.
- Phase 1 CRT-TEST-001 matches still exist with original opportunity IDs.
- Snapshots captured (tests, API JSON, screenshots, TEST SQL counts).
- No secrets committed.
