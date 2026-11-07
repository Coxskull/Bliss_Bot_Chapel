# ALPHA Task 21 — private human operator handoff (not yet active)

**Engineering candidate only.** Requires an approved isolated target and an actual staffed owner/response channel before any hosted acceptance or public release. No one is currently confirmed as an on-duty responder by this source work.

## Operator configuration and non-goals
1. Identify real support operators from an approved existing Supabase Auth tenant; record their real Auth user UUIDs **server-side only**. Never create imaginary users, infer identity from a name/email or commit these UUIDs into the public repository.
2. Provision the server-only secret `ALPHA_SUPPORT_OPERATOR_ROSTER` as a JSON map of role name to an array of permitted Auth user UUIDs, for example the *shape* `{"GENERAL_SUPPORT":["<approved-user-uuid>"]}`. Valid role names: GENERAL_SUPPORT, ADVERTISER_SUPPORT, CREATOR_SUPPORT, BILLING_REVIEW, PRIVACY_REVIEW, SECURITY_REVIEW. Reject missing/invalid rosters. This is an authorization roster, **not** a notification system.
3. Require an actually authenticated, signed-in Supabase Auth user; verify the user JWT live. Sensitive role access additionally requires `aal2`. User-editable metadata has no role authority.
4. Keep `ALPHA_SUPPORT_MUTATIONS_ENABLED` unset/false until Global STOP policy, isolated hosted migration/rollback, escalation staffing and operator authorization have been approved. No new CRM automation or customer messages.
5. Optional browser origin `ALPHA_SUPPORT_OPERATOR_ORIGIN` should be a single approved secure operator frontend HTTPS origin. Without it, browser CORS requests are denied; direct API clients still require a user JWT, role roster and checks.
6. The existing HighLevel contact bridge remains independently controlled by `ALPHA_GHL_SYNC_ENABLED`. General/creator/advertiser *support* CRM also requires `ALPHA_GHL_SUPPORT_SYNC_ENABLED`; the five sensitive categories are never eligible for general CRM delivery.

## Internal API contract (proposed)
Function: `alpha-support-operator`, deployed only with `verify_jwt=true` from `supabase/config.toml`.
- `GET` — returns open queue metadata visible to the verified operator role. Optional integer `page=0..100`, 20 entries per page.
- `GET ?case_id=<uuid>` — detailed existing case + associated receipt source contact + last 100 immutable events; only for authorized staff roles. Not for the public browser.
- `POST` JSON `{"case_id":"<uuid>","status":"TRIAGED","owner_user_id":"<approved-role-user-uuid>","resolution":""}` — updates via service-only `alpha_support_transition` RPC, with database event receipt and required human operator. Must be disabled during protected preview. Status changes are bounded by existing SQL state machine; RESOLVED and CLOSED require a nonempty resolution and valid human owner.
- No public `x-test-mode` header exists. All synthetic admission must happen in an isolated database/test environment, not by altering production traffic.

## Acceptance witness needed
- Validate denial for no JWT, expired/revoked operator session if possible, wrong role, missing `aal2`, wrong Origin, unassigned role, tampered status and missing closure evidence.
- Verify a real on-duty staff member can receive, acknowledge and hand off a fictional incident through an *approved* alert/response channel in a sandbox, with time-stamped receipt. The queue alone is insufficient.
- Verify one isolated support submission, durable request ID/receipt/case/event and human case acceptance; no live CRM and no payment/refund executed. Verify old interest requests remain intact.
- Verify disabled CRM, sensitive-case CRM exclusion, retention cleanup and rollback. Record exact deployed version/commit, redacted IDs, CI URLs, authorized environment and owner sign-off.
