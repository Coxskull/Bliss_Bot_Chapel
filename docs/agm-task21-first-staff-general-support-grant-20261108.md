# ALPHA Global Media LLC — Task #21 verified staff enrollment and least-privilege staging grant

**Date:** 2026-11-08 (Asia/Manila)  
**Stage only:** Supabase `alpha-first-five-staging`, project `wdlmcriajgsretgyusmo`  
**Existing protected Vercel Preview:** `https://bliss-o5dlori6e-alpha-auto-s-projects.vercel.app/staff/task21/`  
**Production:** unchanged. Global STOP: engaged. Payments/outreach: disabled. Spending: $0 incremental.

## Actual user enrollment evidence

- User opened the Vercel Authentication-protected preview in their browser and supplied a screenshot of the private operator page. The page showed **"Staff account verified — roster approval pending"**, their verified staging Auth UUID, the copy-success toast, and no support-case data. The account identifier is not duplicated in this GitHub report.
- The same Auth UUID was independently checked using Supabase staging `auth.users`, matched **exactly one** account to the separately authenticated connected ALPHA internal Gmail identity, and confirmed email verification, no active ban, one **verified TOTP MFA factor**, and a new Auth session created **2026-11-08 00:16:49 UTC**.
- A verified **factor enrollment** does not prove the current browser access token has `aal2`; sensitive `SECURITY_REVIEW`, `BILLING_REVIEW`, and `PRIVACY_REVIEW` are not granted. No password, MFA code, recovery code, JWT or service secret was requested or stored.
- Owner supplied their Auth UUID specifically for operator-enrollment follow-up. Scope is strictly **one read-only GENERAL_SUPPORT account** in staging.

## Staging-only authorization change — verified

1. Added `scripts/task21/staging-only-operator-grants.sql` without storing the actual UUID or roster record in GitHub. It creates `alpha_support_private.staging_operator_grants` under an existing **unexposed** schema, with RLS ON, an exact HTTPS Vercel-preview origin constraint, and `service_role SELECT` only. Browser `anon` and `authenticated` roles have no table access; the service role cannot write an ACK or grant.
2. Added **service-role-only, SECURITY INVOKER** RPCs `public.alpha_support_staging_origin_allowed(text)` and `public.alpha_support_staging_roles(uuid,text,text)` that fail closed for nonconfigured operators and origins. Database grants permit **GENERAL_SUPPORT only** (no billing/privacy/security roles), and the endpoints are not executable by browser roles.
3. Updated `supabase/functions/alpha-support-operator/operator-handler.mjs` to consume this staging-only private database grant only if `SUPABASE_URL` exactly matches the approved staging project and no environment-variable origin/roster is configured. The old explicit server-side variable roster continues to be preferred if configured. The handler still cryptographically checks the JWT claims and calls `auth.getUser()`, never trusts `user_metadata` or browser-supplied user IDs, and requires a separately allowed origin. Support mutations remain disabled unless the unchanged separate `ALPHA_SUPPORT_MUTATIONS_ENABLED=true` gate is explicitly set, which it is **not** in staging.
4. PostgreSQL17 CI independently tests role SQL/RLS restrictions, wrong-origin and wrong-user denials, factor/AAL input, revocation, general-only role scope. Unit CI additionally tests the revised Edge path, including production-project denial, malformed DB role, revoked role and read-only case update denial.
5. Hosted Supabase staging test applied the exact source inside a bounded **`BEGIN`/`ROLLBACK`** with the independently verified real user identity and protected preview origin. Receipt: **`TASK21_OPERATOR_GRANT_HOSTED_ROLLBACK_PASS`**. No persistent changes resulted from this test.
6. Applied the actual generic schema as a new staging migration **`task21_staging_private_operator_grants_20261108`**, version **`20261108002317`**, advancing staging migration count **54→55**. Independent readback confirmed zero initial grants, RLS ON, no anon/authenticated reads and no server-side grant mutations.
7. Independently verified the user identity and inserted **one single account-specific enabled GENERAL_SUPPORT grant** directly into the staging private database (not source code). It is bound to the exact protected preview origin above. Readback of actual `service_role` RPCs returned **`TASK21_STAGING_GENERAL_SUPPORT_GRANTED_READ_ONLY`**, rejected another user/origin, and showed one OPEN fictional GENERAL_SUPPORT case.
8. The focused GitHub [Task #21 source/database/browser CI run #42429383296](https://github.com/Coxskull/Bliss_Bot_Chapel/actions/runs/42429383296) **passed all 3/3 jobs**. Updated only existing staging Edge Function `alpha-support-operator` from **v1→v2**, `ACTIVE`, **`verify_jwt=true`**. Independent deployed file readback confirmed the new `operator-handler.mjs` is **byte-for-byte identical** to reviewed GitHub source, with function bundle checksum `b7743fe75beebf83eb03c4cef6ddb4d8d86c95edbdf96dea37b26aebda30510b`.
9. Staging readback: **1 enabled grant; GENERAL_SUPPORT only**, exact approved origin YES, other preview origin NO, **1 OPEN fictional general-support case**, **1 unrouted fictional critical alert**. No real customer cases, no auto-delivery, no payments, no outreach.

## Release/security limits

- Preview site is already **READY** and Vercel Authentication protected, but **fresh user login and successful case-list rendering after Edge v2 are not yet verified**; the current screenshot predates the roster grant. Ask the user to **refresh** the already-open private page, authenticate if requested, and report whether the fictional general-support case displays. No credentials or tokens should be pasted.
- Do NOT activate `ALPHA_SUPPORT_MUTATIONS_ENABLED` without a separate bounded synthetic-only operator-case acceptance approval. General-support grant is currently **read only**.
- A separate primary/backup incident alert dispatcher and on-call staffing have not been verified. The manually sent Gmail test and real human ACK were confirmed, but the database critical alert remains `UNROUTED`.
- This is a shared Supabase development branch, **not a production environment or customer-data destination**. No changes to live Bliss Bot Supabase `bkutbglzivfdnoerigyb`, production Vercel domains, Global STOP, or financial systems.

**Status:** first human staging Auth identity + least-privilege grant **PASSED**; hosted operator queue browser readback **PENDING**. Task #21 remains **IN PROGRESS**, draft PR #353 unmerged.
