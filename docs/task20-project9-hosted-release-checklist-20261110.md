# Task #20 — guarded $9 Ask Alpha release / recovery handoff

**Scope:** Existing Bliss Bot Supabase `alpha-public-intake` only; source integration commit later than PR #382. As of November 10, 2026, the actual hosted database still has the original **$10** SQL reservation expression, **AI enabled=false**, and zero AI receipt rows. This document is a handoff and synthetic acceptance basis, **not approval to perform production or confidential database mutations**.

## Owner's spending policy
- **$10** funded OpenAI API credits; **$9** enforced Ask Alpha Production project spending limit; **$1** held in reserve. Do **not** lower the account project cap to $8, buy credits, or imply the $1 margin is a guarantee against eventual provider billing overage.
- Application Edge constant and forward-only SQL governor cap are both **$9** in nonproduction source. The project spend screenshot does **not** prove that an installed service key is scoped to the pictured OpenAI project.

## Required preflight (NO MUTATION)

1. Confirm isolated recovery/backup gate for this project, owner/operator access and a rollback point. **Do not bypass Task #1** merely because this is a small change.
2. Verify `public.alpha_ask_alpha_settings.enabled=false`, count of `public.alpha_ask_alpha_turns`, `externally_billed_usd`, and exact installed `pg_get_functiondef` source: precisely one known legacy `>10.000000` expression, or already upgraded `>9.000000`. If neither or conflicting modifications, stop.
3. Verify `alpha_ask_alpha_reserve` and `alpha_ask_alpha_settle` remain SECURITY DEFINER with restricted EXECUTE for only server-side service roles, and private receipt tables retain RLS/FORCE. Never use untrusted client headers or credentials in a test.
4. Independently verify API key/project scope, provider project budget, external usage meter freshness, owner-approved AI processing disclosure, human support escalation, and ingress identity overwrite before **any** positive inference or public AI enablement.

## Deployment, only after the above gates

- Apply the separately versioned `supabase/migrations/20261110000000_alpha_ask_alpha_project9_budget_cap.sql` through an approved migration mechanism. It is transactional and **requires AI disabled**, verifies the specific original function, changes only the monthly SQL expression, and checks privileges/default OFF. Do not overwrite historical 20261108 migration or reset external spend figures.
- Re-read the installed function signature and exact `>9.000000` expression; verify no `>10.000000` expression remains. Read back AI still OFF and count/usage unchanged.
- Deploy the reviewed Edge source **only after** migration and deployment acceptance; verify `MONTHLY_MAX_USD = 9` in published source and run safe negative hosted tests. Keep `ALPHA_ASK_ALPHA_ENABLED` off. A live positive test requires separately approved, bounded criteria.
- A tested nonproduction rehearsal is in `scripts/agm-ask-alpha-project9-release-rehearsal.sql`. It is run on a *disposable* PG17 fixture only: refuse migration while AI enabled, disable synthetic governor, reapply idempotently, preserve receipts/meter, and prove no reservation while disabled. Do **not** point that fixture script at Supabase.

## Emergency fail-closed handling

1. **Leave or set the Ask Alpha AI enable flag OFF** and suspend public AI mode. This must be performed only by an authorized operator in the intended environment.
2. Inspect the exact error and retain existing usage/UNKNOWN liabilities and receipts. Do **not** roll the financial cap back up to $10 to make requests work.
3. If necessary, redeploy a reviewed known-good Edge function revision under the same OFF flag after rollback verification, maintaining public inquiry/CRM consent behavior.
4. For migration errors, rely on transaction rollback and investigate; do not repeatedly execute a partially understood SQL function replacement. Restore only from an independently verified and authorized recovery point if required.

**Release state: BLOCKED** until backup/isolation, provider key/project binding, ingress integrity, metering, privacy/support, and bounded hosted acceptance are evidenced. The factory STOP remains engaged and no production write was authorized by this document.
