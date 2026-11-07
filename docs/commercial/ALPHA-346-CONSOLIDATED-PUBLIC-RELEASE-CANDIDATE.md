# ALPHA-346 — Consolidated public inquiry-only release candidate

**Date:** 2026-11-07 (Philippines)  
**Source:** PR #342 + selected public-facing improvements from PR #341 and PR #340  
**Decision:** SOURCE-ONLY REVIEW CANDIDATE; NOT A PAID-SERVICE RELEASE  
**Factory STOP:** ENGAGED | **Payments:** DISABLED | **New spending:** USD 0

## One nonconflicting website experience

- **Homepage** — from Task #19 PR #341: visible same-origin advertiser and creator public inquiry links, accessible keyboard focus, mobile 48px action targets, the existing informational Ask Alpha guide and notice/terms links. Public entry remains interest-only.
- **Advertiser form** — from PR #340, adjusted to the canonical PR #342 scope: headline and campaign-goal field clarify a manual planning inquiry; both required consent controls and optional marketing consent remain unchanged; no creator booking, fixed price, delivery deadline, or fixed revision count is offered.
- **Service** — from PR #342: one *Campaign Planning & Coordination* planning-only service with five proposed deliverables: discovery brief, creator/channel suitability plan, creative direction outline, coordination schedule and final planning summary. Precise revisions, rights, milestones, taxes and customer-specific terms require separate written approvals.
- **Contact** — from PR #342: `dejesuserwin1@gmail.com` is an operator-selected inquiry address verified with a noncustomer two-way test. No paid-customer support guarantee or response SLA is claimed.
- **Internal operating material** — from PR #342: private proposal desk, internal quotation reference USD $50–$1,000, safety validator, and draft business-approval checklists. These tools are not included in the public build.

## Explicit conflict decisions

- PR #340's alternate *four-output plus one revision* fixed scope is **not imported**; it contradicts the five-deliverable service chosen for this candidate. Its unrelated internal templates, draft paid legal policies and payment/production preparations remain separate, unapproved work rather than silently becoming public terms.
- PR #341's public CTA, CSS, generated-portal smoke check and public artifact CI workflow are retained; creator/advertiser dashboards remain protected, outside the public build.
- PR #342's modifications to the unrelated container vulnerability audit scripts were reverted in this candidate to match `main`, because they are being addressed in another workstream.
- No changes to public-intake Edge Function, Supabase schema, GoHighLevel, Stripe, Railway factory or STOP controls.

## Acceptance procedure (before publishing)

1. Run the existing ALPHA-346 offline proposal tests and **build the actual Vercel public portal** with a synthetic HTTPS application origin. The build runs Task #19's static assertions.
2. Run `scripts/alpha346-generated-public-portal.test.mjs` against generated output: verify five canonical outputs consistently, CTA routes, consent, privacy, support caveat, and exclusion of internal/private tools.
3. Run the existing COM-261 public-form CI workflow. Its generated public artifact is suitable for offline release review; it is **not** evidence of live submission or production deployment.
4. Review the exact candidate SHA and changes before a production merge; do not activate public paid terms, real Stripe payments, automatic customer outreach or advertising placement.
5. Only after a controlled public release, perform anonymous HTTP read-only smoke checks and mobile/keyboard browser QA against the real domain. Do not make a production POST using invented customer identities.

## Still-required independent commercial decisions

- Named human support owner, monitoring/complaint escalation and service availability.
- Merchant/legal identity and allowed market, final paid commercial terms, refunds/cancellations, paid-service privacy and data handling.
- Approved customer-specific price/currency/tax and timeline, written customer acceptance, and separate authorization for any payment.
- Relevant production security and recovery review for an actual paid order; Task #1 full disaster recovery is not falsely marked complete.

**This candidate is a source-code review unit, not a guarantee that a commercial transaction is already possible.**
