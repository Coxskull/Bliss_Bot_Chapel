# ALPHA-346 — Manual first-customer commercial release checklist

**Decision status: INQUIRY-ONLY EXISTING; CUSTOMER-READY PAID SERVICE NOT AUTHORIZED**  
**Global STOP: ENGAGED | Incremental new spend: $0 | Scope: existing ALPHA GLOBAL MEDIA LLC system**

## What has been verified and may be used without paid activation

- ALPHA public site source already contains `/services.html`, `/contact.html`, `/join-advertiser.html`, and public-interest privacy/terms pages. The deployed site was **not independently browser-reverified this session** (external network/browser tools unavailable).
- Connected Bliss Bot Supabase project `bkutbglzivfdnoerigyb` stores aggregate **2 advertiser + 2 creator** intake requests, each with matching receipt and `alpha_ghl_delivery` record marked `DELIVERED`. All four were `NOT_YET_VERIFIED`. This proves persisted source/CRM state, not a new form submission or marketing consent.
- Owner explicitly selected **dejesuserwin1@gmail.com** for this manual commercial work. Connected Gmail profile confirms that exact account is accessible via the owner's integration, but **no inbound delivery/reply test, assigned monitoring schedule, or paid-support escalation acceptance has been performed**. Draft public contact copy may invite ordinary inquiries to that address; it does not claim a guaranteed response time.
- One proposed service is defined in `ALPHA-346-FIRST-CUSTOMER-MANUAL-SERVICE.md`: **Campaign Planning & Coordination**, comprising campaign discovery, creator/channel suitability analysis, creative direction outline, coordination timeline, and planning summary, with no creator booking/advertising publication guarantee.
- `tools/commercial/alpha346-proposal-desk.html` is an **offline browser tool**, excluded from Vercel public build. It drafts proposals and can print marked *internal* drafts. It does not store data, send email, access the DB, request payments or represent a legally accepted quote.

## Mandatory explicit decisions for a PAID manual order — currently OPEN

| Gate | Required evidence | Current |
| --- | --- | --- |
| Seller/merchant identity | ALPHA legal name, contracting entity and Stripe registered country match | NOT VERIFIED |
| Support route | Verified inbox ownership, **inbound test**, named operator, complaint/refund escalation | EMAIL ACCOUNT CONNECTED; MONITORING NOT VERIFIED |
| Scope | Approved five planning deliverables, exclusions, rights, market eligibility, client proof obligations | DRAFT |
| Price/currency | Customer-specific approved quote, ISO currency, itemized taxes/fees, third-party costs, timing | NOT APPROVED |
| Delivery | Order-specific milestones, revision count, acceptance method, exceptions | NOT APPROVED |
| Paid commercial terms | Counsel/owner approved version and effective date, visible to buyer before payment | NOT APPROVED |
| Cancellation/refunds/disputes | Approved eligibility, work-stage consequences, procedure and contact, published policy | NOT APPROVED |
| Paid-customer privacy | Correct handling of customer/order/payment metadata and processor sharing | NOT APPROVED |
| Payment mechanism | Merchant/payment eligibility and applicable processor or manual invoicing approval; safe test | DISABLED |
| Customer approval | Actual identity/representative, written order acceptance, traceable service receipt | NONE |
| Fulfillment control | Exact signed scope and lawful activity permitted without changing global STOP | BLOCKED |

**No generic signoff, checked checkbox or draft content constitutes an effective customer contract.** Existing draft terms/refund texts in `docs/stripe/*CAPTAIN-REVIEW.md` remain **NOT EFFECTIVE** and must not be published as operative policies. A proposal is not a Stripe-approved checkout, and no fixed quote amount/delivery deadline is invented.

## Next actions using existing resources

1. Use connected Gmail inbox `dejesuserwin1@gmail.com` to manually verify **inbound receipt** and set a real monitoring owner/schedule, without publishing an SLA until tested; no bulk outreach or customer campaign messaging.
2. Sign off the exact manual **planning-only** service scope, quote method, approved markets/currency, revisions, rights, merchant identity and official support route.
3. Counsel/owner/finance review existing *draft* paid-service terms and refund/cancel policy; approve and date final versions, and separate paid-client privacy practices. Do not deploy drafts.
4. For an expressly consented advertiser inquiry, authorized operator verifies private receipt and CRM record, composes a **private** quote in the offline Proposal Desk, obtains customer acceptance through an approved written process, then seeks separate payment release. Never paste private prospect identities into CI.
5. Only afterward consider Stripe activation or a separately approved lawful manual invoice mechanism, and recheck specific production security and recovery dependencies. Do not waive active security controls merely to collect funds.

## Task #1 status

Disaster-recovery development and synthetic PostgreSQL backup/restore acceptance **PASSED**. Actual staging database export/restore, hosted Auth/MFA restoration and November 7 managed physical snapshot recovery **DEFERRED / NOT TESTED**. Full Task #1 remains **IN PROGRESS**. That deferred scope does not by itself prevent the limited **nonpayment** manual public-inquiry service, but is not waived for a paid service without documented risk/security review.

## Current source references

- `docs/stripe/CAPTAIN-APPROVAL-PACKET-20261106.md` (internal draft)
- `docs/stripe/commercial-terms-CAPTAIN-REVIEW.md` (internal draft, do not publish)
- `docs/stripe/refund-cancellation-policy-CAPTAIN-REVIEW.md` (internal draft, do not publish)
- Official Stripe website checklist: https://support.stripe.com/questions/business-website-for-account-activation-faq
- Official Stripe minimum site data: https://support.stripe.com/embedded-connect/questions/information-required-on-your-business-website-to-process-payments

**Code publication:** proposed contact-page wording remains on a DRAFT GitHub PR, not deployed or accepted as live production. All commercial permissions remain source-specific.
