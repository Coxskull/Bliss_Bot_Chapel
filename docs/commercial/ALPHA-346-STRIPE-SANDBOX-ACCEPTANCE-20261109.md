# ALPHA-346 — Stripe sandbox checkout and receipt acceptance

**Scope:** Existing ALPHA GLOBAL MEDIA LLC Campaign Planning & Coordination planning-only starter.
**Evidence date:** 2026-11-09, read-only Stripe sandbox API inspection.
**Decision:** `SANDBOX_CHECKOUT_READBACK_VERIFIED` — test-mode verification only, **not live payments or commercial release**.

## Existing resources, reused (no duplicates)
- Connected account: **Alpha Global Media sandbox**; `livemode=false`, US/USD.
- Existing product: `prod_VawZ1nLovQnzq6` — **[SANDBOX ONLY] Campaign Planning & Coordination — Planning-Only Starter**.
- One-time test price: `price_1UZkfyH23maIy3rzzhYzRWFD`, USD **$149.00**.
- Existing active test Payment Link: `plink_1UZkkuH23maIy3rzRSbuvCtT`, with automatic tax enabled and required billing address.
- **Do not copy this link into any public site or offer as a real payment option**.

## Stripe read-back evidence (only technical IDs, never customer details)
- **Successful case:** Checkout Session `cs_test_a127dclwqC9jOEq2Fzt0Rg6R05GEwdbi2U73Lp4R2AlIpb4a0dDkeQvdXI` is `complete` / `paid`, linked to the above Payment Link and one line item with the above price, quantity 1, USD 14,900 minor units.
- PaymentIntent `pi_3UZmosH23maIy3rz1QI0U6Lo` is `succeeded` and `amount_received=14900`, `livemode=false`.
- Matching events: `checkout.session.completed` and `payment_intent.succeeded`, both test mode.
- Stripe Checkout automatic tax status is `complete` with test `amount_tax=0`. This **does not** prove tax registrations or any jurisdiction-specific tax obligation has been fulfilled.
- **Failure case:** Separate test PaymentIntent `pi_3UZmxtH23maIy3rz1AKGKGEK` returned `requires_payment_method` / `insufficient_funds` with `amount_received=0`; event `payment_intent.payment_failed`. This is an independent sandbox failure record and is **not claimed to have come from that same completed Checkout Session**.

## Permanent regression / local acceptance
- `scripts/fixtures/alpha346-stripe-sandbox-readback-20261109.json` contains only the minimal read-only Stripe response fields needed to reconcile test payment records. It does not hold any names, emails, payment method data, API secrets, or customer identifiers.
- `scripts/alpha346-stripe-sandbox-reconcile.mjs` rejects live mode, wrong link/product/price, mismatched amounts, missing events, incomplete or unpaid sessions, incomplete tax status and absent failed-payment evidence.
- `scripts/alpha346-stripe-sandbox-acceptance.test.mjs` tests the accepted sample plus negative cases and asserts `realRevenueRecognized=false`, `productionPaymentAuthorized=false`, `fulfillmentAuthorized=false`.
- The existing `alpha346-manual-first-customer-ci.yml` workflow runs the tests without API access or network calls.

## Not completed or authorized by this sandbox verification
1. Stripe sandbox **webhook endpoints: 0 configured** at this readback. Delivery signature verification, event replay/idempotency, automatic order receipt and hosted fulfillment are **not verified**, and are not silently substituted with read-only polling.
2. The test Checkout has no validated real buyer acceptance, production invoice or verified signed campaign scope. It is an isolated payment test and **not revenue**.
3. Live merchant access, legal/tax/customer-support approval and release remain outside this sandbox-only task. Do not ask for live-account access now; the owner has explicitly deferred it.
4. No production website, Supabase data or factory pipelines were updated; global STOP remains engaged. The existing inquiry-only public portal stays free of checkout links, public $149 pricing and automated outreach.

**Bottom line:** Stripe sandbox payment-link configuration, one real test-mode paid readback, one independent declined-payment readback, Stripe event evidence, and reproducible offline verification are **complete for the sandbox checkout verification phase**. The wider paid-order fulfillment workflow (including live and hosted webhooks) is a separate future phase.
