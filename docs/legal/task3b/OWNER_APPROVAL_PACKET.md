# Task #3B — Owner legal approval packet (review-ready, not approved)

**Directive:** ALPHA-TASK3B-LEGAL-REVIEW-20261113 / ALPHA-A2A-EXECUTION/4.0.
**Prepared:** 2026-11-14. **Source:** Coxskull/Bliss_Bot_Chapel PR349 exact commit cdc8852c1079316e7edb00f541e32218321d2ed0.
**Scope:** Draft legal readiness ONLY. PR349 unmerged; global STOP engaged; live registration NOT AUTHORIZED.

## Documents for owner examination
1. ADVERTISER_REGISTRATION_PRIVACY_NOTICE.md — proposed dedicated account notice, covering exactly the observed auth/application fields, separate inquiry vs account, conditional providers, privacy rights and retention placeholders.
2. ADVERTISER_REGISTRATION_TERMS.md — proposed application-only obligations, accuracy, pending-review, no fee/order, IP/content/security/suspension concepts; material legal remedies reserved for counsel.
3. CONSENT_DISCLOSURE_MATRIX.md — field-by-field source evidence, destinations and policy unknowns.
4. LEGAL_REVIEW_FINDINGS.md — coded classifications PASS/REQUIRES_REVISION/REQUIRES_OWNER_DECISION/REQUIRES_LEGAL_COUNSEL/BLOCKED.
5. CHANGE_MANIFEST.md — exact before/after and safe release boundary.

## Important proposed provisions
- Application and public inquiry are different; only pending review is created, never automatic workspace access.
- Email/password reach Supabase Auth directly from browser; business fields and receipt enter the registration API/database only if independently deployed/approved.
- Marketing is **separate, optional, unchecked** and does not automatically authorize CRM outreach.
- Registration itself creates **no payment or refund promise**; future paid services need separately approved orders.
- Notice and terms cannot become effective until dated, reviewed, version-bound and published with verified technical controls.

## Owner decisions requested — NOT presumed approved
| Decision ID | Owner to determine | Why it matters |
|---|---|---|
| O01 | Company legal mailing/contact address and accountable privacy/support contact/channel | Required accurate published identification and requests |
| O02 | Retention and deletion policy for Supabase identities, applicant profile, receipt, logs, backups and third-party copies | Append-only receipt and legal rights must align |
| O03 | Jurisdictions/eligible applicants, representative authority and any minimum age | Do not claim identity/business authority is verified when code only verifies email/subject |
| O04 | Which processors/transfers are actually used and approved, including Supabase, Railway, Vercel and any future HighLevel | Accurate recipients, regions, contractual safeguards |
| O05 | Manual advertiser review, rejection/appeal, suspension and account closure, customer complaints and withdrawal routing | Terms must match a real supported workflow |
| O06 | Whether registration needs a separate legally-approved feature gate beyond default-OFF engineering flag; who may release it | Owner approval must fail closed |
| O07 | Any future campaign/order, fee/tax, IP, refund/cancellation and support obligations (distinct from registration) | Avoid binding unapproved commercial terms |
| O08 | Final notice and terms effective date, published version IDs and rollout/re-consent policy | UI/API/receipts must reflect exactly what was approved |

## Independent qualified legal review
Counsel should evaluate applicable privacy frameworks and lawful bases; cross-border transfer requirements and processor agreements; notice/consent vs contract design and withdrawal; age/representative eligibility; retention exceptions, erasure vs immutable audit records; fairness/enforceability of any IP license, suspension, disclaimer, liability, indemnity, dispute resolution, governing law, waiver and change clauses. No assumption of global enforceability or ready-to-publish legal text.

## Engineering continuation kept independent
- PR349 at cdc8852: previously accepted 9/9 source checks; account-specific v1 drafts wired, optional unchecked marketing and append-only receipt in source. No hosted 201/200/409 demonstration.
- PR421 at a766937: draft, source-only classifier with 9/9 offline synthetic fixtures; no verified effective live app DB identity.
- Issue401 records that the existing Railway app's historic safe diagnostic was connected/TLS PASS but table count 0/migration history absent; the separate Supabase Bliss project had 29 public tables / seven EF migrations but lacks the proposed membership and receipt tables. **Do not assume those are the same target.**
- Next independent work: classify live app target privately; confirm correct off-host encrypted backup and isolated PG18 restoration; reviewed migration order and OIDC/tenant test host under zero new spend. No host deployment or production writes authorized.

## Approval record (intentionally unsigned)
Privacy notice: PENDING OWNER REVIEW.
Terms: PENDING OWNER REVIEW.
Retention policy: PENDING.
Privacy-rights request path: PENDING TECHNICAL VERIFICATION.
Independent counsel: REQUIRED.
Version binding + hosted regression: BLOCKED.
Global STOP: ENGAGED.
Production publication / payment / outreach / live registration: NOT AUTHORIZED.

**Owner may approve only after editing the open decisions and independent review. No checkbox or automated CI result substitutes for signed approval.**
