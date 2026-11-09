# ALPHA-346 — Stripe SANDBOX-only webhook HTTP receiver, acceptance and hosted deployment boundary

**Evidence date:** November 9, 2026. **Scope:** Existing $149 USD test-only Campaign Planning & Coordination Payment Link. **No live-payment work authorized.**

## Engineering delivered

The independent .NET 8 app at `tools/staging/Alpha346.StripeSandboxReceiver/` reuses the existing `StripeSandboxVerifier`, `StripePlanningSandboxWebhookVerifier` and `PlanningSandboxStripeReceiptStore`.

It is **not** added to the main Bliss API, production dependency injection, existing Vercel static site or production Railway service. Its webhook route `POST /internal/alpha346/stripe-sandbox-webhook` responds `404` by default. The separate health route `GET /internal/alpha346/sandbox-health` responds `404` when disabled.

When properly sandbox-enabled, the receiver:
1. Requires ASP.NET environment exactly `AlphaStripeSandbox`, explicit toggle `true`, exact expected **test-mode Stripe account ID**, and a valid separately stored `whsec_` signing secret.
2. Uses **only a dedicated sandbox receipt database**. CI accepts `Host=127.0.0.1;Database=financial_test`; future external staging accepts only `Database=alpha346_stripe_sandbox`, `Username=alpha346_webhook_ingest`, and `SslMode=VerifyFull` to a nonloopback host. Shared production/customer database connection strings are rejected.
3. Checks the raw HTTP body under 1MB and Stripe timestamped HMAC signature; allows only the existing sandbox Payment Link, exact USD $149, valid test-mode receipt states, and explicitly forbidden real-customer use.
4. Records only event ID, Checkout Session ID, PaymentIntent ID, status, and SHA256 fingerprint transactionally in an immutable sandbox schema. No card numbers, payer identity, CRM record, journal, payout or campaign operations.
5. Returns `204` only for safely persisted test evidence or harmless duplicates, `400` for invalid signature/content, `409` for irreconcilable or review-required state changes, `503` for persistence errors. No response ever authorizes fulfillment or real revenue.

## Exact source acceptance

- Draft child pull request: [PR #371](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/371) targeting existing [sandbox work PR #367](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/367), **not** `main`.
- Commit `3cff7f51d15848f22de3fe307edcf36b6a5ec36f`: [GitHub Actions run 42583531145](https://github.com/Coxskull/Bliss_Bot_Chapel/actions/runs/42583531145), **SUCCESS**.
- Existing .NET sandbox verifier/store suites: **23 / 23 tests PASS**.
- New isolated `dotnet build`: **PASS**.
- Local HTTP test with a **synthetic** Stripe `whsec_` signature, no Stripe network call: **PASS**. Disabled 404, signed 204, replay 204, invalid signature 400, conflict 409, 12 concurrent duplicate deliveries 204.
- Disposable PostgreSQL 16 migration **PASS**; independent SQL readback confirmed **exactly three unique synthetic HTTP receipt rows**, not duplicated or modified by replay.
- The existing public inquiry/payment-link reconciliation job also passed. No hosted services, GitHub secrets or external database were modified.

## Before any hosted Stripe **test-mode** event delivery

1. Select **an already approved, non-production, zero-incremental-charge** HTTPS staging service and a separate PostgreSQL database. Neither is confirmed at this time. Railway `bliss-bot` has only a `production` environment; **do not** deploy there. Railway `alpha-henry-ford-factory-staging` has unrelated engine/health services; do not replace or repurpose them. The Vercel `bliss-bot` PR preview is a static portal, **not** this .NET receiver.
2. Require explicit confirmation of staging environment privileges, effective billing caps, DNS/TLS, owner and retention settings; do not create a billed service by default.
3. In the dedicated database only, under an authorized migration role, review and apply `Bliss.Infrastructure/Migrations/Alpha346PlanningSandboxStripeReceiptsV1.sql`. Create a distinct least-privilege login `alpha346_webhook_ingest` and grant only required `USAGE` on schema and `SELECT, INSERT` on the technical receipt table. No grants to `anon` or arbitrary users. Never copy live customer data into the sandbox.
4. In **the sandbox service's protected secret manager only** supply these settings; do not write their values into GitHub source or issue comments:
   - `ASPNETCORE_ENVIRONMENT=AlphaStripeSandbox`
   - `ALPHA346_STRIPE_SANDBOX_WEBHOOK_ENABLED=true`
   - `ALPHA346_STRIPE_ACCOUNT_ID=acct_1UOn83H23maIy3rz` (sandbox-only)
   - `ALPHA346_STRIPE_WEBHOOK_SECRET` (the Stripe *test-mode* endpoint's unique signing secret)
   - `ALPHA346_STRIPE_RECEIPTS_DATABASE` (isolated staging DSN with hostname verification and restricted DB login)
5. Verify public HTTPS (TLS at ingress), restricted operator visibility, rate limits, request size, stable routing and the final exact-head code review. **Only then** register a Stripe **test-mode** webhook endpoint for `checkout.session.completed`, `checkout.session.async_payment_succeeded` and `checkout.session.async_payment_failed`, pointing to the isolated HTTPS route. Never use a live Stripe mode or production merchant URL.
6. Run a test Payment Link transaction or Stripe test delivery to the verified URL. Check signature, event ID, one durable receipt for retries, 2xx/4xx/5xx handling, and logs without customer details. Test shutdown and endpoint removal; revoke the signing secret when staging is retired.
7. Customer order acceptance, paid-service terms, live Stripe, actual fulfillment, refund/payout workflow and production release remain separate **DENIED** gates; global factory STOP remains engaged.

**Result:** sandbox raw-body HTTP handler and durable DB **locally accepted**; external hosted Stripe-to-receiver delivery **NOT YET VERIFIED**. No real payments, new subscriptions or production changes.
