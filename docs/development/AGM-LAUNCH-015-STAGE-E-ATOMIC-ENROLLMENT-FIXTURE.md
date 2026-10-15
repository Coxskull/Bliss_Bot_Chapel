# AGM-LAUNCH-015 Stage E — atomic new synthetic visitor session fixture

**Tracker:** AGM-COMM-015-S05, parent AGM-COMM-015 stays open.
**Date:** 2026-10-15
**Execution mode:** Non-production repository and CI fixture only. No real OIDC or campaign activation.

## Stage E implementation
- `agm_ownership_fixture.new_sessions` is an append-only table created by SQL only in the isolated GitHub Actions `127.0.0.1/financial_test` fixture. No EF migration/hosted schema change; immutable triggers reject UPDATE/DELETE/TRUNCATE.
- `AskAlphaAtomicFixtureEnrollment` generates a fresh **server-side UUID**, signs a synthetic ownership receipt using existing Stage B HMAC-SHA256 mechanism and saves **the receipt and the new session record in one PostgreSQL transaction**.
- The method obtains a scoped advisory transaction lock on synthetic workspace, issuer, hashed subject and idempotency key. Concurrent exact retries return the same session/receipt pair; changed source slug, expired/revoked record or a different intent reusing the key fails closed.
- `AskAlphaAtomicFixtureEnrollmentGate` composes the existing Stage D pure claims prerequisite check before any database call. It takes the synthetic test actor subject from that principal rather than caller-supplied subject input. This is a test fixture principal, **not** a verified live OIDC provider.
- Tests use disposable PostgreSQL, concurrent eight-way same-key requests, the existing Stage B receipt reader/verifier, mismatched retry refusal, different-workspace/source isolation, revocation, append-only SQL triggers, invalid claims and zero financial or delivery authority.

## Limits and safety
**THIS IS NOT REAL VISITOR REGISTRATION.** The new table is NOT the production Wedding Planner session table and is NOT configured for hosted use. The class refuses any PostgreSQL connection other than loopback `financial_test`. Neither the service nor gate is registered in `Program.cs`, and there is no HTTP route, external network call, customer outreach or production deployment.

Nothing here verifies a real customer's identity or historic Ask Alpha authorship. A fixture principal with arbitrary claims does not demonstrate a cryptographically validated provider token. Signing a test receipt is not informed consent.

Every result remains:
- `RealVisitorAuthenticated=false`
- `HistoricalSessionOwnershipVerified=false`
- `VisitorConsentRecorded=false`
- `TranscriptLinked=false`
- `TranscriptExportAuthorized=false`
- `PaymentAuthorized=false`
- `CreatorPayoutAuthorized=false`
- `CampaignDeliveryAuthorized=false`
- `Delivery=NOT_SENT`

## Next mandatory gates
1. Owner/security approval of a real OIDC issuer/client, verified hosted token/cookie validation and visitor-role entitlement.
2. One authoritative application database transaction that creates the **real** visitor-owned session plus ownership grant, with secure idempotency and tenant isolation.
3. Purpose/version-specific informed consent and independent revocation, privacy retention/export and subject-access policies.
4. Sandbox-only commercial integration, USD/FX and merchant/payees approval, creator rights, visual QA, hosted acceptance and Executive launch authorization.

No merge, release or commercial authorization is implied by CI tests. Stage E must pass four exact-head GitHub Actions workflows and independent reviewer acceptance.
