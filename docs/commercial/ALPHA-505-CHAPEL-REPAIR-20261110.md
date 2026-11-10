# ALPHA-CODEX-BLISS-CHAPEL-REPAIR-505

Evidence: `ALPHA-EV-20261110-REPAIR505`. Source repair only; commercial launch acceptance remains OPEN.

## Delivered repairs

Branch `codex/bliss-chapel-repair-505` starts at integration commit `255b1bcd29beace5304d07a66bc0a802ff810d8b`, not production main. It reuses the existing planning-only Stripe verifier, immutable receipt store, isolated HTTP receiver, public consent forms and offline proposal desk. Coordination scope was posted on PR #367. No other engineer's branch was modified.

Three reproduced defects are repaired:

1. Both the signed Checkout verifier and the offline provider-readback reconciler accepted inclusive tax greater than the entire payment. Tax is now an integer between zero and the existing test-only total, inclusive. This does not calculate or approve a tax rate.
2. The readback reconciler accepted one repeated Stripe event ID as evidence of three different event types/resources. Duplicate event identities now fail reconciliation instead of producing a verified result.
3. Validly signed JSON with wrong field types raised `InvalidOperationException`, which the existing HTTP adapter reported as retryable 503. JSON shape/type/number failures now become a generic invalid-input exception inside verification, yielding 400 before opening a database connection. Database failures remain 503; neither error response discloses the payload or signing secret.

Exact files changed:

- `Bliss.Infrastructure/Financials/StripePlanningSandboxWebhookVerifier.cs`
- `Bliss.Tests/Financials/StripePlanningSandboxWebhookTests.cs`
- `scripts/alpha346-stripe-sandbox-reconcile.mjs`
- `scripts/alpha346-stripe-sandbox-acceptance.test.mjs`
- `scripts/alpha346-stripe-sandbox-http-acceptance.test.mjs`
- This evidence document.

No dependencies, prices, public payment surfaces, production routes, database schemas, authorization rules or factory controls are changed.

## Reuse and inspection

| Area | Evidence and remaining boundary |
| --- | --- |
| Referenced PRs | #359 is merged to main; #367 and #370 are merged into the integration branch. #358 and #368 remain open at inspection. Existing #503 branch owns proposal fingerprint/version repair; those files are deliberately not duplicated here. |
| Advertiser consent/registration | Existing inquiry consent/CRM guards pass locally. Authenticated advertiser registration is separately owned by open #349; it must not be confused with an inquiry. Hosted signup/session acceptance was not performed. |
| Creator registration/authentication | Open #351 stacks on #349. Neither creator accounts nor marketplace activation is required for the standalone manual planning service. No account created here. |
| Intake persistence | Read-only Bliss Supabase aggregate on November 10: 5 requests, 5 receipts, 0 contact-consented, 5 HOLD, 0 approved followups. Existing `alpha-public-intake` is ACTIVE v23. No hosted form POST or row mutation. |
| Quotation | Existing five-deliverable private desk and synthetic proposal guard retained. Business checkboxes do not establish an actual signed quote. #358 arithmetic and #503 evidence-binding work require coordinated review rather than another implementation. |
| Stripe Checkout | Existing AGM sandbox link/product/price identities retained. Local readback fixture reproduces previously recorded provider success and insufficient-funds decline from issue #361; this is historical evidence, not a fresh Stripe API read or a new Checkout run. No Stripe tools or merchant credentials are available in this session. Alpha Auto is not used. |
| Webhook/reconciliation | Real loopback HTTP and disposable PostgreSQL tests pass below. Default-off isolated receiver is not a production endpoint. No real Stripe-to-host delivery, provider expiration readback, or live reconciliation is established by this patch. |
| Support/fulfillment | Existing support receipt and planning packet tests pass. Sandbox receipt decisions always retain `FulfillmentAuthorized=false` and `RealRevenueRecognized=false`. A paid-service primary/backup and an accepted-order-to-fulfillment integration remain unverified. |
| Production | Railway production read-only: bliss-bot and Postgres online, 0 recent failures, no staged/applying work. Application deployment `8dc01af8-61a8-493b-a0c5-0277fab7c478` dates to November 6. Connected Vercel team project search returned no Bliss project, so current Vercel release/configuration could not be independently verified. No environment variables or secrets were retrieved. |

## Reproducible validation

Local SDK: .NET 8.0.427; Node 24.22.1; disposable PostgreSQL 16 on loopback port 55432. Test credentials are local fixture values only. No hosted credentials were supplied.

