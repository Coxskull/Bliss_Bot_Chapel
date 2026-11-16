# Advertiser Account Registration Privacy Notice — release candidate, NOT PUBLISHED

**Controller:** ALPHA GLOBAL MEDIA LLC ("ALPHA").
**Reviewed:** 2026-11-15. **Status:** unpublished non-production documentation. Do not present as a live notice.
**Candidate ID:** `advertiser-privacy-2026-11-14-review-v2`. **Current UI/API/receipt ID:** `advertiser-privacy-2026-11-07-v1`.
**Effective date:** not set. A published, effective version must match browser, server and receipt exactly before any authorized registration.
**Owner-designated controller privacy contact (2026-11-16):** `alpha7auto@gmail.com` for ALPHA GLOBAL MEDIA LLC. This address is approved for the eventual advertiser-account privacy notice; actual mailbox delivery, monitoring, identity-verified case handling and response outcomes have **not yet** been independently accepted. Existing `/contact.html` and `/privacy-request.html` remain public-inquiry-only routes.

## 1. Scope and lawful processing

This proposed notice concerns the **advertiser account application**, not public advertiser interest or a paid campaign. The application is designed to create a `PENDING_REVIEW` record; an auth sign-in or receipt grants no marketplace privileges. Registration is disabled in the reviewed source.

ALPHA's processing purposes are: accepting and assessing an application, confirming sign-in, preventing duplicates and abuse, recording required acknowledgments and separate marketing choices, enforcing access controls, handling privacy/support requests, and meeting applicable legal obligations. Depending on jurisdiction and processing purpose, a valid basis may be steps requested before entering a contract, legitimate interests where permitted and balanced, legal obligations, or freely given specific consent. **A required checkbox does not itself establish that consent is the correct legal basis for all processing.** Optional marketing authorization is separate, unchecked by default, and must be withdrawable through a real channel before any marketing is enabled.

## 2. Information and source-to-destination accuracy

- **Supabase Auth:** the applicant submits email and password directly from the reviewed browser to the configured Auth endpoint over HTTPS, rather than sending plaintext password in the Bliss registration API request. Supabase handles credential verification and related identity records, subject to deployment and provider verification.
- **Business API and proposed PostgreSQL records:** business name, two-letter country code and optional HTTPS website; verified identity subject; request key; canonical request digest; selected privacy and terms version IDs; required processing and terms selections; separate marketing choice; registration and receipt IDs; `PENDING_REVIEW` membership; timestamps. The source represents a proposed design, **not verified hosted storage**.
- **Technical providers:** security logs, device/network identifiers, IP address or diagnostics may be processed by the actual Auth/hosting providers. Do not assert specific log contents or regions without provider evidence.
- **Recipients and verified deployment footprint (2026-11-16 read-only provider inventory):** Railway's existing `bliss-bot` application and PostgreSQL 18 service are configured in region code `iad` in the same **production-only** Railway environment. The distinct Bliss Bot Supabase project `bkutbglzivfdnoerigyb` is configured with project region code `ap-southeast-1` and PostgreSQL 17; it lists only its default `main` branch. These are **service configuration facts, not proof** that this Supabase project is the effective advertiser OIDC issuer, that applicant data has been stored there, or that all logging, backup and subprocessor data remains in those regions. The actual OIDC client/issuer, provider contracts, cross-border transfer safeguards, logs, third-party recipients and retention remain unverified. Vercel is used for a protected preview; a protected preview build is not an accepted production data-flow receipt. The public inquiry flow may use HighLevel/LeadConnector, but **there is no demonstrated account-to-CRM sync** and no automatic authorization to send marketing or share account records with a CRM.
- **No payment:** account registration is not an advertising order and the reviewed registration API has no payment collection.

## 3. Retention criteria and deletion limitations

