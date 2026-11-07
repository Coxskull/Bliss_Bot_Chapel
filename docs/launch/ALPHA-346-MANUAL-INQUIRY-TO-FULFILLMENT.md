# ALPHA-346 — first-customer manual operating procedure and Stripe readiness
**Status:** OFFLINE WORKFLOW READY FOR OPERATOR REVIEW; internal USD $50–$1,000 price band approved (2026-11-07), exact quote/terms/Stripe not approved; PAYMENT NOT ENABLED
**Directive:** ALPHA-FIRST-CUSTOMER-LAUNCH-346 | **Extra spend:** $0 | **Global STOP:** ENGAGED

## Exact existing public path
Public customer inquiry: https://alphacreativecontent.com/join-advertiser.html
Public service explanation: https://alphacreativecontent.com/services.html
Public contact: https://alphacreativecontent.com/contact.html
Existing Vercel form uses frontend/public/alpha-intake.js and the existing Bliss Bot Supabase alpha-public-intake Edge Function; server persists matching alpha_public_intake_requests/receipts and an existing server-side GoHighLevel delivery. The source-of-truth is **Bliss Bot Supabase project bkutbglzivfdnoerigyb**; the Railway marketplace database and Factory staging are separate. No alternate form, CRM or database is created.

## Manual operating states, owner and acceptance evidence
| Stage | Human responsible | Evidence / gate | Permitted next action |
| --- | --- | --- | --- |
| INQUIRY_RECEIVED | Intake system | Server-generated request and receipt after HTTP 201; CRM delivery is only routing evidence | Human review, not automatic outreach |
| REVIEW_PENDING | Authorized private operator | Confirm receipt link, privacy notice and lawful purpose; review PII only in existing restricted CRM/admin, not a public page | Optional **direct response to the customer's own request** if permitted by notice, owner policy and explicit outbound authorization |
| SCOPE_DEFINED | Sales/coordinator | Objective, market, planned outputs, rights and customer dependencies; do not assume creator availability | Prepare individualized proposal |
| PROPOSAL_PREPARED | Sales + owner/finance | Service scope, numeric quote, ISO currency, taxes/fees, schedule, support, Terms/refund/privacy versions | Route for internal approval |
| PROPOSAL_SENT | Authorized operator | Approved outbound communication record and exact proposal/version | Await separate written customer approval |
| CUSTOMER_APPROVED | Customer + operator | Evidence customer explicitly approved full commercial Order, policies, price/currency and dates; public form opt-in is not this approval | Queue approved payment method |
| PAYMENT_PENDING | Finance | Legal entity/merchant/processor and payment route separately approved; no card data via form; currently **BLOCKED** | Send secure invoice/payment request **only after live payments are separately authorized** |
| PAYMENT_CONFIRMED | Finance | Actual processor status and transaction reference reviewed; never infer from a draft, screenshot or browser redirect | Authorize fulfillment per Order |
| IN_FULFILLMENT | Human coordinator | Four planning deliverables tracked; no creator outreach, placement or automated factory | Submit draft and receive one consolidated review |
| DELIVERED | Coordinator | Final deliverable files, date, version and customer receipt acknowledgement | Issue closure note, handle lawful support/cancellation requests |
| CLOSED | Operator/finance | Customer acceptance, support contact, invoice/refund/dispute record if relevant | Retain data under applicable approved policy |

**No early shortcuts:** “delivered to GHL” ≠ contacted; “inquiry received” ≠ account; “customer approved” ≠ paid; “planning complete” ≠ ad displayed. STOP remains engaged for the factory. There is no bulk marketing, cold creator outreach, invitation, Stripe charge, provider call, or automatic campaign fulfillment in this document.

## Test / privacy evidence (read-only, 2026-11-07)
A fresh aggregate readback on **2026-11-07** found **3 advertiser + 2 creator requests**, all `NOT_YET_VERIFIED`, **3 advertiser + 2 creator receipts** and **5 CRM delivery ledger entries** in `DELIVERED` state. The advertiser dates in Philippine time were Nov 5 (one), Nov 6 (one), and Nov 7 (one). All three advertiser records carried the required processing/adult acknowledgments and optional marketing opt-in; this is not proof they are qualified customers. A new inquiry had insufficient campaign objective details, so a **private Gmail qualification-response draft** was prepared for human review, NOT sent. No customer identity or private lead details belong in GitHub. Do not create fake production submissions.

## Independent first-customer qualification (read-only check, November 7, 2026)
- The three historical advertiser submissions are **all NOT_YET_VERIFIED**. A private operator-only review found one explicitly test-labeled entry and two entries carrying ALPHA/operator-associated identifiers. All three supplied no campaign description. **None is independently qualified as an external paying customer.** This does not prove that there are no outside prospects elsewhere.
- Do **not** treat required processing acknowledgments or marketing-opt-in checkboxes as proof of business authenticity, a signed Order, or permission for indiscriminate sales outreach.
- **Do not discard or alter the stored inquiry records** simply because they appear internal. They are useful production-form acceptance evidence but do not count toward independent customer acquisition.
- Before a human responds as part of a real sales process, confirm that a different business has actually initiated a relevant request, its contact person can represent the advertiser, and its campaign objective is sufficiently specified to scope planning. If identity or test status is unclear, hold the reply rather than send a commercial solicitation.
- An unverified record can remain `INQUIRY_RECEIVED / NOT_YET_VERIFIED`. Separately record human verification and permission to reply in the existing restricted CRM or operator handoff, without copying contact names or email addresses into public GitHub or CI.
- A qualification email to the most recent apparent operator/test submission is **saved as an unsent Gmail draft**, not customer outreach. Actual qualified independent leads evidenced by this readback: **0**. A future independent inquiry can be reviewed using the same workflow, without changing intake schema, factory controls or public payment availability.

