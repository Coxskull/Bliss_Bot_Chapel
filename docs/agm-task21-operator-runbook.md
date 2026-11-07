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

## Private operator dashboard source and deployment boundary
- The functional operator UI source is at `frontend/private/support-operator/`. It is *intentionally excluded* from `scripts/build-vercel-portal.mjs` and the deployed `frontend/public` folder. A successful source build is not evidence that an operator login is operational.
- Dashboard code performs an existing Supabase Auth email/password login using the official JavaScript SDK (pinned ESM import in this draft), supports the verified TOTP MFA challenge, lists role-scoped cases, renders history using DOM `textContent`, and allows authorized closed-case lookups by exact UUID.
- `operator-config.example.js` documents the nonsecret configuration shape only. Before authorized private deployment, generate **uncommitted** `operator-config.js` with the *exact* HTTPS private origin, Supabase project URL and a publishable/legacy anon key. The UI rejects service-role or secret keys. Never place customer data or a secret in config files or browser storage.
- The UI defaults to **read-only** (`mutationsEnabled:false`); server-side `ALPHA_SUPPORT_MUTATIONS_ENABLED` must also be explicitly approved to permit audited case status changes. This is not an excuse to release GLOBAL STOP.
- Secure hosting is pending. Its deploy must serve dashboard assets behind an approved private origin and include response headers such as `Content-Security-Policy: ... frame-ancestors 'none'`, `Strict-Transport-Security`, `Cache-Control: no-store`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`. The draft SDK CDN dependency must be bundled/approved under a locked dependency integrity process before release. No additional paid infrastructure is authorized.
- Unit tests: `node scripts/agm-support-operator-ui-smoke.mjs`. A green source test is **not** an authenticated hosted acceptance.

## Offline bundled static artifact (never auto-deployed)
- Browser runtime imports `./operator-sdk.js` from the **same approved private origin**, not jsDelivr. The production service must serve with the restrictive self-only CSP in `REQUIRED_HEADERS.json`, and disallow `unsafe-inline`.
- The isolated GitHub Actions browser job installs *explicit direct versions* `@supabase/supabase-js@2.95.0`, `esbuild@0.25.12`, `playwright@1.56.1` into a temporary CI tool directory, bundles the SDK into the separate `.task21-private-build/` artifact, verifies its SHA256 manifest, then performs two **zero-real-network** browser scenarios. No linked project API is called by the simulated browser. This unsigned artifact is for review, not distribution as a live release; transitive dependency lockfile/integrity and formal supply-chain approval remain required.
- The generated bundle intentionally contains no `operator-config.js` and therefore is not ready to log in. Secret roster, staff identities and operator credentials may never be packed or committed to GitHub artifacts. A private secured host and explicit identity/release approval remain pending.


## First human operator nominated — staging onboarding addendum (2026-11-08)
- The requesting ALPHA operator volunteered to serve as the first human support tester. This is **a nomination**, not evidence of a verified Supabase Auth identity, role roster, on-duty alert delivery or real support coverage.
- See [first-operator staging and isolated acceptance checklist](agm-task21-first-operator-staging-acceptance.md). The connected project names alone do not establish a write-safe isolated Auth/DB target; **do not modify the live Bliss Bot Supabase project or deploy** under this workstream.
- The private page now offers memory-only self-service TOTP enrollment after a **verified email/password session with no existing verified authenticator**. It requires an explicit click, a one-time secret entered in the operator's own authenticator and a verified code; no QR image, browser storage, email, server-side role assignment or outbound customer message is performed by the page. Auth factor lookup failures deny sign-in rather than triggering enrollment.
- The setup secret is cleared on successful verification, signout and failed setup; it must never appear in screenshots, logs, CI artifacts, GitHub issues or the public portal. Actual Auth enrollment is a user-account modification and must occur **only on an independently approved isolated staging target** after the required identity/rollback checks.
- Initial least-privilege intent is a single `GENERAL_SUPPORT` test role. Billing, privacy and security duties require explicit server-roster membership and verified AAL2. The subsequent sandbox case claim may be enabled only after isolated approval; production flags remain unchanged.


## Staging actual deployment and first-operator identity-only handoff (2026-11-08)
- Existing separate development branch `alpha-first-five-staging` (`wdlmcriajgsretgyusmo`) now hosts a **real `alpha-support-operator` Edge Function v1**, with `verify_jwt=true`. Independent list/get readback confirms the deployed handler contains roster checks, two Auth checks, and default-off mutations. [Deployment receipt](agm-task21-staging-api-deployment-20261108.md).
- **No Auth user was assigned a support role**, and the server-side `ALPHA_SUPPORT_OPERATOR_ROSTER` and allowed HTTPS operator origin were not configured. It fails closed; this is not an actual staffed service. The staging public database has no support/intake tables and the private static operator dashboard has not been deployed.
- The *source-only* private browser config now accepts only `https://wdlmcriajgsretgyusmo.supabase.co` with `environment:"staging"`; it rejects the Bliss Bot live Supabase host. The staging `REQUIRED_HEADERS.json` connect-src pins that same staging origin, not the live origin.
- The branch publishable-key inventory confirms a usable existing publishable key; **its value has not been placed in GitHub source or published to chat**. A future approved private host must inject only that client-safe key through its protected deployment configuration, never the secret/service-role key.
- A staff user who successfully signs in to the **approved private staging site** but is not in the server roster sees an **identity-only** page with their **server-verified Auth UUID** and secure signout, not any support cases. The UUID is not a credential or a substitute for administrator authorization. A wrong Auth session, wrong origin or missing MFA never grants access. This user must give the UUID only to the trusted ALPHA administrator for verified least-privilege roster membership.
- Separate synthetic Chromium and source security checks verify the identity-only state; a browser simulation does not establish real human Auth enrollment. Staging Edge deployment and database rollback testing are distinct receipts, not full hosted end-to-end acceptance.
