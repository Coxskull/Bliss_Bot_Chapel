# AGM-LAUNCH-015 Stage D — OIDC new-visitor enrollment prerequisites

**Date:** 2026-10-15
**Tracker:** AGM-COMM-015-S04, with parent AGM-COMM-015 still open.
**Status:** Code review and exact-head CI required.
**Scope:** Repository-only, *no* production enrollment, payment or campaign sending.

## What is implemented
Adds a default-false `Authentication:AskAlphaVisitorEnrollmentRequested` configuration key and a pure `AskAlphaVisitorEnrollmentGate.Inspect` policy. Even when that setting is true, it does **not** register services, create sessions, issue identity records or expose any HTTP endpoint. The option enables only a **prerequisite diagnostic**, not authorization.

The policy rejects:
- disabled OIDC authentication, missing client ID, empty or non-HTTPS authority/visitor issuer, incompatible configured issuer vs. authority path, URI with userinfo, query or fragment;
- staff, admin, operator, reviewer, viewer and advertiser roles; role configurations that conflate the visitor role with privileged roles;
- unrecognized subject issuer, duplicate/malformed subjects, unauthenticated principal;
- identities carrying a pre-existing `ask_alpha_session_id` (cannot bootstrap historical ownership), or any advertiser ID binding.

Even for a positive synthetic principal, the reading is **NEW_VISITOR_OIDC_CLAIMS_DECLARED_SERVER_CREATION_PENDING**, `IdpConfigurationDeclared=true`, `IssuerSubjectClaimsConsistent=true`, but `NewVisitorSessionCreated=false`, `RealVisitorIdentityAccepted=false`, `HistoricalSessionOwnershipVerified=false`, `VisitorConsentRecorded=false`, `TranscriptExportAuthorized=false`, `PaymentAuthorized=false`, `CampaignDeliveryAuthorized=false`, `Delivery=NOT_SENT`.

**Trust model:** `ClaimsPrincipal` is a claims container, not proof of identity. In real hosting, the OIDC middleware must verify issuer, token signature, client audience, nonce, correlation and cookie integrity. Standalone synthetic test principals do not demonstrate that. The runtime `Authentication:Authority` and visitor issuer must be deliberately reviewed and aligned by the owner/security team before any future implementation that performs writes. The feature flag is not a launch permit.

## Tests
- Default feature-off check, disabled OIDC, missing client config, foreign/insecure issuer, URI userinfo/query, mismatched OIDC realms.
- Reject forged issuer, missing authentication, malformed/duplicate subject, existing session claim and advertiser scope.
- Exclude staff, reviewer, viewer, advertiser and mixed-role identities.
- Confirm matching *synthetic* claims never create a session, grant consent, export a transcript, authorize a payment or activate a campaign.
- Require all exact-head .NET Release, isolated financial and Wedding Planner PostgreSQL, Docker build and fictional browser CI workflows.

## Remaining mandatory gates
1. Independently approve and provision a real visitor identity provider/registered OIDC client; validate production middleware and cookie/session controls.
2. Add a server-owned, atomic new visitor session-creation route and owner record in the same authoritative datastore, preserving workspace/tenant separation. Do not accept a client-supplied historical session ID as proof of ownership.
3. Implement explicit visitor consent UI with purpose, retention, consent version, revocation and privacy review.
4. Keep historical Ask Alpha demonstration metadata unlinked until individually verified under a lawful and authenticated process.
5. Independent creative/legal/finance/Stripe test and Executive acceptance remain open; no payout/delivery.

**No actual OIDC provider was connected or tested against this repository in Stage D. No live endpoint was enabled.**
