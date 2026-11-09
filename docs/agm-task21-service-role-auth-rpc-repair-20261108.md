# Task #21 — Real Supabase service-role privilege repair and staging acceptance

**Date:** 2026-11-08 (Asia/Manila)  
**Target:** existing `alpha-first-five-staging` branch, ref `wdlmcriajgsretgyusmo`, parent `yyesqcyjhqjryaskrqip`. **No production changes, no extra spending, no real customer data.**  
**Source revision:** `d176b7614398c93b74fb2976173e0832b0b195ee`, [draft PR #353](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/353).  
**Focused CI:** [run #42419729609](https://github.com/Coxskull/Bliss_Bot_Chapel/actions/runs/42419729609), **all three jobs passed**.

## Root cause — actual hosted permission readback

In the real hosted staging database, PostgreSQL returned `has_table_privilege('service_role','auth.users','SELECT') = false` and `service_role.rolbypassrls = true`. The older Task21 `SECURITY INVOKER` functions `public.alpha_support_claim` and `public.alpha_support_transition` queried `auth.users` directly. Those functions would fail when invoked from the real Supabase service-role API even though earlier tests under superuser succeeded.

## Minimal corrective implementation

1. Keep the exposed public claim/transition RPCs **SECURITY INVOKER**, and continue to deny their EXECUTE privilege to `PUBLIC`, `anon`, and `authenticated`.
2. Introduce **one private, non-exposed schema** `alpha_support_private` and `alpha_support_private.auth_user_exists(uuid)`, a tiny `STABLE SECURITY DEFINER` boolean-only identity-existence lookup using a fixed `pg_catalog` search path and fully qualified `auth.users`. The function owner is the trusted migration owner.
3. Explicitly revoke private schema usage and private function execution from anonymous and normal authenticated roles; grant only the required schema USAGE/function EXECUTE to `service_role`. **Do not grant `service_role` direct SELECT on `auth.users`.**
4. Maintain server-side Edge `auth.getUser(jwt)` plus `auth.getClaims(jwt)`, exact server-only role roster and AAL2 checks. The database helper only checks Auth-record existence, not staff role. The public RPC remains service-only.
5. Extend actual PG17 CI acceptance so fixture `service_role` has the real Supabase `BYPASSRLS` property but **not** `auth.users` SELECT; switch to `SET ROLE service_role`, verify private identity lookups, idempotent self-claim, one audit event and legitimate transition, plus negative tests for unauthorized/exposed helper execution.

Security references: [Supabase Database Functions](https://supabase.com/docs/guides/database/functions) and [Security Definer Advisor](https://supabase.com/docs/guides/observability/advisors?queryGroups=lint&lint=0029_authenticated_security_definer_function_executable).

## Hosted staging test with explicit rollback

Using exact repository source (fixture with `CREATE ROLE`/`auth.users` creation omitted because these objects already exist), the connected Supabase staging SQL runner executed:
- `BEGIN;` with 90-second statement timeout and 4-second lock timeout.
- A collision guard ensuring no pre-existing public intake/case/outbox tables or `alpha_support_private` schema.
- The full existing synthetic intake fixture, the actual corrected `20261108_alpha_support_case_workflow.sql` migration and the actual expanded `isolated-postgres-assertions.sql` tests.
- `SET ROLE service_role` inside the acceptance script; verify the service identity helper, real claim/transition and limited privileges.
- `ROLLBACK;` followed by a positive non-persistence guard.

**Exact returned receipt:** `TASK21_STAGING_SERVICE_ROLE_ROLLBACK_PASS`.

A **separate read-only database query after the test** verified: public tables **0**, public functions **2**, Auth users **3**, `alpha_support_private` absent, public support cases/events absent; migration history **52** with latest version `20261106044740`. No permanent data or schema change.

## Remaining limitations

- This is a **real hosted PostgreSQL execution** and a privilege-contract fix, not a persistent support database installation or end-to-end authenticated browser acceptance.
- The previously deployed separate staging `alpha-support-operator` Edge API is **ACTIVE v1**, `verify_jwt=true`, but its roster/origin are intentionally not configured, case mutations default OFF and the staging public intake/support schema is still absent.
- The first human operator is nominated but their actual Auth UUID has not been verified by secure sign-in, assigned to the server roster, or linked to an on-duty alert/ack channel.
- The private dashboard build is staging-pinned and has passed an isolated browser simulation, but remains unhosted.
- No live production, payments/refunds, campaigns, CRM sends, customer information or Global STOP setting was changed.

**Launch decision:** Task #21 remains **IN PROGRESS** until separately controlled persistent staging prerequisites, private hosted identity/MFA, real staffed notifications, synthetic end-to-end closure, and policy acceptance pass.
