# ALPHA-346 — Stripe Merchant Readiness, Read-Only Acceptance Packet
**Scope:** first customer of the already-defined **manual One-Campaign Planning & Coordination** service.
**Status:** INTERNAL REVIEW — SOURCE-PREFLIGHT PREPARED; LIVE CHARGES AND PUBLIC PAYMENT RELEASE **BLOCKED**
**Date:** 2026-11-07 (Philippine time) · **Incremental spending:** $0 · **Global factory STOP:** ENGAGED

## Why this is a separate task
The public website already accepts voluntary advertiser *inquiries*. The first paid human planning order is independent of creator matching, media inventory, Railway factory jobs and a database disaster-recovery program. Stripe and contract controls must still be satisfied **before any actual payment**. An engineering check never overrides legal, finance, customer, or merchant authorization.

## Payment-readiness evidence — what is known and what is not
| Control | Evidence presently available | Decision |
| --- | --- | --- |
| Legal business | Source identifies ALPHA GLOBAL MEDIA LLC | Legal seller/merchant match requires confidential Finance review |
| Stripe country/currency | **Owner-reported** Dashboard USA / USD, verification status “Verified” on Nov 7 | Reported only; not independently verified through Stripe connector/API |
| Actual live charge and payout capability | No authoritative `charges_enabled`, `payouts_enabled`, restrictions or settlement readback | UNVERIFIED — do not infer from “Verified” status |
| Advertiser sales page | Proposed single planning package, individual quotation, exclusions, no checkout | Draft PR only; production copy not independently released |
| Contact route | `dejesuserwin1@gmail.com` and controlled two-way noncustomer email test passed | Named paid-customer operator, backup, coverage and complaint escalation NOT VERIFIED |
| Refund/cancellation | Internal manual service draft exists | NOT APPROVED / NOT PUBLISHED |
| Paid-service Terms | Internal manual service draft exists | NOT EFFECTIVE / NOT PUBLISHED |
| Commercial privacy notice | Internal manual service draft exists | NOT APPROVED / NOT PUBLISHED |
| Exact customer price | Internal USD $50–$1,000 range approved for individualized quoting | No customer-specific amount, taxes/fees, milestones or acceptance authorized |
| Order, payment, fulfillment | No independently verified paying customer, signed order or cleared Stripe payment | NOT AUTHORIZED |

The current *interest-form* Terms and Privacy Notice do not replace commercial policies for paying customers. No API secret, bank record, identification number, live payment intent, test charge or cardholder data is needed for the **read-only** checks below.

## Human Stripe Dashboard review (no keys; no charge)
The authorized Stripe business operator can open the actual connected Dashboard privately and record **only statuses and sanitized evidence references** in a restricted company file:
1. Which legal entity is the merchant of record for direct **planning-only professional services**? Does the name, address and relevant country match the real company's approved seller records and website?
2. Does Stripe currently show any **information requirements, account restrictions, business verification issues or website review actions**? Record yes/no/pending and the relevant dashboard notification reference without copying documents.
3. Is the account allowed to **accept charges** for the planned product/customer/market? Do not assume that “Verified” alone means `charges_enabled` or payment-method eligibility.
4. Are **payouts/bank settlement** supported and enabled for this business, with no unsupported account country or entity mismatch? Record result only, not bank data.
5. Is USD the actual intended transaction currency for the specific customer Order and what tax/fee treatment will the business disclose? Owner-approved internal quote band is not an individual quote.
6. What secure payment method, receipt, refund procedure and operator permissions will apply **if** a paid Order is separately approved? Do not activate or generate any payment link during this read-only review.
7. Check whether Stripe requests **website/domain ownership evidence** or final public contact/terms/refund/privacy information; supply only through Stripe's authorized process after owner approval. No fabricated business address or mailbox.

**Never share:** account keys, webhook signing secrets, recovery codes, full bank details, tax documents, private card data, account IDs or unrestricted dashboard screenshots in GitHub, ChatGPT prompts, publicly accessible files or customer emails.

## Engineering verification — safe command
Run from the existing repo root:
```sh
node scripts/alpha346-stripe-preflight.mjs
node scripts/alpha346-stripe-preflight.test.mjs
```
This scans **source files only**. It will never contact Stripe or publish a website. `SOURCE_CHECKS_PASSED` means the source is consistent with an **inquiry-only** offer, not that payments may be accepted. `SOURCE_REPAIR_REQUIRED` exits nonzero and stops CI acceptance; any result still reports `PAYMENT_BLOCKED`. The test also injects negative scenarios (checkout links, missing support information, unpublished policy mislabeling) and verifies these cannot release payments.

## Before the owner separately permits a first paid order
- [ ] Obtain authorized owner/legal/finance signoff for one **exact** planning scope: four documents and one consolidated in-scope revision, no real creator/media commitments.
- [ ] Confirm the actual business legal seller and merchant eligibility, country, charge/payout and dispute capabilities through a private Stripe Dashboard check.
- [ ] Approve and publish the specific effective **paid-service** commercial Terms, refund/cancellation policy and privacy notice with actual contact/rights/jurisdiction information, before presenting an actual purchase.
- [ ] Assign the monitored human support operator, backup, complaint/chargeback escalation and appropriate coverage. Do not invent a guaranteed response window.
- [ ] Approve a customer-specific **numeric** USD amount, applicable taxes/fees, any schedule/deposit, delivery dates, content usage rights and refund method. Keep the internal quote band private.
- [ ] Receive authentic business authorization and written approval of the specific customer Order and all applicable policy versions.
- [ ] Obtain a distinct live-payment release decision for the verified Stripe merchant (and any payment-method test authority) **after** those gates. Do not treat a passed repository build, self-reported checkbox or email as a release receipt.
- [ ] Human coordinator fulfills the four agreed written planning deliverables only after the signed Order's actual start conditions are met.

## Record template (restricted owner/finance workspace, NOT GitHub)
| Evidence | Result | Confidential reference / reviewer / date |
| --- | --- | --- |
| Account legal seller and country/currency match | PENDING | [PRIVATE] |
| Dashboard actual charges/payouts enabled status and restrictions | PENDING | [PRIVATE] |
| Authorized payment method and permitted market | PENDING | [PRIVATE] |
| Effective public terms/refund/privacy URLs and versions | PENDING | [PRIVATE] |
| Named human monitoring owner and support escalation | PENDING | [PRIVATE] |
| Exact customer Order and quote/tax/currency | PENDING | [PRIVATE] |
| Customer written acceptance | PENDING | [PRIVATE] |
| Specific live-payment release authorized | **NO** | [PRIVATE] |

## Authoritative Stripe references
- Business website requirements: https://support.stripe.com/questions/business-website-for-account-activation-faq
- Website information for payment acceptance: https://support.stripe.com/embedded-connect/questions/information-required-on-your-business-website-to-process-payments
- Website ownership verification: https://support.stripe.com/express/questions/website-ownership-verification-during-stripe-account-application
- Account information verification: https://support.stripe.com/questions/business-information-requirements-to-use-stripe

**Release verdict:** `INQUIRY_ONLY` / `MANUAL_PROPOSAL_PREPARATION_ALLOWED` / `PAID_ORDER_NOT_AUTHORIZED` / `STRIPE_LIVE_PAYMENTS_BLOCKED` / `STOP_ENGAGED`. This packet is not legal advice, a Stripe verification, a published policy or evidence of a completed customer transaction.
