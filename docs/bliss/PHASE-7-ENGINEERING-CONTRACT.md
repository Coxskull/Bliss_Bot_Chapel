# PHASE 7 — CONTROLLED CAMPAIGN PLACEMENT BINDING

**Status:** Authorized  
**Boundary:** Bliss Bot Chapel only  
**Prerequisites:** Phases 1–6 accepted

## Objective

Give an operator an explicit, auditable way to bind an `APPROVED`
`BlissMatch` to creator-owned content inventory inside an existing DRAFT
campaign. This records planning intent only. It does not reserve inventory,
schedule, deliver, track, measure, or pay.

## Requirements

| ID | Requirement |
| --- | --- |
| BLC-P7-BIND-001 | `POST /api/campaign-placements` creates one placement from explicit match, campaign, content, and slot IDs. |
| BLC-P7-BIND-002 | `(SourceSystem, IdempotencyKey)` is unique; exact replay writes nothing. |
| BLC-P7-BIND-003 | Only `APPROVED` matches may be bound. |
| BLC-P7-BIND-004 | The campaign must exist and remain `DRAFT`. |
| BLC-P7-BIND-005 | Content must belong to the match creator. |
| BLC-P7-BIND-006 | The slot must belong to the supplied content item. |
| BLC-P7-BIND-007 | The match opportunity must remain `ACTIVE`. |
| BLC-P7-BIND-008 | A campaign with no opportunity becomes bound to the match opportunity; later placements must match it. |
| BLC-P7-BIND-009 | Every accepted request writes an immutable `CampaignPlacementRun.InputSnapshot`. |
| BLC-P7-BIND-010 | Placement runs and the approved-match binding queue are visible via GET APIs. |
| BLC-P7-BIND-011 | Placement status is `PLANNED`; match status and slot availability are unchanged. |
| BLC-P7-MULTI-001 | `ContentItemId`, `AdInventorySlotId`, and `BlissMatchId` remain non-unique. |
| BLC-P7-UI-001 | The dashboard can bind an approved match to a compatible creator-owned slot. |
| BLC-P7-SAFE-001 | Invalid input creates no placement/run and mutates no campaign. |

## Persistence contract

Additive nullable bridges preserve Phase 1–2 seed rows:

- `Campaign.AdvertiserOpportunityId`;
- `CampaignPlacement.BlissMatchId`.

`CampaignPlacementRun` records placement, match, campaign, creator,
opportunity, content, and slot IDs plus source/idempotency fields, operator
label, status/outcome, timestamps, and normalized input snapshot.

All foreign keys use `ON DELETE RESTRICT`. Only `(SourceSystem,
IdempotencyKey)` is unique.

## API behavior

- First accepted request: HTTP 201, `isReplay=false`.
- Exact replay: HTTP 200, `isReplay=true`, same run and placement IDs.
- Invalid graph/status: HTTP 400 and no writes.
- Queue contains approved matches with no Phase 7 placement run.

## Acceptance

1. The seeded approved Brazil match binds to Brazil creator inventory.
2. Exact replay leaves placement/run counts unchanged.
3. A second key may create another placement for the same match/content.
4. `REVIEW_REQUIRED`, `CREATED`, and `INELIGIBLE` matches are rejected.
5. Wrong-creator content and wrong-content slots are rejected.
6. Existing seed placements remain present with null `BlissMatchId`.
7. Match status, scores, evaluation/review history, slot availability, and
   placement dates are unchanged.
8. Automated tests, hosted PostgreSQL, API, dashboard, and SQL evidence pass.

## Forbidden

- No automatic placement selection or AI placement authority.
- No slot reservation or mutation of `IsAvailable`.
- No scheduling/conflict engine or campaign activation.
- No ad rendering, creative upload, delivery, tracking pixels, clicks,
  impressions, measurement, ledger, payout, or payment.
- No Fishing Fleet, provider API, scraper, n8n, or live affiliate call.
- No Alpha Auto import, schema, credentials, integration, or merge.
- No destructive migration or rewrite of Phase 1–6 history.
