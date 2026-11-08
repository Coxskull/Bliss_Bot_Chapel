# ALPHA-346 — Combined consent-first first-customer release candidate (November 9, 2026)

**DRAFT SOURCE ONLY — no deployment, customer outreach, payment, production mutation, new spend or STOP change.**

## Canonical integration sources
- Base: [PR #356](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/356), source commit `7af49369c1dc7362404623067f6659bbc69f403c` (all 9 exact-head CI workflows PASS).
- Website, manual service, offline proposal and signoff files: [PR #343](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/343), source commit `bc375237fc9d1b67602e669b93418d09964dfb6f` (9 exact-head workflows PASS).
- **Conflict explicitly resolved:** `frontend/public/join-advertiser.html` uses PR #356's validated market/budget/language/form/consent schema and aligns the introductory planning service copy with #343's *five* deliverables. The older #343 form blob must NOT replace this file.
- New integration regression checks both source and generated public portal against explicit contact consent, separate optional marketing, current notice, server-side budget validation, safe public route, private proposal exclusion and no checkout.
- A historical note in earlier 2026-11-07 source docs is not evidence that the current public release has been approved or that previously collected records are contact-consented.

## Mandatory release sequence
1. Review exact combined PR head; all CI including .NET, public portal build, CRM consent, integration regressions must pass. Check domain/page preview without real submissions.
2. Verify the public form's deployed notice version equals active Supabase `alpha-public-intake` version. New notice `AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1`; no retroactive consent for historic intake rows.
3. Independent legal/finance owner signoff for current support mailbox monitoring, merchant identity, paid-service terms/privacy/refund, service scope/currency/taxes/fulfillment, separately agreed customer order.
4. Stripe account live-payment eligibility and invoice/payment-link capability verified by authorized owner/admin. A draft capability check is not authorization to create/send a charge.
5. Only after a separate explicit release decision, merge **one** reconciled branch and deploy through the existing Vercel project. Do not independently merge #343 and #356. Do not enable factory or private marketplace identity features as a side effect.

## Acceptance
- Automated tests: pending exact-head combined CI at creation.
- Hosted public GET and responsive/consent review: pending preview verification.
- Live payment, payout, staff response coverage, contract acceptance: **NOT VERIFIED / NOT AUTHORIZED**.
- Incremental spend: **$0**; existing production untouched.
