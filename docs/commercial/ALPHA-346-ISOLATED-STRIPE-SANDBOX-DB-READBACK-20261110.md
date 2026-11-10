# ALPHA-346 — Isolated Stripe SANDBOX receipt database: actual provider readback

**Verified:** 2026-11-10 UTC. **Scope:** test-mode only, NO live customer data, charges, production deployment, or fulfillment.

## Existing free staging resource used

- Provider/plan: Neon organization `Alpha Global Media.`, `free` plan; no new paid project or Railway service was created.
- Existing staging project: `alpha-task22-staging` (`empty-glitter-56197229`); its default Neon branch is `br-old-frog-bdmrtc9y`. It is separate from Railway's production Postgres and all Supabase production projects.
- **NEW separate database:** `alpha346_stripe_sandbox`, owned by the existing `neondb_owner`, created through the authenticated Neon API.
- Applied the existing source migration `Bliss.Infrastructure/Migrations/Alpha346PlanningSandboxStripeReceiptsV1.sql` to **only this new database**, using a single transaction. The resulting `alpha_sandbox.planning_checkout_webhook_receipts` table has no rows and contains only technical receipt IDs/states/body hashes; no personal data was copied.
- Created dedicated role `alpha346_webhook_ingest` scoped to this database and the `alpha_sandbox` schema. The role has SELECT + INSERT on the technical receipt table; its credential was NOT written to GitHub. No production connection string was used.

## Independently verified through connected Neon SQL

After applying the migration and grant, privilege query for `alpha346_webhook_ingest` returned:

| Permission/test | Actual readback |
| --- | --- |
| `USAGE` on `alpha_sandbox` | true |
| `CREATE` on `alpha_sandbox` | false |
| `SELECT` on the receipts table | true |
| `INSERT` on the receipts table | true |
| `UPDATE` on the receipts table | false |
| `DELETE` on the receipts table | false |
| `TRUNCATE` on the receipts table | false |
| Current receipt rows | 0 |
| SQL query **executed as the restricted role** | `current_user=alpha346_webhook_ingest`, `current_database=alpha346_stripe_sandbox`, receipt count 0 |

The applied source also includes a one-paid-test-per-PaymentIntent partial unique index and UPDATE/DELETE/TRUNCATE rejection triggers. These safeguards were installed, but this provider round did **not** perform signed live-Stripe delivery or write synthetic receipts; do not overstate the acceptance.

## Hosting and safety gate STILL OPEN

The existing `.NET 8` receiver is at `tools/staging/Alpha346.StripeSandboxReceiver/`. It has passed CI/localhost synthetic acceptance and is **not** deployed.

The connected Railway `alpha-henry-ford-factory-staging` project has only a factory engine and health service; do not repurpose them. Railway `bliss-bot` has only a production environment/database; do not stage webhooks there. Vercel Bliss portal previews are static and protected; they are not a verified .NET webhook host.

Before enabling Stripe webhooks:
1. Obtain an authorized isolated HTTPS host for the reviewed .NET receiver with verified *effective* spend controls; no new service/subscription/spend without approval.
2. Set protected `ALPHA346_STRIPE_RECEIPTS_DATABASE` as an **Npgsql connection string**, targeting only `alpha346_stripe_sandbox`, login `alpha346_webhook_ingest`, `SslMode=VerifyFull`; install credentials directly in its protected manager, never in source or issues. Validate Neon certificate and Npgsql compatibility.
3. Configure `ASPNETCORE_ENVIRONMENT=AlphaStripeSandbox`, `ALPHA346_STRIPE_SANDBOX_WEBHOOK_ENABLED=true`, and exact sandbox account ID, plus a **newly registered test endpoint's unique** `whsec_` secret.
4. Only after the public HTTPS route is verified, register a Stripe **test-mode** endpoint and test actual Stripe delivery/retries/replay idempotency with durable database readback. Disable/remove test destination when no longer used.

### Acceptance statement

**COMPLETE:** separate free-tier staging database, immutable receipt schema, restricted ingestion role, verified SQL read access and privileges.

**NOT COMPLETE:** hosted HTTPS receiver, Stripe test-mode webhook destination, real signed event delivery and receipt acceptance, external-retry test, production/legal/live merchant readiness.

No customer-facing checkout URL, public payment surface, CRM synchronization, campaign fulfillment, or global STOP change is authorized by this database preparation.