- New Node regressions against original reconciler: 2 failed, 6 passed. Repaired: 8/8 passed.
- New .NET regressions against original verifier: 9 failed, 6 passed. Repaired verifier: 15/15 passed.
- Combined existing signed sandbox payment/status verifier suites: 37 passed, 0 failed/skipped.
- Full existing .NET suite: 1,310 passed, 0 failed, 73 fixture-gated skips (1,383 total). The skipped suites are not claimed as verified; the relevant receipt-store fixture was exercised separately.
- Existing PostgreSQL receipt-store suite: 3/3 passed, including 12 concurrent retries, append-only controls and duplicate/conflict handling.
- Existing isolated receiver Release build: 0 warnings/errors.
- Extended real local HTTP acceptance: PASS. Disabled 404; signed payment/retry 204; wrong link, amount, signature, stale signature, malformed JSON and expired Checkout event 400; conflicting state 409; unavailable persistence 503; invalid signature rejected even when DB unavailable; 12 concurrent identical deliveries acknowledged without additional receipts.
- Independent SQL count after HTTP run: exactly 3 rows, 3 distinct event IDs and 3 distinct PaymentIntent IDs. Invalid payloads and duplicates add no receipts.
- Actual generated public portal build and existing ALPHA-346 service/consent/proposal/support/payment-leakage suites: 36 passed, 0 failed/skipped. Private tools and unapproved payment links/prices remain absent from public output.
- Existing `node scripts/agm-ghl-unit-smoke.mjs`: PASS (consent, minimal mapping, disabled mode, delivery/error contracts).
- `git diff --check`: PASS.

The full solution emits existing warnings in `MigrationCommand.cs` (obsolete certificate option/nullability) and `AdvertiserRegistrationSecurityTests.cs` (xUnit analyzer); this patch adds none. Existing CI workflow `.github/workflows/alpha346-manual-first-customer-ci.yml` already includes all changed code/test paths and executes these Node, signed webhook, PostgreSQL and HTTP checks. Remote exact-head CI must be checked on the PR, not inferred from its base.

Commands, from repository root with the existing SDK/dependencies installed:

```sh
dotnet test Bliss.Tests/Bliss.Tests.csproj --configuration Release
BLISS_APPLICATION_ORIGIN=https://example.org node scripts/build-vercel-portal.mjs
node --test scripts/alpha346-stripe-sandbox-acceptance.test.mjs
node scripts/agm-ghl-unit-smoke.mjs
```

For database/HTTP acceptance, use the existing workflow's disposable PostgreSQL recipe and `BLISS_FINANCIAL_TEST_POSTGRES` loopback fixture, then build the receiver and run `node --test scripts/alpha346-stripe-sandbox-http-acceptance.test.mjs`. Never point the fixture at production.

## Remaining shortest path and release decision

**The requested complete, repeatable hosted synthetic customer purchase journey is NOT complete.** The existing isolated payment receipt path is validated; there is still no demonstrated binding from a consented inquiry through a signed, versioned order to a Stripe payment and authorized fulfillment handoff. This is an engineering/integration gap as well as separate commercial approval gates. Passing these tests must not be presented as first-customer payment or fulfillment acceptance.

1. Independently review this small child patch and the existing quotation/auth work. Integrate only through the coordinated release branch; this directive does not authorize merge.
2. Use an already authorized, zero-incremental-cost isolated test host/database and the existing AGM sandbox integration to demonstrate actual signed provider delivery, expiration and retries. Existing hosted receiver/deployment-candidate branches own that work; reuse them. Do not create a billed service or duplicate merchant product. This session has no Stripe credential/endpoint secret and cannot complete provider readback.
3. Connect the existing consent receipt and exact accepted quote/terms version to the payment reconciliation and human fulfillment queue. Demonstrate success, decline, cancellation/expiry, signature rejection, duplicate handling, no unpaid fulfillment and no unauthorized contact with fictional records. This requires further source integration and isolated acceptance; an offline fixture is not a substitute.
4. Before live release, the owner must verify the intended AGM live merchant/account, fees/currency/tax; finalize paid-service terms/refund/privacy versions; identify and test paid-support primary and distinct backup; approve the exact real customer's quote, acceptance and payment action. Historic five inquiries remain HOLD.
5. Only after the complete test journey and explicit release authorization: approve the production change and perform the separately authorized customer-specific live transaction. No live price, charge, production merge or deployment is authorized by this patch.

## Rollback

Nothing is deployed and no migration is added. To withdraw, close the child PR or drop its commit; leave the integration branch and all hosted resources unchanged. If this source is later integrated and released under separate authority, disable the existing sandbox receiver flag and roll back its artifact to the previously reviewed release. Preserve immutable receipts and HOLD/STOP history; do not delete, rewrite or truncate records to roll back code.
