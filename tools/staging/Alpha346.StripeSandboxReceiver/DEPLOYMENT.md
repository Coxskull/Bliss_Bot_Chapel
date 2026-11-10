# ALPHA-346 — Staging-only Stripe webhook receiver deployment candidate

This folder contains the existing .NET 8 TEST-MODE-only webhook receiver and its independent Dockerfile. It does **not** change Bliss Bot's production application, deploy production payment, create customer orders, or release factory STOP.

## Build

Use the repository root as the Docker build context:

```sh
docker build -f tools/staging/Alpha346.StripeSandboxReceiver/Dockerfile -t alpha346-stripe-sandbox-receiver:local .
```

The multi-stage build publishes only the staging receiver. Runtime binds to `0.0.0.0:8080` as a non-root user. No Stripe secret or Postgres password is embedded in the image.

## Railway staging service (not auto-deployed)

Create a **new**, dedicated service in an approved **staging** environment only; **never** replace `bliss-bot`, `Postgres`, `alpha-hf-factory-engine`, or `alpha-hf-staging-health`. Connect the exact reviewed GitHub commit of this candidate, repository root build context, custom Dockerfile path `tools/staging/Alpha346.StripeSandboxReceiver/Dockerfile`, container port `8080`, one replica, 0.25 vCPU / 0.5 GB per-instance upper limits.

Before applying any staged Railway patch, the workspace admin must independently verify that actual subscription/usage and an effective **compute hard stop**, not merely billing alerts, keep ALL authorized combined staging infrastructure within the approved ceiling. Resource caps alone do not cap the monthly bill. Verify no unintended changes to other existing services.

**Default-safe config** (without credentials): `ASPNETCORE_ENVIRONMENT=AlphaStripeSandbox`, `ALPHA346_STRIPE_SANDBOX_WEBHOOK_ENABLED=false`, `ALPHA346_STRIPE_ACCOUNT_ID=acct_1UOn83H23maIy3rz`. Disabled routes intentionally return 404. Do not configure a healthcheck that assumes they return 200; production root `railway.json` healthcheck settings must not be applied to this separate service unchanged. Never connect an unreviewed production database.

## Only after billing and HTTPS authorization

1. Apply the reviewed Dockerfile source on an isolated staged service; ensure no `main` or automatic customer-facing deployment. Verify the disabled 404 route first.
2. Protect a unique test-mode Stripe webhook signing secret `ALPHA346_STRIPE_WEBHOOK_SECRET` and the Npgsql-only `ALPHA346_STRIPE_RECEIPTS_DATABASE` in the staging secret manager. The DSN MUST use database `alpha346_stripe_sandbox`, login `alpha346_webhook_ingest`, `SslMode=VerifyFull`, and the appropriate Neon hostname; never store values in commits/issues.
3. Activate `ALPHA346_STRIPE_SANDBOX_WEBHOOK_ENABLED=true` only after TLS, certificate validation, least-privilege DB access and request size/rate controls are verified.
4. Register one Stripe `livemode=false` webhook destination after verifying the exact public HTTPS route. Send a signed test event and verify `204`, durable receipt readback, retry/replay, forbidden/bad-signature response, and removal/disable procedure. Stripe's test checkout readback alone is insufficient.
5. No `buy.stripe.com` test link on the public site, no real customer payment, no financial journal and no automated fulfillment.

Until actual deployment and signed delivery pass, status is `STAGED_OR_PREPARED_NOT_HOSTED`.
