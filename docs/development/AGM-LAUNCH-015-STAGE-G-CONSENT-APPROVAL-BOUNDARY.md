# AGM-LAUNCH-015 Stage G — consent event schema and owner engineering approval

**Date:** 2026-10-15
**Tracker:** AGM-COMM-015-S07, parent AGM-COMM-015.
**Owner directive:** Owner explicitly approves **engineering implementation** of the visitor-consent workstream. This approval is not, and cannot substitute for, an independent visitor's purpose-specific informed consent.

## Implementation
- A **test-only** `agm_ownership_fixture.consent_events` append-only PostgreSQL schema. CI installs it into disposable loopback `127.0.0.1/financial_test` **after** the existing Stage E synthetic enrollment fixture; it is not an EF or production migration.
- Each synthetic event is scoped to an existing new-session fixture, workspace, HTTPS issuer and pseudonymous subject digest; fixed purpose `CREATIVE_PREVIEW_ONLY` and version `AGM-CONSENT-FIXTURE-V1`. These identifiers are only test fixtures, **not owner-approved legal consent copy**.
- PostgreSQL trigger checks recorded scope against the synthetic session and serializes transitions under a per-session transaction lock. It requires OPT_IN after session creation, REVOKE only following opt-in, and rejects re-opt-in, duplicate actions, reordering, cross-workspace/issuer/subject changes and unsupported purposes/version.
- UPDATE/DELETE/TRUNCATE are rejected. Database superusers can still change test data, so it is not a full tamper-proof archival service.
- `AskAlphaConsentBoundaryEvaluator` explicitly distinguishes `OwnerEngineeringApprovalRecorded=true` from `RealVisitorIdentityVerified=false` and `RealVisitorConsentAccepted=false`, even if a synthetic opt-in exists.
- Security/financial authority flags remain denied in all states: no transcript export, marketing outreach, payment, payout or campaign delivery; `Delivery=NOT_SENT`.

## Parent workstream status
`AGM-COMM-015`: **Owner approval to proceed with implementation: RECEIVED**. **Actual authenticated visitor consent and launch acceptance: NOT DONE.**

Owner approval authorizes writing and testing secure software, designing opt-in/withdrawal experiences, and staging *only* after separate database and OIDC reviews. It does not authorize using any prior Ask Alpha messages, logging in as a visitor, exposing personal transcripts, waiving privacy duties, contacting creators or advertisers, processing real payments or publishing ads.

## Required visitor-facing consent experience (design, not deployed)
- Separate voluntary, unchecked opt-in following independent authenticated visitor sign-in, with clear purpose and explanation of specific content shared.
- Versioned text, time, actor identity, subject and session binding from **server-authoritative sources**, withdrawal capability, retention and contact/export controls.
- The preview-only purpose must not silently permit transcript reuse, marketing messages, customer profiling, external distribution or campaign sending.
- Revocation must suppress future sharing and propagate to derived content, subject to approved retention/legal policy; opt-in does not waive security/rights/finance/creative release gates.

## Remaining blockers for the REAL parent task
1. Owner/security choose and verify real visitor OIDC issuer/client; tests with signed tokens, cookies, nonce, audience and replay protections.
2. Approve the **real** PostgreSQL schema, privacy/retention and tenant constraints; test migration/up/down in isolated staging before production.
3. Server-initiated, atomic new Wedding Planner visitor session + immutable ownership, no retroactive adoption of advertiser or staff sessions.
4. Real opt-in and revocation UI with signed/audited action from that verified visitor; legal review of purpose and retention notice.
5. Consent-controlled content attachment, transcript access/retention/delete, privacy reviews, then commercial financial/rights/quality acceptance.

No hosted changes or merge/deploy authority is implied. PR acceptance requires exact-head Release, financial PostgreSQL, planner PostgreSQL, Docker and browser CI.