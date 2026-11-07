# Task #21 — support cases and incident routing (source candidate)

**Status: IN PROGRESS, NOT DEPLOYED, NOT HOSTED-ACCEPTED.** Global STOP remains engaged. No payments, customer contact, or HighLevel sync were performed by this change. No new Railway service or subscription.

## Reused, not rebuilt
- `supabase/functions/alpha-public-intake/index.ts`: existing allowlisted-origin public inquiry, consent, nonce/idempotency, rate limit and durable receipt.
- `public.alpha_public_intake_requests` and `public.alpha_public_intake_receipts`: existing transactionally issued receipt and 90-day retention.
- `public.alpha_ghl_delivery`, `alpha_ghl_claim`, `alpha_ghl_finish`, existing retry: claim/finish outbox for approved, non-sensitive contact delivery. The working sibling adapter is imported by the retry function.

## Incremental implementation
1. Support kind and `AGM-SUPPORT-2026-11-08` consent are narrowly added to the existing intake handler and schema constraints; no new public API endpoint.
2. New private `alpha_support_cases` and append-only-operational `alpha_support_case_events` tables. On insert the database atomically creates a UUID case reference, initial OPEN event, and receipt. New public form shows **both** case ID and receipt or does not report success. Distinct request keys permit repeated cases; same key remains idempotent.
3. Eight categories: general, advertiser, creator (NORMAL); billing, refund, failed_campaign, privacy (HIGH); security_incident (CRITICAL). Role-based queue routing is automatic but **not a verified human notification**.
4. A service-role-only transition RPC serializes updates with `FOR UPDATE`, checks named operator identity in Auth, requires owner+resolution for closure, and writes status/assignment/resolution events transactionally. Status and history cannot be directly queried by anon/authenticated browser roles.
5. HighLevel accepts *only* name/email from eligible general, advertiser, creator support requests, and *only* when both existing `ALPHA_GHL_SYNC_ENABLED` and new `ALPHA_GHL_SUPPORT_SYNC_ENABLED` are true. Sensitive billing/refund/campaign/privacy/security cases create **no** CRM outbox row. Free-text data and case history never cross the generic CRM adapter.
6. Case and event history is protected from direct role DELETE/UPDATE; historical rows are cascaded only when their parent intake request is lawfully purged via the existing retention process. No new cron is created.
7. Public requests cannot activate an isolated test by supplying a header. Synthetic testing is restricted to isolated CI/disposable PostgreSQL fixture, never the live service or customer CRM.

## Protected human operator backend (draft, not hosted)

- `alpha-support-operator` is a private Supabase Edge Function in the **existing** project, explicitly `verify_jwt=true`, never a new Railway service.
- Each request requires signed Supabase Auth user JWT **and** live Auth user lookup, claim verification, and exact server-only operator roster membership. Sensitive billing/privacy/security queues require JWT `aal2`; ordinary login alone cannot expose them.
- `GET` lists scoped open cases in 20-item pages; `GET ?case_id=<uuid>` returns case details/history only to an authorized role. Unauthorized and nonexistent case IDs both return 404.
- `POST` performs a service-only audited transition only when `ALPHA_SUPPORT_MUTATIONS_ENABLED=true` has separately been approved and configured. Operator owner assignment must belong to the matching role. Default is read-only and fail-closed.
- A valid roster is *permission configuration*, not proof that a person is on duty or received an alert. No staffed human notification channel is claimed until real delivery/readback and escalation ownership are witnessed.
- Only a narrowly allowlisted operator frontend Origin can receive CORS headers, if the owner later configures `ALPHA_SUPPORT_OPERATOR_ORIGIN`; no public frontend is connected yet. Service-role key remains inside Edge runtime.

## Verification plan and strict interpretation
- Offline CI: `node scripts/agm-support-unit-smoke.mjs`, `node scripts/agm-support-operator-unit.mjs`, `node scripts/agm-ghl-unit-smoke.mjs`.
- Disposable PostgreSQL 17 CI: create a matching *synthetic baseline fixture*, execute the proposed migration and real SQL inserts/status transitions; assert 8 cases, 9 intake receipts, 4 eligible CRM outbox rows, no sensitive forwarding, role-level denials, closure audit and retention cascade. The baseline fixture is not a verified full copy of the deployed schema, so this is **not live acceptance**.
- Verify existing GitHub Actions checks at the *exact branch commit*. A green badge alone cannot establish deployed functionality.

## Remaining acceptance gates — never automatically mark Task #21 complete
1. Designated, reachable human operators must be recorded for security/privacy/billing and general support, with verified alert/response channel and cover. The database's role queue is not itself a delivery channel or an SLA.
2. Owner-approved support/privacy notice, contact and dispute/refund policy, plus appropriate on-call and legal review. No refund transaction may be issued by the case workflow.
3. Security-reviewed migration applied first to an **explicitly authorized isolated environment**, not the running Bliss Bot DB. Validate all existing schema constraints/triggers/functions and old creator/advertiser/privacy behavior.
4. Deploy the existing intake Edge Function, the private operator function and updated public support form **only after** human routing and notice approval. Preserve Global STOP and run one server-authorized synthetic end-to-end test with a separate isolated database and disabled CRM automations. Verify saved request→receipt→case→event→owner transition→resolution, invalid key/origin and sensitive CRM exclusion.
5. Independently validate actual HighLevel outbox claim/finish/readback only in an approved sandbox location with automation disabled. Never send fictional records to the live HighLevel location or turn support CRM enablement on implicitly.
6. Record environment, deployed function version/image commit, test case IDs (synthetic only), redacted DB readbacks, CI run URLs, designated staffed channel and owner sign-off in PRE-LAUNCH MASTER 20261105, Task #21.

**Explicit non-goals:** activating a paid order flow, a new chatbot, payment/refund execution, new Railway service, new subscriptions, emergency STOP release, or public claims of staffed support before acceptance.
