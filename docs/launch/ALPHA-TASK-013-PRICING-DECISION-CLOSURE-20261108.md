# ALPHA GLOBAL MEDIA LLC — Task #13 Pricing Decision Record

**Directive:** ALPHA-TASK-013-PRICING-DECISION-CLOSURE-397  
**Prepared:** 2026-11-08  
**Status:** OWNER / FINANCE / LEGAL APPROVAL REQUIRED — NOT EFFECTIVE  
**Location:** Existing ALPHA-346 draft PR #340; internal review document only  
**Global STOP:** ENGAGED · **Additional spend:** $0 · **No production outreach, charge, payout, or publication authorized**

This record consolidates the remaining Task #13 commercial decisions. It reuses PR #340 and the existing COM-282 quote worksheet. It does not approve a quote, commission, creator contract, live payment, payout, or public price.

## 1. Confirmed boundaries and proposed presentation

| Item | Current state |
| --- | --- |
| Manual planning service | One-Campaign Planning & Coordination; four planning documents plus one consolidated in-scope revision |
| Internal quote band | **Owner-approved USD $50–$1,000 inclusive** for this planning-only service; not an exact customer price |
| Proposed scope descriptions | **Focused**, **Comparative**, and **Coordination-heavy**; descriptions only, not fixed-price tiers |
| Published prices | None approved; do not publish tier prices |
| Manual planning revenue | Separate Alpha service revenue; no creator booking, creator payment, media spend, placement, or marketplace commission |
| Marketplace compensation | Separate future track; COM-282 requires one method only: itemized Alpha fee OR percentage of creator fee |
| Creator compensation | Must be a campaign-specific written amount/contract; no amount or rate approved |

**Recommended public wording after separate release approval:** “Custom quote in USD after scope review.” Keep the three scope descriptions informational. State that final price, taxes, timing, cancellation/refund terms, deliverables, and support will be in the accepted Order.

## 2. Stripe and worksheet reconciliation

### Actual configuration readback

- PR #340’s `scripts/alpha346-stripe-preflight.mjs` explicitly identifies itself as **source-only/offline**: no Stripe API, network, payment, secret, customer-data, or STOP access. It reports actual `charges_enabled` and `payouts_enabled` as **not independently verified**.
- The owner previously reported Stripe Dashboard USA/USD, “Verified,” and live charges/payouts enabled. These remain owner-reported statuses; they do not establish a marketplace Connect integration, payment-method fee schedule, settlement flow, country-specific creator eligibility, or completed payment test.
- COM-282 is marked internal-only and no-sale. It has no payment/notification integration. Its current formula is a draft quote total: for percentage compensation, creator fee × (1 + commission) + approved costs/taxes; it blocks missing inputs and simultaneous itemized/percentage fees. It does **not** calculate processor fees, Connect account/payout/routing fees, payout FX, labor/support, refund reserves, or tax obligations. Current worksheet: **0/14 evidence-backed approvals; NOT_READY_NO_SEND; DISABLED_NO_SEND_NO_CHARGE**.
- Therefore, no “actual integration configuration” exists in the reviewed PR #340/COM-282 implementation to reconcile to. The figures below are published-price scenarios only; Finance must confirm the real account schedule, payment flow, and platform pricing model before using them.

### Published Stripe pricing inputs (U.S. standard pricing; verify at order approval)

- U.S. domestic online card: **2.9% + $0.30 per successful payment**.
- International card: published additional **1.5%**; currency conversion, when required, published additional **1%**.
- Connect when the platform handles connected-account pricing: **$2 per monthly active account** (a month in which payout is sent) plus **0.25% + $0.25 per payout**. Stripe separately lists **0.25% of payout volume** for funds routing/platform management; confirm whether the chosen flow incurs it.
- Cross-border connected payouts: **starting at 0.25% of payout volume**, subject to country and funds-flow support.
- Stripe states original payment-processing, Connect, and currency-conversion fees are not returned on refunds. Posted U.S. standard dispute-received fee is **$15 per dispute**.
- If Stripe bills connected accounts directly for payment fees, the marketplace must disclose how those charges reduce creator proceeds. The scenarios below assume Alpha pays payment and payout costs from its own fee so the contracted creator amount is protected.

