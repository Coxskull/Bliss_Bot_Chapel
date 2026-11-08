# ALPHA-SELECTIVE-STOP-REFORM-445 — commercial STOP scope and live status

Date: 2026-11-08 (America/New_York)
Repository: `Coxskull/Bliss_Bot_Chapel`
Existing draft PR: #356, branch `feature/marketplace-426-budget-first-guide`

## Decision

Factory STOP is a research/dispatch control. It does not need to gate public advertiser intake, human inquiry review, internal campaign planning, or preparation of a customer-specific draft quote. No runtime factory STOP check was found in those inbound/manual paths, so this review removes the blanket STOP prerequisite from PR #356's commercial release documentation; it does not clear or bypass the factory control.

A human may review an inbound brief and prepare an internal plan independently of the autonomous factory. This does not establish a monitored post-purchase support channel or a paid service offer. Send a follow-up only after the applicable contact permission, identity, authority, and suppression checks pass. Issue a customer-specific quote only after scope, delivery capacity, costs, currency, tax, price, and applicable written terms are established. The current inventory and payment evidence below do not support a placement quote or charge.

## STOP and action-control audit

| Workflow | Actual control and evidence | Selective outcome |
|---|---|---|
| Factory research / Sailor dispatch | Active Alpha Lead Generation Supabase project `yyesqcyjhqjryaskrqip`; `alpha_factory.factory_control` contains the single `factory=STOP` row, last updated 2026-10-30. Factory registry and assignments are separate from the Bliss Bot inbound-intake project. | Keep STOP for autonomous research, dispatch, and factory writes until that factory's own release criteria pass. It does not authorize blocking unrelated inbound manual work. |
| Public advertiser/creator intake | Active Bliss Bot project `bkutbglzivfdnoerigyb`; deployed `alpha-public-intake` is ACTIVE version 17, `verify_jwt=false`, and its source explicitly allows notice `AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1`. Live `alpha_public_intake_requests` has the matching structured columns and notice constraint. The Edge Function validates and persists budget amount/minor units/currency, market, objective, media, audience requirements, languages, separate contact consent, and notice version. | No factory STOP dependency exists in intake. The live form/backend contract is aligned; no new migration or deployment is required based on this inspection. No inbound request itself is a campaign, order, or payment. |
| Existing inquiries / contact | Privacy-safe live aggregate: 5 requests, including 3 advertisers and 2 creators, all on the 2026-11-05 GHL notice. All 5 have `contact_consent=false`, `followup_gate=HOLD`, and identity/authority/suppression checks false. Their old marketing setting is not contact permission. | Keep all five on hold. Do not backfill consent or contact them. New follow-up still requires one-time inquiry-contact permission plus identity, authority, suppression, and explicit approval. |
| CRM delivery | The live `alpha-public-intake` bridge checks current notice and recorded contact consent, then requires cleared follow-up gate, verified business identity, verified decision authority, CRM suppression check, and `followup_approved_at`. CRM writes also require explicit enablement, credentials, and complete unique field-ID configuration. | Preserve these specific checks. They protect contact creation and are not factory research gates. No CRM contact was created by this reassessment. |
| Ask Alpha AI | `alpha_ask_alpha_settings` has `enabled=false` and `externally_billed_usd=0`; no turn rows exist. The AI endpoint uses its own setting, budget reservation, rate limits, and consent. Guided public content and the human intake route remain distinct. | Do not turn on paid AI for this $0-spend task. Its off switch does not block a human consultation or internal planning. |
| Marketplace matching / quoting | Live marketplace aggregate: 8 creators, only 2 with audience size, primary geography, and language all populated; 8 inventory slots have an availability flag, but the 6 placements are only 2 `PLANNED` and 4 `CREATED`. The quote engine separately requires current verified audience/geography/availability evidence, price and tax evidence, accepted creator compensation/agreement, fulfillment and payment costs, and a passing margin policy. | No autonomous marketplace activity is enabled. Do not promise a placement or send an evidence-backed placement quote from these rows. A human may prepare an internal, nonbinding planning brief without relying on this inventory. |
| Payment / Stripe | Existing repository evidence states the Stripe lifecycle is mocked, its test acceptance plan was not executed, public checkout is not active, and proposed paid-service/refund terms remain internal drafts with missing owner/legal/finance values. No connected Stripe account tool or merchant-state evidence is available in this environment. The previously documented public inquiry forms are not a confirmed monitored post-purchase support channel. | Payment remains blocked independently of factory STOP until actual merchant readiness and the transaction-specific agreement, refund, tax, support, currency, and fulfillment controls are verified. No charge was attempted. |
| Outreach / suppliers | No authorization or evidence is present for unsolicited outreach, unapproved supplier calls, creator commitments, or external contact to the five legacy requests. | Keep these blocked by consent, suppression, verified scope, and transaction-specific approval—not by the factory research STOP. |

## What changed in this PR

- Removed the documentation-level blanket statement that the global factory STOP prohibits all marketplace/commercial operations.
- Defined the allowable factory-independent path: receive a permitted inbound inquiry; perform human review and internal campaign-planning/quote preparation; contact the requester only after individual consent, identity, authority, suppression, and approval checks; send a quote only when all scope, delivery, cost, currency, and terms evidence exists.
- Recorded that no live STOP value was changed and no factory gate was bypassed.
- Reconciled prior release notes with current live evidence: the versioned intake schema and matching Edge Function are already active, while old requests remain held.
- Kept payment, outreach, supplier-call, and placement-commitment safeguards independent and active.

## Verified operational result and limits

The advertiser form/backend can accept the new structured contract without the factory being cleared. Human review and internal planning can proceed from a new, permissioned inbound brief without autonomous creator discovery. The three legacy advertiser requests are still held. No current evidence supports a live placement quote, CRM delivery, payment, or first collected revenue. This reassessment created no database rows, CRM contacts, customer communications, transactions, or spend.
