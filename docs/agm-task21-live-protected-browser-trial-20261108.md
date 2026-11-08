# ALPHA GLOBAL MEDIA LLC — Task #21 approved protected browser trial

**Date:** 2026-11-08 (Asia/Manila). **Scope:** existing staging-only Supabase branch `alpha-first-five-staging` (`wdlmcriajgsretgyusmo`), existing SSO-protected Vercel `bliss-bot` preview, no new spend, no production changes, Global STOP engaged.

## Operator approval and verification distinction

The operator explicitly stated in the current engineering conversation: **"approved it i already tested it"**. This authorizes proceeding with the bounded staging-only **single fictional case**; it is *not* evidence that the current, newly deployed browser POST already succeeded. A fresh independent Supabase readback showed trial case `619fe5d5-fd4f-4fee-9e2d-c8baee50cc79` still **OPEN, owner null, one CREATED event**, and the trial was **disabled** before activation. Previously verified read-only operator OPEN/CLOSED case detail tests remain accepted.

## Protected exact-source preview — READY

- Vercel team `team_TmcvMnL7h33LpCqjfGbQdRoZ`, existing project `bliss-bot` `prj_bWOvwDWg3zDXoduS1utRwKnYautF`, SSO protection `enabled=true`, `deploymentType=all_except_custom_domains`.
- **Deployment:** `dpl_8RDisLoQBozeKhvMuD9HeWv3xynp`, **READY**, target Preview, source draft PR #353 exact commit `97ae62aa9a2cb0ab9af3d31f6f7481c898d8225a`, no production alias.
- **New protected staff URL:** https://bliss-5kd2bb6bu-alpha-auto-s-projects.vercel.app/staff/task21/
- Build events verified exact GitHub commit clone, `vercel build`, `TASK21_VERCEL_PROTECTED_STAGING_PREVIEW_BUILT` and deployment completion. Exact commit has **11/11 GitHub workflow successes**, including focused Task21 run #42438597875.
- Vercel's earlier free API deployment quota no longer prevented this **one no-cost Preview deployment**. No upgrade or bypass.

## Private account/origin rotation — PASS

Used the reviewed [exact-origin rotation script](../scripts/task21/prepare-protected-preview-origin.template.sql), replacing its placeholder with the just-verified Preview hostname and performing full staging drift checks. DB receipt: `TASK21_PROTECTED_PREVIEW_ORIGIN_ROTATED_TRIAL_DISABLED`.

Independent `service_role` DB readback: exactly one enabled `GENERAL_SUPPORT` staff grant on **new** protected origin, **zero** staff grants on old Preview hostname; the one synthetic trial was at that new origin but still **enabled=false**. The old Preview origin is now deliberately denied. No production user or other support role changed.

## 90-minute EXACT fictional trial — ACTIVE at this checkpoint

After verifying latest PR, full CI, protected READY host, RLS, safe private role and target case, the user-approved [90-minute one-case arming script](../scripts/task21/arm-exact-fictional-case.template.sql) was executed against the staging DB **once**. Receipt: `TASK21_ONE_FICTIONAL_CASE_TRIAL_ARMED_90_MINUTES`.

Independent restricted RPC readback at **2026-11-08 10:41:23 Asia/Manila**:
- **One** enabled temporary trial, exact fictional case `619fe5d5-fd4f-4fee-9e2d-c8baee50cc79`.
- The real verified operator is authorized only by their existing non-exposed staff roster; their account UUID, credentials, session tokens and TOTP secrets are deliberately omitted here.
- New HTTPS preview Origin: allowed.
- **Original OPEN general case:** denied. **Previously CLOSED case:** denied. **Critical SECURITY_REVIEW case:** denied. **Old protected Preview:** denied.
- Trial case still OPEN, no owner, one CREATED event; **no human browser CLAIM/POST verified yet**.
- **Hard expiry:** `2026-11-08 12:10:56 Asia/Manila`. The case-specific privileged SQL RPC checks the expiry on every POST and the installed migration #57 immediately denies further mutations once the case closes, regardless of unexpired timer.
- The original global `ALPHA_SUPPORT_MUTATIONS_ENABLED` remains **OFF**. JWT validation remains required on staging Edge `alpha-support-operator` v3.

## Next browser evidence to accept

User opens the **new URL**, authenticates, selects exact case `619fe5d5-fd4f-4fee-9e2d-c8baee50cc79`, then uses the **case-specific** CLAIM button, followed by audited `IN_REVIEW`, `RESOLVED` with fictional summary, and `CLOSED`, all before expiry. After the user reports completion, independently read back status, verified owner, immutable events, synthetic CRM `BLOCKED`, unrelated cases unchanged, critical alert `UNROUTED`, and last-post denial; run the [idempotent case revocation script](../scripts/task21/revoke-exact-fictional-trial.sql) to leave zero enabled trials. **Do not declare browser test passed until that evidence exists.**

No real customer contact, refund, campaign operation, production deployment, source PR merge or Global STOP release. Separate Task21 remaining gates: app-originated incident alert delivery, staffed primary/backup coverage, approved customer support/refund/privacy/security/dispute/failed-campaign policies, and consent/opt-out sandbox proof.

**Formal Task21 status:** IN PROGRESS, draft PR #353 unmerged.
