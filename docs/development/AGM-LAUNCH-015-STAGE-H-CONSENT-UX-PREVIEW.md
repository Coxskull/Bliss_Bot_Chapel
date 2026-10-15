# AGM-LAUNCH-015 Stage H — visitor opt-in/withdrawal interaction preview

**Date:** 2026-10-15
**Tracker:** AGM-COMM-015-S08, parent AGM-COMM-015 remains open
**Implementation approval:** ALPHA owner approved engineering work, NOT real visitors' consent.

## Functional implementation
- `frontend/public/ask-alpha-consent-preview.html` presents a mobile-friendly, accessible, deliberately **unchecked** opt-in; a purpose-bound creative-preview notice; explicit description of denied transcript export, advertisement publication, contact, payment and payout; and a visible warning that it is non-production and will never submit consent to a server.
- `frontend/public/ask-alpha-consent-preview.js` manages an **ephemeral tab-only** state machine: NONE -> PREVIEW_OPT_IN -> PREVIEW_REVOKED. The latter is terminal in the current tab. No fetch, cookies, localStorage, sessionStorage, HTTP POST or real grant exists; reloading clears all state.
- `scripts/phase7-consent-preview-smoke.cjs` uses Playwright in the existing isolated three-system browser CI to assert default-off, deliberate checkbox selection, deselection, state transition, withdrawal, hard prohibition on reaccepting after withdrawal, no browser storage/network side effects and responsive mobile width. No paid AI calls.
- Existing CI enforces the Stage H browser smoke against an explicitly disposable loopback development fixture. No real visitor provider is linked.

## Consent terms (prototype copy, not yet legal-approved)
The version `AGM-CONSENT-PREVIEW-V1` and purpose `CREATIVE_PREVIEW_ONLY` are illustrative UI language. A future production version must state **exactly which content** is shared, with **whom**, under what retention/deletion policy, and how users can withdraw. It must separately ask for personal, authenticated, unbundled opt-in. Owner authorization to build this UX cannot stand in for that individual's choice.

## Denial invariants
There is no route to write consent records and no identity verification. An opt-in displayed in this page is not a consent receipt. Any synthetic opt-in/withdrawal state never authorizes historic transcript ownership, transcript linking or export, a payment/creator payout, marketing outreach or campaign delivery. `Delivery=NOT_SENT`.

## Next required stages
1. Obtain approved real visitor OIDC provider/client and test cryptographic token validation in controlled staging.
2. Approve real visitor-ownership schema, privacy/retention text and migration with cross-tenant and rollback testing.
3. Implement server-owned new session registration and real purpose/version-scoped authenticated consent APIs, with idempotent revocation and subject-access safety.
4. Upgrade this preview into a real UI only after server-authoritative CSRF, identity binding, data minimization, consent text review and rejection of forged/unauthenticated consent.
5. Keep all separate commercial/legal/finance/rights/production/launch gates closed until individually accepted.

**Production deployment and real consent capture were not part of Stage H.**