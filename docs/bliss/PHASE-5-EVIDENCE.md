# PHASE 5 — CONTROLLED BLISSMATCH FORMATION EVIDENCE

**Date:** 2026-09-20  
**Status:** PASS  
**Automated tests:** 63 / 63 passed  
**Hosted migration:** PASS on the dedicated Bliss Supabase PostgreSQL database  
**Alpha Auto coupling:** NONE

Raw evidence: `docs/bliss/evidence/phase5/`

## Delivered boundary

Phase 5 adds an explicit operator-controlled path:

```text
Creator + AdvertiserOpportunity + RuleVersion
    → BlissMatch
    → optional deterministic evaluation
    → immutable MatchFormationRun
```

The boundary does not discover, rank, or select candidates. It makes no live
provider, crawler, AI, n8n, affiliate, campaign, money, measurement, ledger,
or Alpha Auto call.

## Automated verification

`dotnet test --no-restore`:

```text
Passed: 63
Failed: 0
Skipped: 0
```

New coverage proves:

- controlled create plus deterministic evaluation;
- exact idempotent replay with stable match/run IDs and no duplicate children;
- multiple certificates are allowed for one creator;
- missing references and document-less evaluation persist nothing;
- formation-run API collection/detail visibility;
- unique source/idempotency constraint and Restrict foreign keys;
- `BlissMatch.CreatorId` remains non-unique.

## Migration

Applied:

```text
20260920123653_Phase5MatchFormation
```

The migration only creates `MatchFormationRuns` and its indexes/Restrict
foreign keys. Its `Up()` contains no DROP operation. Five migrations are now
recorded in `__EFMigrationsHistory`.

The regenerated `docs/sql/bliss-supabase-full.sql` was also applied twice to a
separate clean PostgreSQL database. Both executions passed; the result had five
migrations, five fictional seed creators, eight seed matches, and zero
formation runs.

## Hosted API proof

Request:

- source: `PHASE5_LIVE_TEST`;
- creator: `Supabase Live Test Creator`;
- opportunity: `Opportunity PH window - Clip Mic`;
- rule: Phase 2 explicit rules (`2.0.0`);
- evaluate on create: true.

Results:

| Operation | Result |
| --- | --- |
| First POST `/api/bliss/matches` | HTTP 201, `isReplay=false` |
| Exact replay | HTTP 200, `isReplay=true`, same run and match IDs |
| Missing creator | HTTP 400; no formation run |
| Formation run detail | HTTP 200 with normalized input snapshot |
| Match detail | HTTP 200 with checks and score components |

The formed certificate:

- run `332e36a9-8e5b-4310-9800-8fbc5565d3d1`;
- match `85a9afc5-290f-4226-8f39-8994238feb49`;
- result `REVIEW_REQUIRED`;
- overall score `0.8500`;
- confidence `0.4000`;
- one evaluation run, five eligibility checks, and three score components.

Replay left those counts unchanged. The invalid idempotency key has zero runs.

Phase 1 Match A remains `CREATED` on its original opportunity and RuleVersion.

## Dashboard proof

The dashboard loaded from the same API and hosted database. The recorded flow:

1. Opens **Form match** with explicit creator, opportunity, and rule selectors.
2. Submits a controlled Phase 4 creator with evaluate-on-create checked.
3. Displays `Match formed`.
4. Opens the evaluated certificate for `Phase 4 Creator Spain` and
   `Opportunity A - Morning Tonic`.
5. Shows deterministic `INELIGIBLE`, score `0.30`, eligibility checks, score
   components, and evaluation history.
6. Match count moves 9 → 10 and formation-run count 1 → 2.

Direct PostgreSQL verification after the recording shows two formation runs,
ten matches, and multiple matches for existing creators without any unique
creator constraint.

## Deferred

- human review queue and decision audit;
- Fishing Fleet/live provider adapters;
- campaign execution and ad delivery;
- measurement, ledger, payout, and payment;
- Alpha Auto integration or merge.
