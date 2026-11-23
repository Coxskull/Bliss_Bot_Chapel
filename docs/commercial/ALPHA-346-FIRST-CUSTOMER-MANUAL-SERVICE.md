# ALPHA-346 — Manual Advertising Planning & Coordination (internal prelaunch service)

**2026-11-07 | Proposed service, owner/finance/legal review outstanding | No paid offer published | STOP ENGAGED**

Reuse existing ALPHA public `/join-advertiser.html` → `alpha-public-intake` Supabase Edge Function → `alpha_public_intake_requests` + `alpha_public_intake_receipts` → `alpha_ghl_delivery` / GoHighLevel. This is a **manual first-customer pathway**, not another factory or CRM.

## Defined planning deliverables (for customer-specific written quote)
1. **Campaign discovery brief:** business objectives, market, audience, platform preferences, compliance claims, materials and approvals.
2. **Creator/channel suitability plan:** evaluation criteria and potential formats only, not a claim of available or consenting creators.
3. **Creative direction outline:** message direction, candidate formats and approval requirements; no finished paid-media asset guaranteed.
4. **Coordination schedule:** scope-specific milestones, prerequisites, reviewers, dependencies and approval points, subject to signed order.
5. **Final planning summary:** agreed recommendations, revisions and next-step decision record.

**Exclusions:** no booked creator, fixed ad placement, guaranteed audience/conversions, live outreach, video production, campaign publication, automatic payment, payout or impressions.

## Manual workflow and independent gates
1. **INQUIRY_RECEIVED:** voluntary public advertiser form; consent confirmed, backend receipt shown. No customer account/order.
2. **HUMAN_REVIEW:** authorized operator confirms matching source receipt and CRM delivery, validates requested market/legality, consumer rights and permission to contact. CRM `DELIVERED` means delivery to CRM, not customer consent or a closed sale.
3. **PROPOSAL_DRAFT:** use `ALPHA-346-MANUAL-PROPOSAL-TEMPLATE.md` only in approved confidential workspace; link exact inquiry receipt privately; add customer-specific deliverable quantities and exclusions.
4. **OWNER/FINANCE/LEGAL_REVIEW:** require exact written scope, merchant identity, itemized price and ISO currency, taxes and third parties, milestones, included revisions, approved support address, effective paid-service privacy and terms, cancellations/refunds/disputes, usage rights, and appropriate signatories. No default price or deadlines invented.
5. **CUSTOMER_APPROVAL:** only a separately issued and accepted customer order is evidence; this offline workflow does not send, sign, approve, or invoice.
6. **PAYMENT:** **DISABLED**. Stripe is not activated or represented as ready. No payment details in public forms; no payment link/deposit until separate accepted release.
7. **FULFILLMENT:** **BLOCKED** pending accepted order, explicit authorization, prerequisites and no STOP-conflicting activity.

## Real metadata observations, not a new live submission test
Connected Supabase Bliss Bot `bkutbglzivfdnoerigyb`: active `alpha-public-intake` version 7. Read-only 2026-11-07 aggregates: **2 advertiser + 2 creator** `NOT_YET_VERIFIED` public inquiries; **2+2 matching receipts**; **2+2 GoHighLevel delivery records marked `DELIVERED`**. This is stored-record evidence, not proof of a fresh user submission, complete native GHL workflow, customer approval, or sale. Public source has `/services.html`, `/contact.html`, `/join-advertiser.html`, but external browsing was not able to independently retrieve these URLs today.

The intake table RLS is enabled but FORCE RLS is not; broad security acceptance cannot be inferred from RLS enabled alone. The current `docs/stripe/*CAPTAIN-REVIEW.md` terms and refund drafts are unapproved. A paid-customer support channel is also not independently verified.

## Task #1 disposition / independent manual launch
Task #1 **IN PROGRESS — actual logical restore and Nov 7 physical snapshot restore DEFERRED**, synthetic recovery tests passed. This outstanding DR gate is not automatically a dependency for non-transactional manual planning inquiries, though specific data security and recovery dependencies must be examined before paid orders. This file does **not** mark Task #1 complete, release STOP, or authorize commercial checkout.

**Engineering test:** `node --test scripts/alpha346-manual-proposal-gate.test.mjs`. It simulates only fictional receipts, never sends customer messages or executes a payment. No production source or CRM writes; cost $0.
