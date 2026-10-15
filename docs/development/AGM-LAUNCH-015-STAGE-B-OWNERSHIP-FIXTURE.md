# AGM-LAUNCH-015 Stage B - synthetic session-at-creation ownership receipts

**Date:** 2026-10-15
**Tracker:** AGM-COMM-015-S02. Parent AGM-COMM-015 stays open.
**Scope:** Repository, disposable CI fixture only.

## Purpose
Extend Stage A's fail-closed, issuer-scoped Ask Alpha visitor **claim** check with an independently read, cryptographically signed, **synthetic** session-at-creation commitment. Stage A matching claims do not by themselves establish a visitor's ownership of historical chat turns.

## Implementation
- `agm_ownership_fixture.receipts` and `revocations` are installed only in the GitHub Actions disposable, loopback `127.0.0.1/financial_test` database.
- Receipt binds five claims of scope and time: workspace UUID, session UUID, source slug, HTTPS issuer, pseudonymous digest of a fixture visitor subject, issue/expiry window, and receipt UUID. HMAC-SHA256 authenticates the complete canonical commitment using an isolated synthetic test key; verification uses constant-time comparison.
- A one-hour expiry, exact subject/session/issuer/source/workspace equality, unique session enrollment, and append-only revocations are required. PostgreSQL triggers reject UPDATE/DELETE/TRUNCATE of the receipt and revocation ledgers.
- `AskAlphaVisitorOwnershipPreflight` composes the Stage A claim-gate reading with a separately retrieved synthetic receipt. A missing, expired, wrong-key, mismatched, forged or revoked fixture receipt is refused.
- Even matching evidence explicitly reports `SYNTHETIC_AT_CREATION_RECEIPT_MATCHES_HISTORY_UNVERIFIED`, with `HistoricalSessionOwnershipVerified=false`, `VisitorConsentRecorded=false`, `TranscriptExportAuthorized=false`, `PaymentAuthorized=false`, `CampaignDeliveryAuthorized=false`, and `Delivery=NOT_SENT`.

## Verification
Unit tests cover missing or tampered record, cross-session, foreign workspace/source/issuer, claim subject mismatch, expiration and revocation, invalid fixture credentials and denial of financial or delivery authority. Isolated PostgreSQL verifies persisted HMAC receipt, unique enrollment, subject-scoped revocation, readback, immutable trigger refusal and negative downstream checks. Require all four exact-head workflows: .NET 8 and Docker, isolated financial/gateway PostgreSQL, Wedding Planner PostgreSQL, and three-system browser.

## Scope limitations and blockers
**THIS IS NOT A PRODUCTION AUTHENTICATION SYSTEM.**
- The test fixture has no public routes, no user consent UI, no hosted DI registration, no live OIDC middleware enablement, and no production schema migration. Only a test caller can issue synthetic evidence.
- A fixture caller's assertion that an enrollment happened at conversation creation is not independent proof of historic Ask Alpha authorship.
- The SHA-256 subject digest should not be treated as irreversible anonymization; real privacy and retention policies require independent review.
- Nothing here permits attaching or exposing historical transcripts; buyer/creator rights, USD/FX authority, payment acceptance, payout and advertising send gates are unchanged.
- Parent AGM-COMM-015 remains **NOT DONE** until owner-approved identity provider, verified new-session ownership registration path, purpose-bound consent text/UX, authenticated opt-in and withdrawal, revocation propagation and privacy sign-off.

## Merge and owner controls
Submit as a reviewable PR only after exact-head CI passes. Any merge is a repository engineering milestone, not Executive acceptance, hosted deployment, financial approval or commercial launch.
