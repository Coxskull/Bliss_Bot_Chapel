# AGM-LAUNCH-012 — read-only native-currency funding preflight

**Date:** 2026-10-15
**Scope:** `Coxskull/Bliss_Bot_Chapel`, following merged PR #217.
**Status:** Code proposed, exact-head CI and independent review required.

## Why

The actual synthetic Manila Economics handoff is stored in **PHP**. The financial importer added earlier accepts **USD** without an approved FX conversion. The valid response is to report the blocker, not to relabel PHP as USD or assign a conversion rate.

## Changes

The existing workspace-scoped handoff controller now provides:

`GET /api/wedding-planner/workspaces/{workspaceId}/handoff/plans/{handoffId}/funding-preflight`

The endpoint:
- Enforces existing workspace isolation **before** accessing the handoff; another advertiser cannot read the record.
- Reads the exact stored campaign, placement, handoff, run, quote, quote-version, quote-line, and latest approval/outcome, including the frozen snapshot.
- Refuses altered/mismatched handoff identity, amount, currency, quantity, placement state, source association and snapshot.
- Reports the original currency and amount in invariant-culture text, **without** calculating any USD equivalent.
- Reports `SOURCE_CURRENCY_NOT_USD` for the actual PHP fixture; `QUOTE_AUTHORITY_CHANGED` when latest Economics authority is stale/revoked; and `USD_QUOTE_STILL_REQUIRES_FINANCIAL_AUTHORITY` for internally consistent USD evidence.
- Keeps `MockPaymentAuthorized=false`, `ProviderCalled=false`, `PaymentCompleted=false`, `CommissionAllocated=false`, `CreatorPayoutAuthorized=false`, `CampaignReady=false`, `CampaignDeliveryAuthorized=false`, `Delivery=NOT_SENT`, `FxPolicyStatus=NOT_APPROVED` in **all** cases.

The existing `campaign-handoff.html` history now offers **Check funding currency (read-only)** for each planned record, showing the authoritative source amount/currency and the blocking prerequisite. It does not send any approval/write request.

## Automated verification criteria

1. Existing synthetic PHP handoff returns `SOURCE_CURRENCY_NOT_USD` through the authenticated workspace endpoint, without modifying quote/placement/handoff.
2. Foreign advertiser and nonexistent handoff return 404.
3. A stale approval or accepted outcome returns `QUOTE_AUTHORITY_CHANGED`.
4. Changed amount, handoff delivery, or frozen snapshot fails closed.
5. A separately constructed **in-memory** USD projection returns `USD_QUOTE_STILL_REQUIRES_FINANCIAL_AUTHORITY`. This is not a stored, accepted USD quote or a currency conversion.
6. Exact-head .NET 8, Docker, dedicated financial/gateway PostgreSQL, Wedding Planner PostgreSQL and browser CI must pass before independent review.

## Unresolved policy and commercial gates

An authorized collecting merchant and payees, approval for a genuinely quoted USD contract **or** independently approved FX/settlement policy, creator compensation and ALPHA revenue split, independent creative/placement quality, consented Ask Alpha linkage, actual Stripe test account, signed webhook, refunds/reconciliation, hosting review and Executive acceptance all remain outstanding.

**No schema changes, production database operations, real payment calls, creator payout, campaign activation, or campaign delivery.**
