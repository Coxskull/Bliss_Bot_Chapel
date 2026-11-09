# ALPHA GLOBAL MEDIA — Task #21 controlled existing-staging SQL acceptance

**Test date:** 2026-11-08 (Philippines time)  
**Execution:** Connected Supabase SQL execution, preflight and independent post-rollback readback  
**Authorization:** Operator explicitly authorized existing staging use, synthetic tests only, no production changes or additional spending  
**Source branch:** `feat/agm-task21-support-cases-20261108`  
**Source commit:** `d61729670adfa4406f6e287976c51ab3cd953c67`  
**Repository:** `Coxskull/Bliss_Bot_Chapel`, draft PR #353, unmerged  
**Target:** existing persistent Supabase development branch `alpha-first-five-staging` (`wdlmcriajgsretgyusmo`, branch ID `81949194-906a-429b-8851-5ed295e9b0aa`), parent `Alpha Lead Generation` (`yyesqcyjhqjryaskrqip`). This is **not** live Bliss Bot Supabase (`bkutbglzivfdnoerigyb`).

## Result

**PASS — STAGING SQL TRANSACTIONAL ACCEPTANCE, ALL WRITES ROLLED BACK.**

The staging database returned the exact final readback string:
`TASK21_STAGING_TRANSACTION_ROLLBACK_PASS`.

This is a *real hosted PostgreSQL 17.11 execution of the Task #21 database migration and actual SQL negative/positive acceptance assertions*, not the previously tested disposable GitHub runner alone. It is **not** an Auth sign-in, MFA/roster verification, persistent hosted schema migration, or functioning support service.

## Preflight evidence

- Branch listing confirmed the named non-default, persistent development branch and separate parent; branch status `FUNCTIONS_DEPLOYED`, preview `ACTIVE_HEALTHY`, `with_data=false` as branch metadata (which alone does **not** prove zero real data).
- Branch SQL was available even though the Supabase project-management `get_project` endpoint returned `Project not found`; that management limitation remains.
- Public schema: **0 tables, 2 existing functions**. `alpha_factory`: 37 tables; `alpha_prototype`: 5 tables. Neither pre-existing factory schema was written by this operation.
- Auth schema had 27 tables and 3 pre-existing users (aggregate count only; no personal information fetched). No application Auth logins were performed.
- The actual Bliss Bot intake prerequisite `public.alpha_public_intake_requests` was absent; therefore the Task #21 migration **cannot** be permanently applied as-is to this factory staging branch. A disposable public fixture was required for the reversible test.
- Staging migration inventory: **52 existing migrations**, latest `20261106044740`.

## Exact bounded execution procedure

One SQL execution batch opened `BEGIN`, set local statement timeout 45 seconds and lock timeout 4 seconds, and refused to continue if a public intake/case/outbox object already existed. Its inputs were loaded directly from the exact GitHub source commit:

1. `scripts/task21/isolated-intake-fixture.sql` — skipped the CI-only `CREATE ROLE`, `CREATE SCHEMA auth` and `CREATE TABLE auth.users` lines because Supabase already provides those objects. Created only the temporary-in-transaction *public* intake, receipt and outbox prerequisites.
2. `supabase/migrations/20261108_alpha_support_case_workflow.sql` — the **actual full production-candidate SQL**, unmodified, applied *inside the transaction*.
3. `scripts/task21/isolated-postgres-assertions.sql` — the **actual full tests**, unmodified, using fictional emails and synthetic account UUIDs within the transaction. Verified eight support cases, nine receipts, four non-sensitive GHL outbox entries, category/severity routing, notice-version denial, service-role/anonymous SQL privilege denial, idempotent claim, second-operator denial, owned-case transition constraints, immutable history, resolution closure and retention cascade.
4. Executed `ROLLBACK` **within the same SQL batch**; a final read-only SQL guard asserted that all four public fixture and support objects were absent.

No actual HighLevel delivery, public webhook, email, refund, support inbox, live Supabase Edge function deployment, browser sign-in or user TOTP setup was triggered. PostgreSQL roles/auth schema were not created, altered or removed permanently. All synthetic test inserts and schema changes were rolled back.

## Independently verified post-rollback state

A separate read-only SQL execution confirmed:
- `public` tables: **0** (same as preflight).
- `public` functions: **2** (same as preflight).
- Auth users: **3** (same as preflight).
- `public.alpha_public_intake_requests`: absent.
- `public.alpha_support_cases`: absent.
- `public.alpha_support_case_events`: absent.
- `public.alpha_ghl_delivery`: absent.
- `list_migrations`: **52**, latest `20261106044740` (same as preflight).

## Remaining release and hosted acceptance gates

**Do not mistake this result for a persistent staging deployment.** The branch contains factory-specific schemas and three existing Auth accounts. It does not contain the live Bliss Bot public intake schema. No verified private Task #21 hosted frontend origin, pre-approved Auth identity mapping, role roster or monitored on-duty incident alert exists yet; rollback/backup acceptance for persistent changes remains outstanding. Project-management read access is restricted.

Before any persistent migration or Auth user enrollment on this shared staging branch, the owner/operator must confirm isolation/backup/rollback, scope separation from the other factory staging work, approved private host, actual existing Auth user identity, and legitimate human support coverage. Otherwise continue with the already verified rollback-only/CI synthetic acceptance, which incurs no additional service spend.

**Launch status:** Task #21 still **IN PROGRESS**. No production changes, extra costs, Global STOP release or final operator staffing/notification acceptance.