## Stripe readiness — exact remaining requirements
Read the existing `docs/stripe/CAPTAIN-APPROVAL-PACKET-20261106.md`, `docs/stripe/commercial-terms-CAPTAIN-REVIEW.md`, and `docs/stripe/refund-cancellation-policy-CAPTAIN-REVIEW.md`; avoid drafting competing public policies.
| Requirement | Current classification | Real release proof |
| --- | --- | --- |
| HTTPS public site, service description and advertiser inquiry | VERIFIED PUBLIC PAGE, RECEIPT PATH PREVIOUSLY VERIFIED | Fresh read-only page/browser + existing receipt count |
| Monitored customer support contact | **CAPTAIN-DESIGNATED: dejesuserwin1@gmail.com; account verified, external two-way email test PASSED, HUMAN MONITORING NOT VERIFIED** | Assign monitoring operator, backup, complaint escalation and approved response practices; payment/publication approval remains separate |
| Merchant country and payment currency | **Owner-reported on 2026-11-07: Stripe account USA; USD currency. NOT independently verified against Stripe Dashboard or processor API.** | Confirm this account is the intended ALPHA GLOBAL MEDIA LLC merchant profile, not a separate historic business. No account or collection approval implied. |
| Legal entity, US registration/address/tax identity, beneficial-owner verification, bank/payout, business supportability and Stripe live-charge status | **NOT VERIFIED / PAYMENT_BLOCKED** | Read actual Stripe Dashboard account-status banner and business verification checklist; never send tax IDs, ID documents, bank numbers, access tokens or personal information into GitHub or chat. |
| Custom price, currency, tax/fees, service schedule, invoicing party | **NEEDS APPROVAL PER ORDER** | Approved numeric order, authorized currency, dates |
| Paid-service commercial Terms, cancellation/refund/dispute policy | **DRAFTED INTERNALLY; NOT APPROVED/PUBLISHED** | Captain/Legal/Finance versioned approval, effective date, public access before payment |
| Commercial privacy notice and support-data handling | **NOT APPROVED** | Reviewed paid-service data/processor/retention disclosure |
| Secure Stripe invoice/Checkout test-mode and receipts/refunds/who may operate | **UNVERIFIED/NOT ENABLED** | Stripe account/test mode eligibility & private test acceptance, no real card charges |
| Processor/webhook/payment reconciliation if automated | **NOT REQUIRED FOR THIS INQUIRY-ONLY LAUNCH** | Required only before automated settlement/fulfillment, not to receive inquiries |
| Creator rights, booking, media inventory, campaign delivery | **NOT REQUIRED FOR PLANNING ONLY** | Mandatory for a separately sold placement service |

**Owner pricing decision (2026-11-07):** For this human-delivered planning service only, individually quote **USD $50.00–$1,000.00 inclusive**. This is an internal allowed range, not a standing public price, customer-specific approval, invoice, authority for payment, creator fee or advertised media-placement package. The amount inside that band, taxes, payment schedule and recipient/merchant must be approved for each actual order.

### Mandatory approvals for first *paid* manual Order
1. Captain approves one planning service and actual per-order price, currency/tax, deliverables, start/delivery dates, revision and ownership terms.
2. Captain/Legal/Finance approve versioned paid Terms, refund/cancellation and commercial privacy notice, and the actual verified monitored support path.
3. Finance/merchant owner verifies legal payment entity/Stripe merchant capability and specifically authorizes the payment method and a live transaction for the signed Order. **Not authorized now.**
4. A real customer accepts the complete Order and the policies separately; authorized operator documents the acceptance.
5. Deployment approval is separately required before the edited public service page is published; all other existing public inquiry forms remain unchanged.

### What does NOT block manual inquiry and proposal preparation
Railway PostgreSQL disaster recovery, Railway Restricted Preview, automated Sailors/factory deployment, creator matching, creator inventory, AI artwork/video, automated Stripe integration and the private operator queue PR #331. These must be revisited only if the proposed service actually depends on those protected systems. Existing public intake/CRM controls remain essential and should not be bypassed.

### Go/no-go
**PUBLIC INQUIRIES: YES, existing verified path.**
**MANUAL PROPOSAL PREPARATION: YES, source templates and human review.**
**SEND CUSTOMER PROPOSAL: ONLY after operator approves permitted reply and fills commercially material values; not sent in this task.**
**PAYMENT / FULFILLMENT OF A PAID ORDER: NO until approvals listed above.**
**DEPLOY / MERGE / OUTREACH / STOP RELEASE: NO.**