The controlling principle is purpose limitation and data minimization: retain only while needed for application review, authorized account administration, security evidence, a legitimate documented dispute, or a specific legal obligation; then delete or irreversibly de-identify when technically and legally possible. This **does not imply a currently implemented deletion schedule**.

| Record class | Policy criterion to implement | Current technical limit |
|---|---|---|
| Auth identity / credentials | While sign-in is needed; on verified account closure assess revocation and identity deletion | Supabase Auth account-deletion and session-revocation flow not verified |
| Pending, declined or approved business profile | Through review/appeal or active account; minimize after the relevant purpose ends | No hosted expiry/deletion job accepted |
| Consent/registration audit receipt | Preserve only the minimum defensible evidence for specific audit/claims obligations | Existing proposed receipt is append-only and ordinary updates/deletes are restricted; erasure/anonymization must be designed rather than promised |
| Security and support logs | Shortest documented operational or mandated period | Actual host/provider logging and expiry unverified |
| Encrypted backups and processor copies | Defined rolling backup lifecycle, legal hold exception and verified expiry | No account-specific backup deletion/retention acceptance |
| Marketing preference/withdrawal | Retain minimal suppression/evidence data needed to honor the choice | No verified account-marketing withdrawal endpoint or CRM propagation |

Before publication, ALPHA must fill in **the actual retention periods or objectively determinable criteria**, applicable local exceptions, ownership of the deletion workflow, provider obligations and backup expiry. The separate **90-day public interest-form** rule is not an account retention schedule. Do not claim all traces or immutable audit records will be immediately deleted.

## 4. Data-subject requests and account support

Subject to applicable law, applicants can request information, access, correction, deletion/restriction, objection, portability and withdrawal of optional marketing choice where the right applies. The operational procedure to implement is: receive request in a clearly labeled account channel; record case/reference and receipt time; verify identity proportionately without asking for passwords or payment credentials; classify jurisdiction and request type; locate the relevant Auth/profile/receipt/provider records; apply lawful retention exceptions; safely fulfill or explain the result within the applicable legal deadline; record evidence of resolution and securely communicate outcome.

**Current implementation exception:** ALPHA has designated `alpha7auto@gmail.com` as its advertiser-account privacy contact, but authenticated intake, monitored email acknowledgment and final resolution have not yet passed hosted acceptance. `/privacy-request.html` explicitly handles **public interest form submissions only**, and `/contact.html` lists public inquiries rather than a verified account-specific case queue. Neither may be represented as accepted account privacy-request processing. The frontend source must not link to the inquiry-only request form as an account-rights solution. No account rights or support response SLA is currently proven.

## 5. Security and international processing

The proposed API uses verified issuer-bound identity, pending-review role restrictions, CSRF protections, explicit consent versions and an immutable receipt model. These are source-level observations only: hosted login, tenant denial, rollback, isolation and backup readiness have not passed. ALPHA cannot guarantee absolute security. A country code on the application does not automatically determine which privacy law applies. Territorial scope, collection location, processor countries, transfer mechanism, local notice language, regulator and response deadline must be established **per actual market and deployment**, using the companion international legal applicability matrix. Do not state universal GDPR or global legal compliance.

## 6. Notice changes and launch gate

An operative notice needs a real effective date, correct controller/privacy contact, accurate providers/transfer description, approved retention criteria and a verified request route. After those are confirmed, bind **one** new identifier across HTML, JavaScript, server constants and immutable receipt version evidence, with tests. Do not enable registration or infer prior consent from draft versions. Material changes may require fresh disclosure and/or acceptance under applicable law.

**Related:** `ADVERTISER_REGISTRATION_TERMS.md`, `CONSENT_DISCLOSURE_MATRIX.md`, `INTERNATIONAL_APPLICABILITY_CHECKLIST.md`, Issue #401.

**Decision already settled:** owner chose international business orientation and no paid external counsel. This does not waive jurisdiction-triggered requirements or the need for technically truthful disclosures. No generic counsel-approval gate is imposed by this document.