Sources, checked 2026-11-08:
- Stripe U.S. pricing: https://stripe.com/pricing?locale=en-US
- Stripe Connect pricing: https://stripe.com/connect/pricing
- Stripe international connected-account payouts: https://support.stripe.com/questions/pay-out-to-connected-accounts-in-other-countries
- Stripe refunded payments: https://support.stripe.com/questions/understanding-fees-for-refunded-payments

## 3. Consolidated calculation schedule

### A. Manual planning-only quote examples

Illustrative domestic U.S. online-card calculations; no tax, international-card premium, conversion, labor, support, or other costs included. These are not proposed fixed prices.

| Exact custom quote if later approved | Domestic-card processing estimate | Amount after processing, before all other costs |
| ---: | ---: | ---: |
| $50.00 | $1.75 | $48.25 |
| $250.00 | $7.55 | $242.45 |
| $1,000.00 | $29.30 | $970.70 |

Refund exposure if a full refund is issued after capture: the original $1.75 / $7.55 / $29.30 processing fee remains an Alpha cost under Stripe’s published standard pricing. The proposed full refund before work starts and itemized post-start remedy remain unapproved.

### B. Marketplace commission sensitivity

Assumptions: one $100 contracted creator fee; domestic U.S. card; no taxes or third-party costs; one monthly active creator with one payout; Alpha bears payment/Connect costs; creator receives the full $100. Commission rates are hypothetical only. Connect figures exclude the separately listed routing fee unless shown.

| Hypothetical commission on creator fee | Alpha fee | Advertiser subtotal before tax | Card processing | Connect monthly-active + payout base | Alpha residual before labor, support, taxes, refunds, and overhead |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 10% | $10.00 | $110.00 | $3.49 | $2.50 | $4.01 |
| 15% | $15.00 | $115.00 | $3.64 | $2.50 | $8.86 |
| 20% | $20.00 | $120.00 | $3.78 | $2.50 | $13.72 |

Sensitivity adjustments at $100 payout:
- If Stripe’s separately listed funds-routing fee applies, subtract **$0.25** from each residual.
- If the payout is cross-border, subtract at least another **$0.25** at the published starting rate; confirm the actual country, payout rail, FX and fee schedule.
- For multiple campaigns to one creator in a month, allocate the $2 monthly active-account fee across that creator’s payouts rather than charging it as a new $2 cost for every campaign.
- If the customer card is international or FX conversion is required, recalculate the payment fee on the complete amount charged, including taxable amounts as applicable.
- If a full refund follows capture, the original payment-processing and Connect fees remain unrecovered under Stripe’s published terms. A dispute may add the posted $15 dispute-received fee. A payout already sent may create additional recovery exposure.

## 4. Finance recommendation

**Recommendation: approve no commission rate as “sustainable” yet.** The existing evidence contains no measured campaign labor/support cost, creator quote, tax treatment, dispute/refund experience, or selected Stripe Connect flow. At the $100 creator-fee example, even the 20% scenario leaves only $13.72 before these costs.

Use the COM-282 single-fee model and retain the creator’s signed compensation amount in full. Alpha should absorb payment and payout costs from its disclosed fee unless a different, lawful, creator-approved allocation is explicitly adopted. Do not add an undisclosed card surcharge or collect a second Alpha fee.

Finance can calculate a minimum candidate rate after entering real inputs:

`r_min = [p(C + X) + f + K + O + R + M] / [C(1-p)]`

where:
- `C` = signed creator compensation;
- `p` and `f` = applicable percentage and fixed payment costs;
- `X` = taxes and approved pass-through items included in the card charge;
- `K` = actual Connect, payout, routing, FX and cross-border costs;
- `O` = measured human labor, support, payment operations and allocated overhead;
- `R` = expected unrecovered refund/dispute loss;
- `M` = required pre-income-tax contribution; and
- `r_min` = minimum commission as a fraction of `C`.

Finance must add the tax adviser’s determination for transaction taxes, Alpha income taxes, creator reporting/withholding, and any taxes on fees. Do not set `X` or `O` to zero without evidence.

**Decision request:** finance to approve a costing method and supply real inputs; then recommend a percentage only if the resulting margin meets the approved minimum at realistic transaction sizes. Until then, keep 10% / 15% / 20% as sensitivity scenarios only.

