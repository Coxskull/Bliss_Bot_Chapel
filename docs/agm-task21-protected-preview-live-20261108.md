# ALPHA GLOBAL MEDIA LLC — Task #21 Protected Operator Preview Deployment Evidence
**Date:** 2026-11-08 Asia/Manila  
**Task:** Pre-Launch Master #21 — protected human-support operator staging activation  
**Status:** PREVIEW DEPLOYED; REAL HUMAN AUTHENTICATED E2E NOT YET ACCEPTED  
**Scope:** existing Vercel Hobby team and existing Supabase development branch, no incremental spending, no production changes, Global STOP engaged.

## Actual new deployment — verified from Vercel deployment record and build logs

- Existing Vercel project `bliss-bot`, ID `prj_bWOvwDWg3zDXoduS1utRwKnYautF`, team `team_TmcvMnL7h33LpCqjfGbQdRoZ`.
- **NEW READY protected Preview deployment:** `dpl_34uJaD9ShDNqwVJVfwpDnHVkyUo9`.
- **Exact Git source:** `Coxskull/Bliss_Bot_Chapel`, branch `feat/agm-task21-support-cases-20261108`, commit `86fbd0e635c8bd38db216909a95a319cfeab5ebb` (draft PR #353; unmerged).
- **Deployment URL:** `https://bliss-o5dlori6e-alpha-auto-s-projects.vercel.app/staff/task21/`.
- **Inspector:** https://vercel.com/alpha-auto-s-projects/bliss-bot/34uJaD9ShDNqwVJVfwpDnHVkyUo9
- Vercel deployment target: **preview** (not production); deployment state `READY`. Its only returned alias belongs to the branch preview `bliss-bot-git-feat-agm-task21-supp-f831be-alpha-auto-s-projects.vercel.app`.
- Independent Vercel project readback: preview protection setting `ssoProtection.enabled=true`, `deploymentType=all_except_custom_domains`. This covers the Vercel preview hostname; it does not establish a signed-in browser session by itself.
- **Vercel build events:** cloned the exact draft commit; ran `vercel build`; output `TASK21_VERCEL_PROTECTED_STAGING_PREVIEW_BUILT: staging-only HTTPS origin, read-only, same-origin SDK, no sensitive keys`; completed deployment successfully. The previous HTTP 402 daily API deployment quota blocker **cleared for this one deployment**, without upgrading service or spending.
- Branch-scoped Preview-only environment variables verified by metadata (value not disclosed): `ALPHA_TASK21_STAGE_PREVIEW` and `ALPHA_TASK21_STAGING_PUBLISHABLE_KEY`. No `service_role` key, Auth token, password, TOTP seed or roster entered into GitHub or this evidence.
- Private bundle generated at `/staff/task21/` only on exact approved preview branch with `VERCEL_ENV=preview` and explicit opt-in flag; compiled with committed transitive integrity lockfile. GitHub previous source checks passed. No source code or app-level support data was moved into the production site by this deployment.

**Current limitation:** No real interactive browser navigation, Vercel SSO authentication, response headers readback, browser Auth login, MFA challenge, or case queue load was executed by the engineering connector. Vercel state/SSO configuration and build logs are verifiable; do not misrepresent them as an authenticated human end-to-end test. User should sign in to the protected URL with their own authorized Vercel identity, then use their existing staging Supabase Auth account and authenticator. Do not share the password, JWT, browser cookies, recovery codes or MFA seed via chat.

## Existing authenticated account discovered via read-only Supabase staging SQL

- **Only one** `auth.users` account matches the existing, separately connected internal operator Gmail address in the staging project `wdlmcriajgsretgyusmo` (3 Auth users in total).
- That matching staging account has **email confirmed**, **not banned**, and **one verified TOTP factor** (`auth.mfa_factors` aggregate).
- The last recorded session creation for the matching account predates this preview (2026-11-06 01:25:24 UTC). This does **not** show a successful login to the new site or a currently valid AAL2 session.
- **Privacy:** intentionally do not publish the email, Auth UUID, factors, session IDs, tokens, or cookies into public GitHub evidence. Roles are not inferred from an email match; real fresh Auth login and trusted operator identity verification remain required.

## Server access controls and staging data

- Existing Supabase staging project `wdlmcriajgsretgyusmo` hosts `alpha-support-operator` Edge Function v1, `ACTIVE`, `verify_jwt=true`.
- Server-only `ALPHA_SUPPORT_OPERATOR_ROSTER` and `ALPHA_SUPPORT_OPERATOR_ORIGIN` are not configured; `ALPHA_SUPPORT_MUTATIONS_ENABLED` is OFF. The API therefore continues to **deny case access**, even to an otherwise authenticated user; the private frontend can display only the verified user's own Auth UUID to assist approved enrollment.
- Existing permanent fictional cases: general case `931d5569-9958-4603-b3c4-fdad25c1acd6` (one blocked CRM outbox record) and fictional critical security case `90d58c01-c9ad-464a-99a9-dee900b09d98` with alert `1cf5e519-33e2-4d66-8984-185cc93f3641` still `UNROUTED`. No real customer or incident. Human manually sent internal Gmail test was independently ACKed Nov 8, 07:37:20 +08, but application notification is **not automated**.

## Remaining P0 gates to mark Task #21 complete

1. User performs a fresh login on this protected preview with their authorized Vercel identity and the **existing** staging Supabase Auth account; verify own UUID and TOTP/AAL2 without pasting secrets into ChatGPT.
2. A trusted staging admin approves the precise Auth UUID, configures exact protected preview origin and minimum `GENERAL_SUPPORT` server-only roster. This connector currently has no supported direct Edge secret-setting action; do not hardcode identity or disable JWT checks to shortcut it.
3. Verify authenticated case listing; separately approve bounded **synthetic-only** stage mutation enablement; then a genuine operator claim, audited status transition and closure.
4. Implement and verify a safe **application-generated** incident alert route and real primary/backup staff monitoring. The database critical intent remains `UNROUTED`; a manual Gmail test and human reply do not prove an automated service.
5. Confirm owner/finance/legal acceptance of complaint, refund, disputes, privacy, security and failed-campaign policies, and sandbox GHL consent/opt-out, before any production release.

**Release:** NO-GO for customer support launch; draft PR #353 stays unmerged and Global STOP remains engaged.
