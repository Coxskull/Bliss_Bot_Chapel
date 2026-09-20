# PHASE 2 — EVIDENCE

**Date:** 2026-09-20  
**Status:** PASS  
**Tests:** 34 / 34 passed (0 failed, 0 skipped)  
**Migration:** PASS (additive `RuleVersions.DocumentJson` only)  
**Phase 1 preservation:** PASS (Match A still CREATED on RuleVersion 1 / Opportunity A)

Snapshots: `docs/bliss/evidence/phase2/`

## What Phase 2 is

Controlled fictional data + deterministic evaluator from an explicit RuleVersion JSON document.

## What Phase 2 is not

AI matching, n8n, live affiliates, payments, auth redesign, campaign execution, ad rendering.

## TEST database

Local `bliss_phase1_test` only. Applied `20260920063520_Phase2RuleDocument` (`AddColumn` nullable text). No DROP in `Up()`.

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

Match A (`…7771`) remains Opportunity A + RuleVersion 1.0.0 + CREATED.

Phase 2 matches on RuleVersion 2.0.0:

| Match | Status | Meaning |
| --- | --- | --- |
| …7775 PH creator → BR opportunity | INELIGIBLE | GEO_NOT_ELIGIBLE |
| …7776 BR creator → BR opportunity | APPROVED | overall 1.0 |
| …7777 unknown demographics → PH Harbor opp | REVIEW_REQUIRED | UNKNOWN ≠ 0 |
| …7778 PH creator → Harbor PH opp | APPROVED | same creator, second advertiser |

## API

- GET networks, access, provenance, campaigns
- POST `/api/bliss/matches/{id}/evaluate-rules`
  - Match A → HTTP 400 (no DocumentJson on v1)
  - Brazil match → HTTP 200 APPROVED

JSON: `docs/bliss/evidence/phase2/api/`

## Tests

Command: `dotnet test BlissBotChapel.sln`  
Commit captured in git after this evidence pack.

## Recommended next

Do not start Phase 3 (Chaperone productization / Fishing Fleet / measurement) until a Phase 3 contract is issued.
