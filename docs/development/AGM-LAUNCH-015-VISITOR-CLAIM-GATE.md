# AGM-LAUNCH-015 Stage A - Visitor identity claim prerequisite

**Date:** 2026-10-15
**Spreadsheet:** AGM-COMM-015-S01 child of AGM-COMM-015
**Scope:** Repository-only; no production migration, endpoints or private conversation export.

## Added
- Explicit Ask Alpha visitor role, session-claim type and *unconfigured by default* visitor issuer in existing authentication options.
- Pure `AskAlphaVisitorClaimGate.Inspect` requires authenticated ASP.NET ClaimsPrincipal, visitor role, no staff/advertiser/reviewer role, exactly one subject and one session UUID claim, exact configured claim issuer and matching requested session UUID.
- Returning `OIDC_CLAIMS_PRESENT_HISTORY_UNVERIFIED` means **only** issuer-labelled claim values are internally consistent. Historical session ownership is not established; real visitor authentication provider provisioning/configuration is not present.
- All consent, transcript access/export, payment and delivery authority remains false and delivery NOT_SENT.
- Unit tests cover valid synthetic claims; missing auth/config, wrong session, malformed subject, spoofed issuer, duplicate claims and staff/advertiser/viewer/reviewer exclusion.

## Explicit limits
The gate is **not wired to any HTTP route**, data persistence, hosted OIDC configuration, consent ledger or historical Ask Alpha session. It trusts that a future real authentication middleware would authenticate claims; the unit test creates artificial principals and **is not evidence of independent identity verification**. The existing AGM-LAUNCH-014 ledger remains test-only on financial_test. No user consent text/retention protocol is implemented.

## Remaining for parent AGM-COMM-015
- Owner-approved IDP visitor role provisioning and cryptographically verified issuer / callback configuration.
- Server-side visit/session ownership binding, not a claimed UUID alone.
- Explicit opt-in request, informed text/purpose/version, independent audit trail and verified revocation.
- Consent-controlled transcript storage, access controls and privacy review.

## Acceptance
Require exact-head four GitHub Actions workflows and independent reviewer approval before merging. Mark Stage A as engineering-ready for review only; parent visitor consent implementation stays NOT DONE.