## 5. Legal decision checklist — draft, not legal advice

- [ ] Confirm legal seller / merchant, contract entity, notice address, governing terms, and approved customer/creator countries.
- [ ] Approve separate advertiser and creator commercial terms; define whether creator compensation is a fixed negotiated amount or another expressly agreed basis.
- [ ] Specify creator IP license: exact asset/content, channels, territory, duration, paid-media/organic use, editing, exclusivity, attribution, sublicensing, and revocation/termination.
- [ ] Require advertiser substantiation and approval of product/regulated claims; define sponsored-content disclosures and each party’s responsibilities.
- [ ] Set cancellation before and after work/placement, completed-work allocation, creator cancellation/no-show, non-delivery, replacement, refund, tax reversal, and recovery rules.
- [ ] Define chargeback/dispute handling, evidence retention, escalation, who bears nonrefundable processor/Connect/FX fees, and limits on any creator deduction.
- [ ] Confirm each creator’s payout country, permitted account type, KYC/KYB, eligible payout rail/currency, transfer timing, FX, withholding/reporting, and processor approval. Stripe states cross-border payouts require supported countries and a supported funds flow; do not promise availability based on Alpha’s U.S. account alone.
- [ ] Confirm tax registration/collection/remittance and creator tax documentation with qualified advisers for each active jurisdiction.
- [ ] Approve published commercial privacy/retention terms, support contact and monitoring, before accepting paid orders.
- [ ] Ensure no customer-interest form or marketing consent is treated as paid-order, creator-rights, payout, or advertising consent.

## 6. Single owner-approval register

Each decision requires an approver, date, and evidence reference in the restricted company record. Blank fields mean **not approved**.

| Decision | Recommendation / allowed choices | Approver and evidence |
| --- | --- | --- |
| Manual planning scope and band | Confirm four planning documents + one revision; retain USD $50–$1,000 custom-quote band | Captain: ______ |
| Three planning descriptions | Retain Focused / Comparative / Coordination-heavy as descriptions only; no fixed tier prices | Captain: ______ |
| Manual-service tax and order terms | Approve exact quote-level tax, payment milestone, delivery, support, rights, cancellation/refunds | Finance + Legal: ______ |
| Marketplace fee basis | Select ONE: itemized Alpha fee OR percentage of creator fee | Captain + Finance: ______ |
| Commission rate | Leave blank pending evidenced costing; select only after `r_min` and minimum margin review | Captain + Finance: ______ |
| Creator compensation | Approve fee basis, amount source, payment timing, fee deductions (if any), and signed creator terms | Finance + Legal: ______ |
| Processor flow | Confirm Stripe product/account pricing model, charge type, actual fee allocation, payment methods, payout rail, refunds and dispute liability | Finance + Engineering: ______ |
| International payout scope | Name approved recipient countries and account/rail/currency combinations; otherwise exclude those markets | Finance + Legal: ______ |
| Effective legal policy versions | Approve and publish customer terms, creator terms, refund/dispute policy and commercial privacy notice | Legal + Captain: ______ |
| Operating cost and reserves | Enter labor, support, overhead, refund/dispute, tax and minimum-contribution inputs | Finance: ______ |

## 7. Remaining blockers and closure rule

1. Actual Stripe integration / charge flow / Connect pricing configuration is not present in the reviewed PR #340/COM-282 material; account Dashboard statuses are owner-reported, not independently verified.
2. No creator quote, selected compensation basis, approved commission rate, tax analysis, measured operational cost, support coverage, or final refund/dispute allocation is recorded.
3. The COM-282 worksheet remains **0/14 approvals, NOT_READY_NO_SEND, DISABLED_NO_SEND_NO_CHARGE**.
4. PR #340 remains a draft review branch; its source-only Stripe preflight does not verify account state or authorize payment.

**Task #13 status: OPEN — APPROVALS OUTSTANDING.** Close only after the original package/pricing/commission/creator-compensation criteria have evidence-backed approvals and this register is completed. Do not merge/deploy, publish prices, send outreach, activate marketplace payouts, issue a new charge, or change GLOBAL STOP under this record.
