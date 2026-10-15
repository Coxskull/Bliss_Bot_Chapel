# AGM-LAUNCH-015 Stage I — default-denied server consent command contract

**Date:** 2026-10-15
**Tracker:** AGM-COMM-015-S09; parent AGM-COMM-015 owner-approved engineering, actual visitor consent still NOT DONE.
**Scope:** Repository-only authenticated HTTP contract; NO real consent write storage or production activation.

## Implementation
- Introduces `AskAlphaVisitorConsentCommandGate` with an explicit request shape: exact `CREATIVE_PREVIEW_ONLY` purpose, draft `AGM-CONSENT-PREVIEW-V1` version, explicit opt-in checkbox, constrained request key and separate withdrawal intent.
- The gate uses existing default-disabled `AskAlphaVisitorEnrollmentGate` issuer-scoped visitor claims prerequisites, rejects staff/advertiser/foreign/malformed/disabled identities, forged purpose/version, reused prior-session claims, invalid consent or withdrawal selection.
- `AskAlphaVisitorConsentController` introduces read-only `GET /api/ask-alpha/visitor-consent/readiness` and POST `opt-in`/`withdraw` command contracts. The controller is `[Authorize]` and `[ResponseCache(NoStore)]`; POSTs are write-rate-limited. Existing ASP.NET middleware validates CSRF for authenticated `/api` unsafe methods. This code does not bypass existing authorization/CSRF.
- **All POST requests return HTTP 409 and no records are created**: the authoritative visitor-owned session + legal-approved consent ledger do not exist. GET returns `NOT_RECORDED` and does not disclose historic sessions/contacts/transcripts.
- Unit tests cover exact claim prerequisites, forbidden roles, malformed purpose/version/checkbox/retry input, authorization attributes, controller response bodies and universally false transcript/export/payment/payout/delivery flags.
- New isolated HTTP smoke `scripts/phase8-consent-backend-denial-smoke.cjs` sends only synthetic requests to the local Development fixture, validates no user-consent authority, no payment and `Delivery=NOT_SENT`. CI runs smoke as part of existing three-system browser workflow. Browser Stage H opt-in remains client-side only and is deliberately **not connected** to this endpoint.

## Trust and privacy boundary
Even when the OIDC-claims shape matches, this is **NOT** cryptographically verified real visitor ownership. Owner approval to implement and a client's checkbox value do not equal a real visitor consent receipt.

No production identity provider is configured or validated. There is no real visitor-ownership migration, consent storage, opt-in/withdrawal or transcript release. The draft `AGM-CONSENT-PREVIEW-V1` notice is **not** legally approved. No outreach, live charge, creator payout or campaign delivery.

## Next work: true authenticated consent
1. Approve and connect visitor OIDC issuer/client under staging, validate real signed tokens and cookie/CSRF flows; deny staff/advertiser/foreign/ambiguous claims.
2. Approve and migrate a real visitor ownership + consent event schema in isolated staging with tenant constraints, immutable audit/revocation, verified new-session issuance and no historic session adoption.
3. Integrate Stage H UI with authenticated session-bound opt-in, purpose/version/retention/recipient notice, personally initiated withdrawal, negative concurrency/forgery/replay tests.
4. Make consent revocation cascade into any derived content or future sharing; complete independent privacy, creative, financial, hosted and Executive acceptance.

**This Stage I code must not be described as a working real consent backend. The API is deliberately blocked.**