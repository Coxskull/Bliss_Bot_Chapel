# Task #21 — existing Supabase staging private operator API deployment receipt

**Date:** 2026-11-08 (Asia/Manila)  
**Operator mandate:** Existing staging only, incremental spending $0, no production changes, Global STOP unchanged.  
**Repository:** `Coxskull/Bliss_Bot_Chapel`, [draft PR #353](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/353).  
**Implementation snapshot:** unmerged Task21 source at PR head `ff1f223dee7cb7647334eef0f7f45d3c58ea5b62` before the staging-only dashboard configuration follow-up commits.  
**Target:** connected existing `alpha-first-five-staging` Supabase branch, project ref `wdlmcriajgsretgyusmo`, parent `yyesqcyjhqjryaskrqip`.  
**Never deploy to:** Bliss Bot production `bkutbglzivfdnoerigyb` under this directive.

## Deployment evidence

- Deployed new, uniquely named `alpha-support-operator` Edge Function to the connected *development-branch project* using the exact three existing source files: `index.ts`, `operator-handler.mjs`, and `operator-contract.mjs`.
- Supabase returned `ACTIVE`, `version=1`, `verify_jwt=true`, function ID `ba53e002-fcd9-4ffc-9b6e-3fac4e70806e`, and code hash `b1a06ca79ccf3efe177878f164ba778ba0f77953b928fa66361927cc6f88464e`.
- Independent `list_edge_functions` readback confirmed exactly this staging function `ACTIVE` v1 with JWT verification enabled. `get_edge_function` independently returned the deployed source containing the required server-only `ALPHA_SUPPORT_OPERATOR_ROSTER` and `ALPHA_SUPPORT_MUTATIONS_ENABLED` checks plus `auth.getClaims` verification.
- No server operator roster was configured, no permitted frontend origin was configured, and the mutations flag was not enabled. Consequently the handler fails closed and cannot authorize anyone to read/write cases, even with an otherwise valid generic Auth login. A public URL exists but is not an approved staff login or published dashboard.
- No live HTTP status proof was obtained: a separate unauthenticated web fetch could not access the function. Deployment metadata and source verification are solid; **do not claim a tested 401 or logged-in live journey** from this evidence.

## Actual database boundary

The same development branch previously passed the entire Task21 SQL plus synthetic assertions within an explicit, independently verified `ROLLBACK` ([evidence](agm-task21-staging-rollback-acceptance-20261108.md)). The staging public schema still lacks the actual Bliss Bot intake and support tables; this Edge Function therefore has **no live support queue** to load. The branch also hosts existing factory schemas and three Auth accounts, so it is *shared* staging rather than disposable/approved customer-data staging. No table migrations or persistent customer data were added as part of the function deployment.

## Private browser staging safety

The draft private dashboard and its generated response-header template were corrected to pin the *staging* Supabase URL `https://wdlmcriajgsretgyusmo.supabase.co` with explicit `environment: "staging"`. The live Bliss Bot Supabase URL is rejected by the staging dashboard configuration. The UI also provides an identity-only **Auth UUID** readback after successful Auth verification if the roster denies access, but reveals **no cases**. A real staff sign-in and secure private HTTPS origin are not yet operational.

The private static bundle remains **unhosted** and excluded from the public Vercel build; `operator-config.js` is intentionally absent from source and artifacts. The latest exact-head Chromium and unit tests must pass before treating the UI change as verified.

## Remaining launch gates

1. Establish an approved private HTTPS staging origin, correct locked SDK/transitive dependencies, CSP/HSTS/no-store, and a publishable staging key (not a service-role key).
2. Verify the nominated operator's real Supabase Auth UUID, and MFA enrollment in the approved staging account only. Never request passwords, MFA setup secrets or six-digit codes over chat.
3. Configure the **server-only** role roster for least-privilege `GENERAL_SUPPORT`, plus explicit AAL2 for sensitive roles, using an authorized secrets-management process. This connector currently offers no direct secret-setting action; do not bypass with SQL or paste secrets into source control.
4. Obtain explicit isolation/backup/rollback proof and avoid interfering with the existing factory. The staging support public tables are absent; the original Task21 migration cannot simply run persistently without its real intake prerequisites.
5. Complete a genuine hosted fictional intake→receipt→case→human claim→resolution and separate notification/ack receipt test. No real customers, CRM sends, refunds or production release.

**Task #21:** IN PROGRESS. The API deployment alone does not constitute hosted operational acceptance.
