# ALPHA Global Media — Task #21 persistent staging acceptance ledger

**Date:** 2026-11-08 (Asia/Manila)  
**Goal:** reusable existing resources, no incremental spend, real product evidence with fictional records only.  
**Target:** existing Supabase development branch `alpha-first-five-staging` (`wdlmcriajgsretgyusmo`), parent `Alpha Lead Generation` (`yyesqcyjhqjryaskrqip`). **Not Bliss Bot production** (`bkutbglzivfdnoerigyb`).  
**Source:** draft [PR #353](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/353), unmerged.

## Real completed staging operations

1. Reused the previously deployed `alpha-support-operator` Edge Function: **ACTIVE v1**, platform `verify_jwt=true`; handler's server-only roster is unconfigured and mutations default OFF. No existing production function was modified.
2. Added [staging-only intake prerequisite](../scripts/task21/staging-only-intake-prerequisite.sql) with an explicit collision guard, RLS and service-role-only grants. This does not create a new infrastructure service, paid project or CRM sender. The prerequisite reuses existing names/constraints required by the real code.
3. Composed this baseline with the **existing** `20261105_alpha_ghl_contact_delivery.sql` migration and the **existing repaired** `20261108_alpha_support_case_workflow.sql` migration. Executed the entire stack first in `BEGIN`/`ROLLBACK` on hosted staging PG17 with an actual `SET ROLE service_role` synthetic intake and checks of case, receipt, immutable creation event and dormant CRM outbox. Database returned **`TASK21_FULL_STAGING_STACK_ROLLBACK_PASS`**.
4. Applied the **same exact three source files** as one atomic Supabase migration: `task21_staging_support_stack_20261108`. Supabase returned `success:true`, recorded migration `20261107222825`. Migration history advanced **52 → 53**, staging only. No existing factory table or Auth account was changed.
5. Independent hosted SQL readback confirmed the five **new** `public` tables: `alpha_public_intake_requests`, `alpha_public_intake_receipts`, `alpha_ghl_delivery`, `alpha_support_cases`, `alpha_support_case_events`. **All five have RLS ON**, `anon` and `authenticated` table `SELECT=false`, `service_role SELECT=true`. The claim RPC is not executable by normal authenticated users and the `alpha_support_private` schema has no authenticated access.
6. Independent readback confirmed shared `alpha_factory` table count **37**, `alpha_prototype` count **5**, existing `auth.users` count **3**. The new public tables and outbox were initially **empty** after migration.
7. Persisted exactly **one fictional general support request** via `SET ROLE service_role`, with fixed request key `5a6b3f11-1234-4a1f-9a44-58e1e0000021`, `@example.invalid` contact, `marketing_opt_in=false`, and explicit `SYNTHETIC / DO NOT CONTACT` labeling. Atomic trigger readback: **case ID `931d5569-9958-4603-b3c4-fdad25c1acd6`, OPEN, NORMAL, GENERAL_SUPPORT, 1 receipt, 1 CREATED event**.
8. Immediately set that fictional fixture's GoHighLevel outbox row to **BLOCKED** with `invalid_record` classification, independently verified; no staging GHL-sender Edge Function exists. **No messages were sent**. The fixture exists only in staging for an eventual authenticated operator readback.

## Additional real hosted case-lifecycle acceptance (rolled back)

After persistent schema installation and the one durable general-support fixture, a second **real staging PostgreSQL** test opened a bounded transaction, created a temporary **fictional** Auth user (not an actual operator identity), set the current database role to the genuine restricted `service_role`, and inserted a fictional `security_incident`. It verified CRITICAL/SECURITY_REVIEW routing, verified **zero sensitive HighLevel outbox rows**, successfully called the real `alpha_support_claim` and `alpha_support_transition` RPCs, and completed `OPEN → TRIAGED → IN_REVIEW → RESOLVED → CLOSED` with five durable audit events **inside that temporary transaction**.

The database returned **`TASK21_HOSTED_STAFF_RPC_TRANSACTION_PASS`** after `ROLLBACK`. An independent guard established that the single persistent OPEN general fixture, its one receipt/event, the blocked HighLevel row, and the original three Auth accounts were unchanged. This is a real database role/transaction test, **not evidence of human Auth login, real case ownership, operator availability, or alert delivery**.

## Security warning scope

The Supabase security advisor reports 25 RLS-with-no-policy informational notices; five are Task21 private service-role tables that intentionally allow no direct browser access. It also reports a signed-in-user-executable SECURITY DEFINER function `public.record_prospect_verification_review` belonging to the **pre-existing factory workflow**, not Task21; do not mutate an unrelated factory function without a separate scope review. See [Supabase lint remediation](https://supabase.com/docs/guides/database/database-linter?lint=0029_authenticated_security_definer_function_executable). The advisor separately reports **Leaked Password Protection Disabled** on staging Auth: review before authenticating real staff, see [password security](https://supabase.com/docs/guides/auth/password-security#password-strength-and-leaked-password-protection).

## Private operator preview attempt and quota boundary

The existing `bliss-bot` Vercel project `prj_bWOvwDWg3zDXoduS1utRwKnYautF` belongs to the user's existing Hobby team and protects preview domains through Vercel Authentication (`all_except_custom_domains`). The draft branch's operator preview build has explicit triple guard: `VERCEL_ENV=preview`, exact `VERCEL_GIT_COMMIT_REF=feat/agm-task21-support-cases-20261108`, and `ALPHA_TASK21_STAGE_PREVIEW=enabled`. Only the existing staging Supabase publishable key was placed in **branch-scoped Preview-only** Vercel variables, never in source control, production variables or browser service credentials. The preview source bundles the pinned Supabase SDK locally; no extra project is created.

**Vercel rejected an explicit PREVIEW deployment with HTTP 402 `api-deployments-free-per-day`: more than 100 API deployments in the rolling daily window; retry after 86400 seconds.** The existing subscription was not upgraded and the production alias was not changed. Consequently **NO new Task21 private preview was successfully deployed or verified**. The prepared preview feature remains unserved and must pass an exact-head build and runtime security review after quota resets.

## Controlled rollback plan (not executed)

A fail-closed, dependency-RESTRICT [staging-only rollback script](../scripts/task21/staging-only-safe-rollback.sql) exists in source. It refuses to run if there is any unexpected case/contact/CRM record or a factory schema count change. It must never be run on production. Dropping schema objects would still leave the Supabase migration ledger history and would require separate migration reconciliation. **No rollback was needed for the successful persistent staging migration.**

## What remains before Task #21 can be marked COMPLETE

- **Identity**: nominated staff operator must authenticate with an actual staging Supabase Auth account, verify MFA (AAL2 for sensitive categories) and provide their own verified Auth UUID by a trusted channel. The three existing Auth users are aggregate evidence only and have not been attributed to the nominated operator.
- **Access**: approved server-only `ALPHA_SUPPORT_OPERATOR_ROSTER` with minimum `GENERAL_SUPPORT` scope and one approved private origin; no roster or approved origin currently exists. API correctly fails closed. No production role changes.
- **Private host**: quota-cleared Vercel SSO-protected PREVIEW or another pre-existing, explicitly secured host, exact origin, CSP/HSTS/no-store and a lockfile/integrity review. Do not place a service-role key or secrets in browser config.
- **End-to-end authenticated UI**: staff login→view fictional case `931d5569-9958-4603-b3c4-fdad25c1acd6`→human claim→audited in-review/resolved/closed transitions. Mutations remain OFF until a separate scoped staging acceptance approval and proper operator identity.
- **People / policies**: actual monitored incident/complaint channel with human alert delivery and timestamped ACK, a backup owner for time off, policy approval for failed campaigns, refunds/disputes, privacy requests, security incidents, staffing response times; HighLevel sandbox opt-in/opt-out acceptance with no sensitive leakage; final launch decision.
- **Release control**: maintain Global STOP, leave PR #353 draft/unmerged, do not communicate with real customers, charge/refund, or deploy production.

**Status: STAGING DATABASE ACCEPTED; CUSTOMER SUPPORT SERVICE NOT YET COMPLETE.** This ledger is proof of real engineering output and explicitly does not claim hosted staffed support.
