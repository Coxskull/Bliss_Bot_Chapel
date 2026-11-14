# Advertiser Account Registration Privacy Notice — OWNER / LEGAL REVIEW CANDIDATE

**Controller / proposed publisher:** ALPHA GLOBAL MEDIA LLC ("ALPHA", "we").
**Review date:** 2026-11-14. **Status:** DRAFT ONLY — not an effective or published notice.
**Source draft:** frontend/public/advertiser-account-privacy.html at cdc8852c1079316e7edb00f541e32218321d2ed0.
**Candidate identifier:** advertiser-privacy-2026-11-14-review-v2 (NOT configured in the registration application).
**Application identifier currently pinned:** advertiser-privacy-2026-11-07-v1.
**Effective date:** PENDING owner and counsel approval. No production acceptance authorized.
**Privacy contact:** Existing website contact route /contact.html; applicability of /privacy-request.html to account registration is NOT verified. Owner must approve a working account-privacy request channel before publication.

## 1. Which service this notice covers
This proposal describes an **advertiser account application**, which is different from the public advertiser-interest form governed by /alpha-privacy.html. Creating a sign-in or receiving an application receipt does not mean an advertiser has been approved, does not create an advertising order, and grants no automatic marketplace access. Registration is currently disabled.

## 2. Information collected and how it enters the service
- **Sign-in step:** the applicant provides an email address and password to the configured Supabase Auth endpoint. On the reviewed browser implementation, the password travels directly from the browser to Supabase Auth over HTTPS and is not included in the request body sent to ALPHA's registration API. Supabase Auth's separate storage, verification, logs, and retention require a provider-specific confirmation.
- **Business registration:** the applicant enters a required business name and two-letter country code, plus an optional HTTPS business website. The registration API checks length/format, and obtains an identity subject from verified session claims. A verified email claim is required for the pending-applicant lane.
- **Registration evidence:** the service is designed to store the opaque subject, advertiser identifier, registration request key, canonical request SHA-256 digest, privacy and terms version identifiers, required processing/terms choices, optional marketing choice, receipt identifier, pending-review state, and creation timestamps. These are retained as a registration record and append-only receipt in the proposed database schema.
- **Technical metadata:** hosting, authentication and network providers may process IP addresses, diagnostics and request metadata. Specific categories, destinations and provider retention are unverified and must be confirmed rather than assumed.

Do not include payment credentials, government-issued identity documents, or another person's confidential information in this application.

## 3. Why the information is used
The proposed purposes are creating and confirming a sign-in; associating a verified applicant with a business application; validating, deduplicating and securely processing submissions; documenting acknowledgment and marketing choices; preventing unauthorized account access; reviewing applications; handling support/privacy requests; and meeting applicable security or recordkeeping duties. Each purpose and applicable **lawful basis** must be reviewed for the relevant jurisdictions. Acceptance of terms is not automatic consent to marketing. Where consent is the applicable basis, it must be separately captured and capable of withdrawal as required by law.

## 4. Who handles the information
The source code identifies **Supabase Auth** for sign-in; an ALPHA-controlled registration database/API designed for the proposed membership and receipt records; **Railway** as the contemplated application/database host when independently authorized; and **Vercel** for the web/preview interface. Their effective deployed roles, processing regions, subprocessors, agreements and cross-border safeguards are **NOT VERIFIED** for this registration flow. The existing public inquiry flow separately mentions GoHighLevel/LeadConnector; account registration alone does not authorize HighLevel marketing or establish an account-to-CRM sync. If ALPHA later adds a recipient, processor or transfer, its legal disclosure must first be reviewed and updated.

## 5. Review status and account access
A successful business submission is designed to create a PENDING_REVIEW membership and an application receipt, not a marketplace entitlement. ALPHA would need a separately authorized review/approval process before granting privileges. The production deployment of this behavior has not been demonstrated.

## 6. Retention and deletion — POLICY DECISION REQUIRED
The registration identity, application profile, receipts, security logs, backups and any provider-held data **do not yet have an owner-approved retention schedule**. Do not rely on the separate 90-day public interest-form cleanup period for account registration. The existing receipt design prevents ordinary receipt updates/deletes and may be subject to lawful audit retention; the company must define an authorized erasure/anonymization approach, relevant legal exceptions, backup lifecycle and processor deletion process. These rules must be approved and reflected accurately in the published notice **before the feature is enabled**.

## 7. Access, correction, deletion and marketing choices
An applicant may seek information about records associated with the application, request corrections/deletion where legally applicable, or ask to withdraw optional marketing consent. Requests require appropriate identity verification and review of lawful retention exceptions; do not send passwords or payment information in a form. The current /privacy-request.html is explicitly scoped to public interest forms, so it **cannot yet be represented as a verified end-to-end account privacy-rights channel**. ALPHA must approve and verify a suitable account channel and processing workflow before activation. Applicable rights and response periods depend on governing law and must be checked by counsel.

## 8. International handling and security
The submitted data may be processed in the jurisdictions used by the selected auth, hosting, delivery and backup providers. Actual locations, transfer mechanisms and security agreements are **PENDING verification**. ALPHA intends to restrict pending accounts and require HTTPS, but this draft makes no guarantee of absolute security, universal legal compliance, deletion timing or currently deployed isolation.

## 9. Changes and contact
For material changes, ALPHA should publish a dated notice with a new version identifier and update the account registration UI/API/receipt binding before accepting new submissions. The final legal entity contact details, effective date, request channel, geographic scope and approved retention schedule require owner decision and independent counsel review.

**Related proposed terms:** ADVERTISER_REGISTRATION_TERMS.md.
**Do not publish or rely on this draft as operational consent text.**
