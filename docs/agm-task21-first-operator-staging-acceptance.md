# ALPHA Global Media — Task #21 first human operator onboarding (STAGING ONLY)

**Date:** 2026-11-08  
**Nominated first human operator:** ALPHA's requesting operator (self-nominated in chat; **identity not yet verified against Supabase Auth**).  
**Status:** PREPARED — no deployed operator login, verified roster entry, private staging site, or human notification acknowledgement yet.  
**No-spend directive:** $0 incremental, existing resources only.  
**Change-control:** Global STOP engaged; production mutations, customer outreach, GHL sends, refunds, campaign changes and unapproved staging infrastructure are prohibited.

## 1. Hard gate: select a genuinely isolated Auth + database target

The connected Supabase inventory currently shows projects named `Alpha`, `Alpha Lead Generation`, and `Bliss Bot`. A name alone **does not prove** a project is an approved disposable staging target. Existing project `bkutbglzivfdnoerigyb` holds the current Bliss Bot live intake/function footprint and **must not** be used for Task #21 operator creation, enrollment, role-roster edits, migration, or synthetic case writes under this directive. An in-memory mocked browser plus disposable CI PostgreSQL is already available and is the only currently verified write-safe acceptance target.

Before any hosted sign-in test, obtain separate written evidence that the existing resource is isolated, has synthetic-only data, has a tested rollback point, and is eligible for Auth enrollment. The current dashboard's `safeOperatorConfig` pins the existing Bliss Bot URL; it **must be pointed only at a separately verified approved isolated target** in a reviewed later change, not overridden ad hoc in a live config.

Do not deploy `alpha-support-operator`, run the migration against a connected Supabase project, open public support traffic, or flip the mutations flag until this gate is met. No new account purchases or subscriptions.

## 2. Provisional operator duties

The requesting operator is the proposed primary **general-inquiry** responder during a bounded fictional test. That nomination is not an authentication record or legal assignment of live support coverage. Sensitive queues (billing/refund/failed campaign/privacy/security) remain excluded unless a real approved Auth user UUID is added to the server-side staging roster for the matching role, and TOTP AAL2 is verified. One person can test multiple queues in staging but is not evidence of 24/7 launch staffing.

The operator must approve a monitored *existing* support mailbox/channel and nominate an alternate escalation owner for periods when unavailable. A case dashboard or assigned UUID alone is **not** an actual email/SMS/page delivery or a human response-time promise.

## 3. Account and authenticator onboarding — only after isolated target approval

1. From the approved isolated Supabase Auth tenant, locate or provision **your own** account under an approved operator email. Never provide password, MFA setup secret, six-digit TOTP, access token, service-role key, or recovery codes to a chatbot, repo, screenshot, or support case. Only a trusted Auth administrator may verify the actual Auth user ID; do not guess one from a name or email.
2. Confirm the private HTTPS operator origin, security response headers, `no-store` and `noindex`, firewall/private access where applicable, and an operator config with **only a public publishable key**. The private bundle is excluded from the public Vercel build. `ALPHA_SUPPORT_MUTATIONS_ENABLED` must remain **false**.
3. Sign in on that approved private page via email/password. If no verified TOTP factor exists, choose **Set up authenticator**, manually enter the temporary secret in an authenticator app, and verify the current code. If you already enrolled TOTP, use the existing MFA verification screen. The setup secret exists only in DOM text/memory for the active onboarding session, is cleared after successful verification or signout, and is never included in the unsigned CI artifact. This step does not grant operator permissions by itself.
4. A trusted administrator must place the **actual verified Auth UUID** in the **server-side only** `ALPHA_SUPPORT_OPERATOR_ROSTER` JSON for the approved isolated test role(s); do not put this roster in browser config or repository source. An unrostered login must be denied. Initial proposed least-privilege role is `GENERAL_SUPPORT`; escalate roles separately only for the test and only after verified `aal2`.
5. Capture an evidence note with only the date, test run ID, which queue(s) were authorized, whether sign-in, MFA, denial and signout passed, and redacted case references. Never capture TOTP secrets, login credentials or customer records.

## 4. Current isolated acceptance already performed

- Source, JWT/role-scoped operator contracts, default deny, browser storage/DOM XSS restrictions, the service-only PostgreSQL RPC and denial of second operator takeover.
- Eight fictional support categories persisted with receipt/event/outbox checks in disposable PostgreSQL 17; sensitive support excludes general CRM delivery.
- Synthetic Chromium runs with all outbound calls intercepted: password sign-in, MFA challenge, role gating, immutable case history, a single human claim, blocked overwrite, closed-case readback and logout. New enrollment flow must additionally be verified on **the exact PR head**. A browser mock does not enroll a real staff account.

These tests **do not prove** human availability, mail delivery, Supabase-hosted migration, real Auth identity or live GHL acceptance.

## 5. Hosted synthetic acceptance sequence (NOT AUTHORIZED UNTIL GATES ABOVE PASS)

1. Verify written isolation/rollback/ownership and verify private origin. Confirm operator account and server roster, `aal2` for sensitive roles, no public table/data exposure.
2. Apply additive SQL to the isolated database only; inspect migration readback and security advisors; keep CRM sends and real delivery disabled.
3. POST one **fictional** general inquiry; verify unique request receipt, new support case, CREATED event and general-queue-only readback.
4. With mutations still OFF, verify all POST claims/updates return LOCKED. Deny unauthenticated, wrong-role, unrostered, missing AAL2, forged UUID, and invalid Origin requests.
5. Only in that isolated synthetic environment, explicitly approve a bounded flip of `ALPHA_SUPPORT_MUTATIONS_ENABLED=true`; claim the case using verified staff login, require exactly one ASSIGNED event and repeat-idempotent readback. Competing claimant must fail.
6. Record TRIAGED → IN_REVIEW → RESOLVED → CLOSED with a fictional non-sensitive resolution. Verify actor IDs, event ordering, exact case history and retention/rollback. Do **not** send a real refund, email or campaign modification.
7. Trigger one fictional security event, verify its CRITICAL queue is accessible only at AAL2 and that a separately approved *test* alert channel reaches a live human who acknowledges the message. Record time-stamped human ACK without secret content.
8. Switch mutations flag OFF, revoke isolated roles/fixtures as approved, validate rollback, and record evidence that no production changes or external provider sends occurred.

**Required final outcome:** source-only acceptance PASS is distinct from hosted staff PASS. Task #21 remains **IN PROGRESS** until the actual isolated target, staffing/alert acknowledgements, refund/dispute/privacy policy sign-offs, sandbox GHL result, and launch owner final decision are recorded.

## 6. Evidence ledger format (no PII)

| Gate | Evidence | Current result |
| --- | --- | --- |
| First operator named | User's self-nomination | NOMINATED (not yet Auth verified) |
| Safe isolated target | Project/rollback owner approval | BLOCKED — not identified |
| First Auth account / TOTP | Operator uses approved isolated host | NOT EXECUTED |
| Server-only role roster / AAL2 | Admin configuration and denied/allowed reads | NOT EXECUTED |
| Offline source, DB, browser | GitHub Actions exact-head results | PREVIOUS PASS; enrollment revision pending |
| Human incident alert acknowledgement | Timestamped fictional alert delivery / ACK | NOT EXECUTED |
| Hosted E2E, GHL sandbox, rollback | Redacted receipts | NOT EXECUTED |
| Production | Explicit owner launch signoff | DENIED; Global STOP engaged |

**Do not mark Task #21 complete on the strength of this checklist or CI alone.**
