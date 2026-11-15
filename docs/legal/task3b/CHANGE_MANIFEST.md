# Task #3B — change manifest and acceptance boundaries

**Directive:** ALPHA-P0-TASK3B-FINAL-RESOLUTION-20261114
**Updated:** 2026-11-15
**Repository:** Coxskull/Bliss_Bot_Chapel
**Documentation PR:** #423, branch `review/task3b-legal-readiness-20261114`, stacked on the original PR #349 source baseline.
**Source engineering PR:** #349, branch `codex/task3-secure-advertiser-registration-20261107`.
**Private DB identification PR:** #421 (read-only owner-operated classifier; not deployed).
**Status:** non-production source candidates, not published/effective; Global STOP and registration activation remain held.

## Actual reviewed changes

| File | Change / evidence | Acceptance |
|---|---|---|
| `docs/legal/task3b/ADVERTISER_REGISTRATION_PRIVACY_NOTICE.md` | Reconciles known source data categories and separate application/inquiry purposes; documents provider uncertainty, per-purpose lawful basis, retention class criteria, rights-request process, missing verified account case route | Draft content only; not published |
| `docs/legal/task3b/ADVERTISER_REGISTRATION_TERMS.md` | Registration-only no-fee terms, applicant representations, PENDING_REVIEW, restricted business-data permission, required rights, no unsupported universal law/arbitration claims | Draft content only; not binding |
| `docs/legal/task3b/CONSENT_DISCLOSURE_MATRIX.md` | Source-field inventory, v1 UI/API parity, explicit separate optional unchecked marketing; v2 candidate not deployed; consent withdrawal unverified | Source evidence only |
| `docs/legal/task3b/INTERNATIONAL_APPLICABILITY_CHECKLIST.md` | **New** country-conditional statutes for PH/MY/ID/CO/PA/DO/TH, EU GDPR and NJ where triggered; official references; required factual tests | Not universal legal certification |
| `docs/legal/task3b/LEGAL_REVIEW_FINDINGS.md` | Reclassifies generic counsel-only blockers into concrete jurisdictional checks and identifies narrowed source fixed/hosted open rights-route defect | Engineering finding only |
| `docs/legal/task3b/OWNER_APPROVAL_PACKET.md` | Records settled international strategy and refusal of paid outside counsel; lists specific factual publication and operational requirements | No assumed signature/launch approval |
| `docs/legal/task3b/ACCOUNT_PRIVACY_CASE_OPERATOR_RUNBOOK.md` | **New** single-human operator case handling: subject verification, jurisdiction classification, privacy rights categories, lawful exceptions, evidence/ACK procedure, no false response/suppression promises | Operational source candidate, not hosted |
| `docs/legal/task3b/CHANGE_MANIFEST.md` | This reconciled change and evidence index | Source only |

**PR #423 contains seven task3b Markdown documents.** Its changes are to documentation, not a new auth, database, or registration architecture. Six original docs were present before this continuation; the international applicability checklist is the seventh.

## Separate already existing registration implementation and scoped repair

PR #349 source contains versioned `advertiser-privacy-2026-11-07-v1` and `advertiser-terms-2026-11-07-v1` browser and API constants, independent required processing/terms choices, unchecked optional marketing, and append-only registration receipts. These are **source features**, not evidence of a working hosted registration.

- [8c1de954](https://github.com/Coxskull/Bliss_Bot_Chapel/commit/8c1de9549bc7f06fbed530e36a88def9b605346e) — Corrects misleading account-privacy draft hyperlink pointing to inquiry-only `/privacy-request.html`.
- [ef7fae3e](https://github.com/Coxskull/Bliss_Bot_Chapel/commit/ef7fae3e265b7b34e288ead4189ebf855a6c8bdc) — Regression test to prevent that false account-privacy routing assertion.
- Exact-head PR #349 GitHub CI: five retrieved workflow runs completed SUCCESS, including .NET restore/build/regression and account notice source contract, initial disposable PG migrations and synthetic browser/PG regressions. [CI evidence](https://github.com/Coxskull/Bliss_Bot_Chapel/actions/runs/43671795460).
- This source repair **does not** implement verified account-specific privacy intake, erasure/withdrawal or hosted acceptance.

## Decision and hold reconciliation

The owner settled global/international commercial orientation and declined paid outside counsel. Generic external counsel approval is **not an independent Task #3B stage**. Specific privacy statutes, required disclosures, consent/rights and security controls still apply under the actual deployment/target markets.

**Version gate:** candidate review-v2 IDs in PR #423 have not been bound to PR #349 source v1 frontend/API, database receipt or a published effective notice. Do NOT claim v2 acceptance.

**Hosted gate:** existing `bliss-bot` Railway project has production environment only. Bliss Supabase `bkutbglzivfdnoerigyb` reports no development branches and read-only table inventory lacks proposed registration receipt/membership tables; this does **not** classify the actual Railway application's effective database. Protected isolated Auth/API/PostgreSQL, recoverable backup and hosted 201/200/409 + tenant-denial tests are not accepted.

**Operational claims not authorized:** No new registration customers or payments, outreach, production DB writes, unrestricted host access, new software purchases, deployed migrations, Global STOP changes or published legal documents.

**Completion accounting:** One precisely scoped false privacy routing claim was removed and protected by successful source regression CI; the full Task #3B and documentation publication remain OPEN. Record this as `PARTIAL_WITH_EVIDENCE`, not `COMPLETE_VERIFIED`.

## Follow-on source correctness and hosting inventory (PR #424)

- [PR #424](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/424) is an unmerged non-production draft stacked on the existing PR #349 implementation. It adds minimal authenticated subject/tenant-bound account privacy-case intake, case receipts, PostgreSQL RLS and revoked client grants, and a default-closed human operator queue/ACK. A request does **not** fulfill erasure, optional marketing withdrawal or account closure.
- Verified follow-on source failures fixed: approved advertiser middleware previously denied the new own-case endpoint; it now permits only GET/POST on that exact route with separate `Authentication:AdvertiserPrivacyRequestsRequested` switch, OFF by default. Both browser POST scripts previously sent a header that did not match the server's `X-CSRF-TOKEN`; both now use that configured header and a regression enforces parity.
- Existing Bliss Railway lists only production, and Bliss Supabase has zero development branches. Unrelated Factory/other Alpha Railway projects and DR362 recovery must not be used as advertiser staging. Effective Railway app PostgreSQL identity and standalone recoverable backup remain unverified.
- Latest PR #424 commit `670d3fed37501e4bb53ffdf640a78ab6956aed22` has **five exact-head SUCCESS GitHub workflows**, including [.NET #43674945815](https://github.com/Coxskull/Bliss_Bot_Chapel/actions/runs/43674945815) with 1,298 tests passed, 63 skipped and zero failures; PostgreSQL 16/18 migration + synthetic restore #43674945768, browser #43674945784, finance #43674945771 and concurrency #43674945841 also passed. These are non-production source/synthetic CI receipts, not hosted registration or privacy-case fulfillment.
- PR #423's legal v2 remains a proposal: there is no published, effective date or matching browser/API/receipt version binding. No new outside-counsel payment gate and no live launch.

**Tracking:** [Issue #401](https://github.com/Coxskull/Bliss_Bot_Chapel/issues/401), [PR #349](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/349), [PR #423](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/423), [PR #421](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/421), [Alpha Daily Operations](https://docs.google.com/spreadsheets/d/12ZLd1_L8mnvMO6fky9jkd-CVY8HX5XscuT56Yql_if4/edit).
