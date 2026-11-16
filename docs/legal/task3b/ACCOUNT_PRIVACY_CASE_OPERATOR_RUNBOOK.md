# ALPHA GLOBAL MEDIA LLC — Account Privacy Case Operator Runbook (non-production candidate)

**Directive:** ALPHA-P0-TASK3B-FINAL-RESOLUTION-20261114
**Authority:** owner authorized continuation of Task #3B on 2026-11-15, including non-production technical and legal completion.
**Status:** operational draft, NOT a live support commitment or published account privacy channel. **Owner-designated company privacy contact (2026-11-16):** `alpha7auto@gmail.com`. The existing single human support operator is the intended accountable handler. Verify mailbox access, actual delivery, private case creation, owner/operator acknowledgment, identity verification and response evidence before describing this address as an operationally accepted account privacy queue. Never claim an action or deadline has been met without its receipt.

## 1. Activation boundary

Source implementation is [registration PR #349](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/349), [privacy intake PR #424](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/424), [legal PR #423](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/423).

- `Authentication:AdvertiserPrivacyRequestsRequested` is **OFF** until an isolated deployed API/Auth/PostgreSQL environment, a recoverable backup and real OIDC+CSRF test are accepted.
- `Authentication:ApprovedPrivacyOperatorSubjects` is empty by default. Only a provider-verified staff role AND a separately owner-allowlisted subject may view the operator queue or acknowledge. Do not put an email address, name or CRM contact ID in the subject allowlist; use an actual verified opaque identity subject stored securely.
- The account-rights preview page is `noindex`, disabled by default. Neither `/privacy-request.html` nor the public interest form is an account data-subject rights request channel.
- No new customer, marketing, SMS, email, payment or campaign event may be generated as part of rehearsal. Keep global STOP.

## 2. Intake and identity handling

1. Read only the authenticated account cases routed through GET `/api/advertisers/privacy-requests/operator-queue`; view only fields necessary for the case. An accepted POST `/api/advertisers/privacy-requests` records a type, subject, advertiser ID, reference, time and `RECEIVED` status.
2. Acknowledge an eligible case through POST `.../{caseId}/acknowledge`. A successful `VERIFYING` response records the operator subject and timestamp. **It does not** establish that the request was fulfilled, that its jurisdiction was verified, or that a customer was notified.
3. Authenticate the requester through the existing verified account identity. Do not rely on an email typed in a free-text inquiry, CRM lead identity, an unverified URL or a public request key. Do not ask for passwords, full payment credentials or government ID scans in an unapproved intake channel.
4. If additional verification is necessary, use only an owner-approved secure channel; avoid duplicating protected data in GitHub issues, logs, shared workbooks or case comments.
5. Classify the requested action and applicable jurisdiction using actual account/provider/deployment facts and [international applicability checklist](INTERNATIONAL_APPLICABILITY_CHECKLIST.md). Determine legal response timing **per applicable law**; do not invent one uniform world-wide SLA.
6. Keep a private, least-privilege case file with reference, identity verification method, applicable law, deadline, operator, processor/data inventory, approved retention exceptions, action, evidence and final communication. Public GitHub/task entries carry sanitized status only.

## 3. Request-type procedure (do not claim fulfillment until verified)

| Request type | Operator action | Special hold |
|---|---|---|
| ACCESS | Verify identity, locate subject's Auth, applicant/business profile, consent receipt, approved provider-held data, and disclose only the requester-entitled records through a secure channel | No generic export of other advertisers; source has no actual data-export feature |
| CORRECTION | Confirm the precise erroneous field, verify authorized replacement, apply via approved business record process and log before/after hashes rather than copying protected content | Source has no customer correction action yet |
| ERASURE | Identify actual records, active legal holds, audit evidence constraints, provider account/session records, support logs, backups and available deletion methods. Escalate immutable receipts for legally permissible minimization rather than altering audit history casually | Source has no verified deletion, account-session revocation or processor cleanup |
| MARKETING_WITHDRAWAL | Immediately cease *any optional account marketing* through controlled send-side suppression; reconcile any CRM/export/processor contact state and log suppression before responding | An immutable historical consent receipt does **not** prove current permission. Source-only case receipt does not suppress HighLevel or prove sends stopped |
| OBJECTION | Identify processing purpose and jurisdiction-specific lawful basis; restrict/review processing if legally required, document decision and notify through verified channel | No automated objection resolution |
| ACCOUNT_CLOSURE | Confirm authority, revoke access/sessions as allowed, reconcile active commitments and billing under separate terms, perform lawful data minimization and communicate outcome | No live account closure endpoint exists |

## 4. Decision, human acknowledgment and evidence

- Record each request as `RECEIVED` and only mark `VERIFYING` when the allowlisted human actually acknowledges. The current source does not implement a final `RESOLVED` or `DECLINED` API; do not alter database status manually to fabricate closure.
- The human operator must keep a verified action manifest for each subject/provider, with exceptions and security checks. Any required regulatory, incident, or legal escalation is separate from routine outside-counsel procurement.
- An approved conclusion must include the request type, action performed, controller decision, statutory timing, processor acknowledgments (if applicable), lawful retention exceptions, customer communication receipt and responsible human identity. Without this evidence, classify case as OPEN/PARTIAL.
- Marketing withdrawal is not complete merely because a user submitted a case or a staff member acknowledged it. Implement and test a send-side account marketing suppression gate before making an opt-out completion promise.
- Never email identity/record details from a fake OIDC session or use the public marketing CRM to respond without verified consent and role.
- Keep registration and actual paid-service terms separate: a free application is not a $149 order or Stripe payment permission.

## 5. Security acceptance in isolated hosting only

1. Verify target app DB identity and a separately recoverable encrypted backup. Use existing resources only if isolated; do not run migrations in `bliss-bot` Railway production for testing.
2. Verify correct HTTPS, trusted forwarded headers, signed OIDC subject, email-verification state, no role/tenant escalation and anti-forgery header `X-CSRF-TOKEN`.
3. Verify POST/GET privacy cases: 201 accepted, 200 exact replay, 409 different/cross-subject replay, 400 unsupported type/CSRF failure, anonymous 401 and unapproved/pending tenant and operator scope 403.
4. Verify private operator queue default 403, issuer-bound allowlist and staff role; with the approved synthetic staff identity only, queue/list and ACK then evidence of `VERIFYING`/auditor.
5. Confirm no real Auth user, mail delivery, customer payment, CRM sync, deletion or support message generated by tests.
6. Run independent rollback and backup restore acceptance; leave Global STOP engaged until authorized release gates pass.
7. Publish legally accurate final account privacy notice/terms with controller contact, effective date, actual recipients/regions and retention criteria. Bind exactly one version in HTML, JavaScript, server constants and immutable receipt checks and verify deployed links.

**Source receipt is not a hosted or legal acceptance.** Do not turn this file into a public live promise merely because the owner authorized work.
