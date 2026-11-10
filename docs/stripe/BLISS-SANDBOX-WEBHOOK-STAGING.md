# Bliss Bot — Stripe sandbox webhook (staging acceptance, NOT production)

**Repository:** `Coxskull/Bliss_Bot_Chapel`  
**Endpoint:** `POST /api/webhooks/bliss/stripe`  
**State:** Code prepared; staging endpoint **not deployed/registered**; no payment fulfillment authorized.

## Safety boundaries

- New handler is separate from `Coxskull/alpha-backend`.
- Handler only becomes reachable in `ASPNETCORE_ENVIRONMENT=Staging`, with `Runtime__RestrictedPreview=false` and explicit `BlissStripeWebhook__SandboxEnabled=true`.
- Restricted Preview stays immutable and blocks API/data writes. Never bypass `RestrictedPreviewGate`.
- Only HMAC-verified **testmode** non-Connect events within five minutes are accepted.
- The raw payload is not logged or stored; only event type, event ID, object ID, SHA-256, disposition and receipt time are persisted.
- The `bliss_stripe_sandbox.event_receipts` journal is append-only, de-duplicated by Stripe event ID, and refuses a mismatching replay.
- Received successful payment events remain **RECONCILIATION_REQUIRED**. **No automatic fulfillment, cash ledger posting, ad publication, or commission transfer occurs.**
- Return 503 on unavailable journal so Stripe can retry. Return 202 only once durable receipt exists; same-event replay returns 200.

## Isolated staging prerequisites

1. Prepare **a separate authorized staging backend and staging PostgreSQL database**, with spending controls verified. The connected Railway `bliss-bot` project currently has only a `production` environment. Do not use its production URL or database for this task.
2. Apply `Bliss.Infrastructure/Migrations/BlissStripeSandboxJournalStagingV1.sql` **only** to the verified staging database. This is intentionally not an automatically applied EF migration. Use a restricted DB role with SELECT+INSERT only on the journal; no UPDATE, DELETE, TRUNCATE or financial ledger grants.
3. Configure these secrets/settings **on that staging service only**:
   - `ASPNETCORE_ENVIRONMENT=Staging`
   - `Runtime__RestrictedPreview=false` (staging **only**, after confirming isolated OIDC, runtime, storage, and network posture)
   - `BlissStripeWebhook__SandboxEnabled=true`
   - `BlissStripeWebhook__SigningSecret=<Stripe test-mode endpoint whsec secret>` (never commit)
   - `BlissStripeWebhook__JournalConnectionString=<separate staging database DSN>`
   - `BlissStripeWebhook__ExpectedJournalDatabase=<exact staging database name>`
4. Deploy the reviewed and CI-accepted commit to staging. Confirm public HTTPS URL is an isolated staging hostname, correct route and public Stripe accessibility. Verify wrong signature, stale signature, Connect event, and live event get 400; production/Preview mode returns 404.
5. Only then create a **test-mode-only** Stripe webhook endpoint pointed at `https://<approved-stage-host>/api/webhooks/bliss/stripe`. Subscribe to `checkout.session.completed`, `checkout.session.async_payment_succeeded`, `checkout.session.async_payment_failed`, `checkout.session.expired`, `payment_intent.succeeded`, `payment_intent.payment_failed`, `payment_intent.canceled`.
6. Verify actual Stripe delivery attempts: new event 202 with one durable receipt, duplicate 200 with one receipt, failed/expired disposition `NO_FULFILLMENT`; test-mode successful payment `RECONCILIATION_REQUIRED` with no business effects. Capture redacted evidence and CI runs.

## Separate acceptance gate: payment-to-fulfillment reconciliation

The endpoint is **not a payment-completion handler**. It does not yet map Stripe PaymentIntent/Checkout Session to Bliss's real authorized advertiser quote, campaign, merchant and accepted amount/currency. The legacy Alpha order model is not compatible by assumption. Before enabling fulfillment, build an isolated and atomic reconciliation workflow that:
- Verifies real provider status using an authorized server-side Stripe lookup as needed.
- Joins to **one** immutable Bliss funding operation/accepted quote and merchant identity; checks amount, currency, scope, authorization and test account identity.
- Handles out-of-order and duplicate events atomically, never releases a campaign more than once, and never treats expired/failed payments as paid.
- Records a durable financial decision and ensures STOP and human approval gates remain intact.
- Passes an actual hosted staging acceptance test before changing `fulfillmentAuthorized=false` or allowing a live endpoint.

## What constitutes complete

- [x] Separate Bliss sandbox handler and signature verification
- [x] Append-only event journal schema and durable repository implementation
- [x] Unit/CI fixture tests in PR
- [ ] Staging infrastructure isolated and approved
- [ ] CI build and tests green; review/merge
- [ ] SQL applied only to staging and staging service deployed
- [ ] Stripe test-mode endpoint registered and deliveries confirmed
- [ ] Funding reconciliation and paid-only fulfillment end-to-end accepted
- [ ] Live payments (requires separate owner approval)

Do not claim complete on the strength of Stripe-origin events or the existence of this PR alone.

## Pre-fulfillment quote/payment authority evaluator (read-only)

`Bliss.Api/Financials/BlissStripeSandboxReconciliationGate.cs` compares one previously persisted verified webhook receipt and one independently retrieved test-mode PaymentIntent to an **immutable and database-verified** `PreparedPaymentOperation` (merchant, campaign, module, quote, recipient, amount and currency), a persisted **bound PaymentIntent ID**, and the approved frozen funding reference. The gate refuses live-mode/Connect evidence, non-success state, wrong amount/amount_received/currency, or wrong merchant/campaign/module/intent/funding reference.

Even exact matching evidence returns `EVIDENCE_MATCHES_MANUAL_REVIEW_REQUIRED` and **`AuthorizesFulfillment=false`**. Its caller must provide the authoritative operation from the separate isolated staging database and must establish authenticated Stripe test-account origin; the evaluator itself does not make provider calls, invoke a settlement worker or consume real Stripe events. Tests exercise positive proof-of-match and multiple negative states. This gate is *not* a completed payment fulfillment workflow.

### Current existing infrastructure overlap

Connected Railway inspection on 2026-11-11 found:
- `bliss-bot` project contains **production only** and therefore is not an approved staging environment.
- `alpha-henry-ford-factory-staging` contains a separate **staged-create**, not deployed, service `alpha346-stripe-sandbox-receiver`. Its source is the `deploy-only/alpha346-stripe-sandbox-railway-20261110` branch and is a distinct, planning-service receipt boundary; it is **not** a Bliss advertiser/campaign settlement endpoint.
- The ALPHA-346 receiver targets an isolated Neon `alpha346_stripe_sandbox` database and a different schema. Do not point this Bliss Bot journal at that existing ALPHA-346 database without a reviewed isolation and data ownership decision.
- The staging patch has pending changes; applying it would create a service and requires verified actual billing protections and explicit deployment authorization. The pending patch has **not** been accepted by this work.

