# AGM-LAUNCH-014 — synthetic Ask Alpha consent-intent and revocation fixture

**Date:** 2026-10-15
**Repository:** `Coxskull/Bliss_Bot_Chapel`
**Scope:** Non-production engineering demonstration following merged PR #219.

## Implemented

- An isolated `agm_consent_fixture.events` PostgreSQL append-only table, created **only** inside GitHub Actions' disposable `127.0.0.1/financial_test` database. It is not an EF migration and not applied to Supabase, Railway or any production database.
- One synthetic visitor-scoped `OPT_IN` intent followed by an irreversible `REVOKE` for the same source, session, quote and planned handoff. Concurrent exact retries reuse one event. Stale versions, changed retry payloads, foreign scope, mismatched transcript fingerprints and re-opt-in after revocation fail closed.
- Each event includes a SHA-256 digest of the **caller-supplied transcript snapshot**, turn count, explicit purpose, source slug, five independent UUID scope fields, synthetic actor subject, previous-event hash, and hash of canonical event fields. No raw conversation text or personal contact data is recorded.
- PostgreSQL triggers reject UPDATE, DELETE and TRUNCATE on the audit table. The ledger additionally verifies the hash chain and sequential version history on every read/append. The table is a test fixture, **not immutable against PostgreSQL superusers or privileged migration changes**.

## Critical identity/consent boundary

Historical Ask Alpha chats currently use mutable demonstration records and lack a reliably authenticated visitor identity. An authenticated advertiser or ALPHA operator **cannot consent on behalf of a historical visitor**.

This increment therefore **does not expose any HTTP route, browser opt-in control, DI service, transcript retrieval or real visitor consent workflow**. Its ledger constructor rejects any connection other than `Host=127.0.0.1;Database=financial_test`; the policy accepts only an explicit `fixture-visitor-*` actor flagged authenticated, visitor-verified and synthetic by a trusted *test caller*. Those booleans are fixture assertions, **not a real verification system**. Do not wire them to untrusted request inputs or production.

The state is `OPT_IN_RECORDED_UNVERIFIED` or `REVOKED`; neither state makes an audience eligible for contact. For every state, `IdentityIndependentlyVerified=false`, `TranscriptLinked=false`, `TranscriptExportAuthorized=false`, `PaymentAuthorized=false`, `CreatorPayoutAuthorized=false`, `CampaignDeliveryAuthorized=false` and `Delivery=NOT_SENT`.

Because only the transcript **digest** is recorded, not content, this is a fingerprint commitment to a synthetic snapshot, not recoverable immutable transcript storage, independent legal consent, or evidence of an approved campaign creative.

## Acceptance verification

- Pure unit tests enforce exact bounded subject/scope/digest and reject foreign caller impersonation, malformed slugs and tampered event chains.
- Isolated PostgreSQL tests require actual transaction serialization, eight parallel same-key opt-in/revoke retries, persistent replay, stale/changed intent rejection, cross-scope refusal and UPDATE/DELETE/TRUNCATE trigger denial.
- The existing financial CI workflow installs the fixture SQL only in disposable `financial_test`, and runs the new `Bliss.Tests.Financials` family alongside existing financial/gateway tests.
- The existing .NET 8 Release, Docker, Wedding Planner concurrency and three-system browser workflows must also pass on the exact new PR head before promotion.

## Remaining owner/security prerequisites

Design independent visitor sign-in / session ownership, a consent UI displaying purpose/retention/revocation, consent text versioning, secure authenticatable receipt (not caller-supplied flags), retention/deletion/privacy policy and subject-access rules, immutable or content-addressed transcript capture under explicit consent, revocation propagation to downstream assets, independent rights/creative QA, authorized USD quote or approved FX policy, collecting merchant/payee decisions, Stripe test-mode acceptance, and no-send authorization controls.

**No hosted schema change, payment, creator payout, external contact, or campaign publication.**
