# Advertiser Account Registration Terms — release candidate, NOT PUBLISHED

**Proposed contracting party:** ALPHA GLOBAL MEDIA LLC.
**Reviewed:** 2026-11-15. **Status:** non-production release candidate, not an accepted or effective customer agreement.
**Candidate ID:** `advertiser-terms-2026-11-14-review-v2`. **Current source UI/API ID:** `advertiser-terms-2026-11-07-v1`.
**Effective date:** not set; publication, full version binding and hosted acceptance are outstanding.

## 1. Purpose

An application requests review for a potential ALPHA advertiser account. It is **not** a public-interest inquiry, purchase, advertising campaign order or guarantee of account approval. A sign-in or registration receipt does not grant access to an advertiser workspace, creators, inventory or paid services.

## 2. Applicant responsibilities and eligibility

A person applying for an organization must be legally capable of using the service, authorized to submit that organization's information and entitled to control the supplied email address. Provide accurate business name, country code and optional HTTPS website; do not impersonate others or submit another person's private material. **The reviewed UI verifies an email through Auth when enabled but does not independently prove age, entity incorporation or representative authority**; these remain potential manual review subjects, not asserted automated checks. Country-specific eligibility rules may apply at actual release.

## 3. Sign-in and security

The reviewed frontend sends credentials to Supabase Auth, not the business registration API. Protect sign-in details and promptly report suspected unauthorized access via the account-support route once it has been verified and published. ALPHA may prevent abuse, reject forged requests and restrict access while applications await review, subject to applicable law. An accepted sign-in is not an approved advertiser account.

## 4. Application review

The source registration path is designed to create `PENDING_REVIEW` membership with a durable receipt. ALPHA may request clarification or decline an application based on a lawful, consistently applied eligibility/security policy. It must establish who conducts review, how to correct records or contest an error, how any approval is recorded, and how closure works **before claiming those capabilities exist in production**. No automatic workspace authorization follows from a successful POST.

## 5. Business information and restricted permission

The applicant represents they are authorized to submit the supplied business name and website. For an application **only**, they propose to permit ALPHA to use those details solely for validation, fraud prevention, review and necessary responses. That is **not** a license to run advertisements using the applicant's marks, publish creative assets, sublicense content or claim ownership of applicant intellectual property. Any future creative, placement or campaign rights must appear in a separate, specifically accepted written order.

## 6. Prohibited conduct

No false identity, unauthorized third-party personal data, injected malicious content, credentials or government identifiers in business fields, unauthorized access attempts or misuse of the registration system. Future advertisement-content rules are a separate decision and are **not silently incorporated** into account application terms.

## 7. Separate processing and marketing choices

Account processing acknowledgment, terms acceptance and optional marketing are distinct. Marketing is unchecked by default; declining it must not prevent a registration application. A valid privacy notice must give purposes, recipients, retention criteria, actual privacy request route, and other information required by the relevant law. The receipt design captures versions and choices, but actual hosted persistence and optional marketing withdrawal remain unverified. No consent to share account records with a CRM or send marketing arises from these terms.

## 8. No fee, payments or campaign entitlement

There is no fee for filing the reviewed account application, no automatic order and no payment authorization. Advertising prices, tax treatment, revision limits, campaign permissions, refund/cancellation terms and delivery obligations must be separately agreed for a paid transaction. These application terms offer no refund for a payment that has not been made.

## 9. Applicant support, privacy complaints and account closure

**Designated company privacy contact:** `alpha7auto@gmail.com` (owner decision, 2026-11-16). Before activation, verify mailbox access/monitoring and publish one functioning account-specific route for review questions, incident reports, correction, deletion and marketing withdrawal. The designated email alone does not prove a case has been received, identity-verified or resolved. ALPHA's human operator should classify requests, verify identity proportionately, assign a case reference, escalate security/privacy issues, record a lawful disposition and communicate within applicable local deadlines. Existing `/contact.html` and `/privacy-request.html` currently address public inquiries, and their successful use for account-specific requests is **not verified**. No guarantee of a support response time or deletion date is offered without operational evidence.

ALPHA may suspend an account for a genuine security risk or close a declined application when authorized, subject to mandatory notice, fairness and data-rights requirements. Procedures for review/reversal and retention exceptions must be documented before making any further promise.

## 10. Applicable law and disputes

Mandatory consumer, privacy, communications and other laws apply where their jurisdictional tests are satisfied, regardless of what these terms say. The company does **not** impose an unreviewed arbitration clause, venue waiver, automatic damages cap, indemnity, nonrefundable registration fee or universal choice of law. Complaints should first use the verified support route once available; legally protected rights to contact a regulator or pursue court remedies are not waived.

The international jurisdiction matrix identifies concrete checks; it is not a claim that a single notice is legally sufficient for every country. Paid legal counsel is **not a blanket deployment prerequisite**, but neither is the absence of counsel proof of compliance.

## 11. Updates and effective date

These terms are not live. An effective dated version must be approved by the company, published, and bound exactly to browser, server-side version check and immutable receipt. Material changes may require separate notice or acceptance in affected markets. Registration remains disabled until independently accepted hosted Auth/API/database, privacy-request and launch controls pass; global STOP stays engaged.

**Related:** `ADVERTISER_REGISTRATION_PRIVACY_NOTICE.md`, `CONSENT_DISCLOSURE_MATRIX.md`, `INTERNATIONAL_APPLICABILITY_CHECKLIST.md`, Issue #401.
