# PHASE 7 — CONTROLLED CAMPAIGN PLACEMENT BINDING EVIDENCE

**Date:** 2026-09-20  
**Status:** PASS  
**Automated tests:** 77 / 77 passed  
**Hosted migration:** PASS on the dedicated Bliss Supabase PostgreSQL database  
**Alpha Auto coupling:** NONE

Raw evidence: `docs/bliss/evidence/phase7/`

## Delivered boundary

```text
APPROVED BlissMatch + DRAFT Campaign + creator ContentItem + slot
    → PLANNED CampaignPlacement
    → immutable CampaignPlacementRun
```

The boundary records inventory intent only. It does not reserve inventory,
schedule, activate, deliver, track, measure, post a ledger entry, or pay.

## Automated verification

```text
Passed: 77
Failed: 0
Skipped: 0
```

Coverage proves first bind, exact replay, multiple placements for the same
match/content, non-approved match rejection, creator/content and slot/content
coherence, API visibility, non-unique business-key indexes, and Restrict
foreign keys.

## Migration

Applied:

```text
20260920133028_Phase7CampaignPlacementBinding
```

The additive migration:

- adds nullable `Campaigns.AdvertiserOpportunityId`;
- adds nullable `CampaignPlacements.BlissMatchId`;
- creates `CampaignPlacementRuns` and indexes.

Existing seed placements remain present with null match IDs. Seven migrations
are recorded. The regenerated Phase 1–7 full SQL applied twice to a clean
PostgreSQL database: seven migrations, four seed placements, zero binding
runs.

## Hosted API proof

The first request bound:

- approved Brazil match `77777777-7777-7777-7777-777777777776`;
- Phase 2 DRAFT campaign;
- Brazil creator content;
- rotating overlay slot.

| Operation | Result |
| --- | --- |
| First POST `/api/campaign-placements` | HTTP 201, `PLANNED`, `isReplay=false` |
| Exact replay | HTTP 200, same run/placement IDs |
| CREATED Match A request | HTTP 400, no run |
| Run detail | HTTP 200 with immutable input snapshot |
| Campaign detail | HTTP 200 with new match-linked placement |

Hosted invariants after the API request:

- one run and one new placement;
- slot `IsAvailable=true`;
- placement dates remain null;
- match remains `APPROVED`;
- campaign remains `DRAFT`;
- two legacy Phase 2 placements still have null `BlissMatchId`.

## Dashboard proof

The dashboard then planned one PH placement:

1. selected approved `Test Creator` match;
2. selected Phase 1 Foundation DRAFT campaign;
3. selected creator-owned `Chapel Conversations Episode 1`;
4. selected its available pre-roll slot;
5. recorded `TEST_OPERATOR`.

The placement ledger moved one → two runs and the binding queue moved three →
two. The detail drawer shows `Planned`, the campaign, content inventory intent,
and the immutable request snapshot.

Post-dashboard PostgreSQL:

- six total placements (four legacy plus two planned);
- two `CampaignPlacementRuns`;
- both planned rows have null dates and available slots;
- both source matches remain `APPROVED`;
- both campaigns remain `DRAFT`.

## Deferred

- slot reservation and scheduling conflicts;
- campaign activation and cancellation;
- creative upload and ad delivery;
- impressions, clicks, measurement, ledger, payout, and payment;
- Fishing Fleet/live providers;
- Alpha Auto integration or merge.
