# ALPHA-CODEX-INDEPENDENT-REPAIR-470

2026-11-08. Source: PR #356 head `1bc57289a3746c5fcef0fd18e58e4150eb661c3b`.
Isolated branch: `codex/independent-repair-470`. No changes to Erwin's branch,
production, databases, factory STOP, outreach, paid policies or payment activation.

## Implemented repairs

- Reject unsupported advertiser markets before inventory evaluation, including
  inherited object-property names. Previously an unknown market bypassed the
  market/currency consistency check.
- Reject placement quotes whose summed costs exceed exact integer range even
  when individual costs are valid. Previously an unsafe total could be eligible.
- Calculate planning and placement contribution basis points with exact integer
  multiplication and floor division. A valid total of 9007199254740991 minor units
  and contribution of 99979911727625 previously rounded to 111 basis points,
  incorrectly passing a 111-basis-point minimum; the correct value is 110.

No prices, fees, tax assumptions, paid terms or currency mappings were invented.
Planning quotes remain DRAFT and require existing scope, capacity and cost evidence.

## Verification

Node v24.22.1, offline, synthetic data only:

- `node scripts/agm-quote-boundaries.test.mjs`: 4/4 pass. Covers exact decimal
  boundary, unknown markets, summed-cost overflow and false margin acceptance.
- `node scripts/agm-marketplace-budget-smoke.mjs`: PASS, existing synthetic
  consented intake, stub CRM, pending opportunity, planning quote and inventory flow.
- `node scripts/agm-ghl-unit-smoke.mjs`: PASS consent, mapping, disabled gate,
  delivery and error contracts.
- `BLISS_APPLICATION_ORIGIN=https://synthetic.example.invalid node scripts/build-vercel-portal.mjs`
  then `node scripts/stripe-public-link-smoke.mjs`: PASS, 11 public pages,
  no checkout claim and no draft-policy exposure. Synthetic origin is local only.
  Initial build without the required origin correctly refused; supplied the
  synthetic origin and reran successfully.
- `node --test bliss-frontend/src/*.test.js`: 4 test files PASS.
- `git diff --check`: PASS.
- C# payment tests not run: `dotnet` is not installed. No claim of full backend,
  hosted browser, real CRM, merchant-account or Stripe sandbox acceptance.

## Focused diagnosis and remaining launch blockers

Intake: structured budget/media/language and independent contact consent are
validated in `supabase/functions/alpha-public-intake`; existing CRM unit tests
pass. This is local contract evidence, not proof of hosted receipt or CRM
permission to contact existing requests. Existing holds remain intact.

Planning quotation: `calculatePlanningServiceQuote` supports PLANNING_ONLY
without creator inventory, provided verified requirements, scope, human capacity,
terms, labor, delivery, processing, tax, budget and margin policy exist. The result
is a draft, not an accepted order or payment authority. Selling a scoped planning
service is the shorter route than promising currently unverified placements.

Checkout: no Checkout Session creation integration was found in application code.
`StripeSandboxClient` creates USD test PaymentIntents only; it is not customer
Checkout and cannot consume the six local-market currencies directly. Do not
convert a local quote to USD by relabeling currency or bypass test-only guards.
`StripePaymentDispatcher` explicitly is not wired into runtime.

Verification: `StripeSandboxVerifier` checks raw-body HMAC, timestamp, sandbox
mode, expected intent, amount, currency and merchant/campaign/module bindings;
mock reconciliation has receipt/replay storage. It rejects live/Connect events.
These are useful existing primitives, not a deployed live webhook or evidence
of paid orders. A redirect or client success message must never mark an order paid.

## Owner actions and shortest path to first legitimate payment

1. Approve ONE planning-only order: actual customer requirements, outputs,
   accountable delivery owner/date, price/currency, complete costs/tax and margin.
   Record customer acceptance and preserve identity/authority/contact controls.
2. Resolve the existing `docs/stripe/CAPTAIN-APPROVAL-PACKET-20261106.md`:
   merchant legal identity and Stripe country, monitored paid-order support,
   delivery/revisions, refund/cancellation/dispute terms, paid privacy/terms and
   approved publication versions. Do not substitute public inquiry terms.
3. Owner checks Stripe Dashboard account eligibility, outstanding verification
   requirements and charges capability; supplies an isolated test-mode account
   and webhook secret through secure environment settings, never chat/repository.
   No connected Stripe account evidence was available in this session.
4. Reuse the existing merchant infrastructure: an owner-created order-specific
   hosted Stripe invoice/payment link may avoid building public Checkout first,
   if approved for that merchant, service and currency. Otherwise implement
   authenticated server-side Checkout from an immutable accepted quote, with
   idempotency, quote/customer/currency/amount binding and approved policy links.
   Neither path is activated or represented as available by this repair.
5. In isolated Stripe test mode prove paid, declined, canceled, pending/delayed,
   wrong amount/currency/order, bad/stale signature and duplicate/out-of-order
   callbacks. Persist authoritative payment evidence once; no paid status from
   a return URL. Run .NET and hosted integration tests in the existing CI/runtime.
6. Obtain separate production approval, then merge/deploy approved repairs and
   policies. An authorized human can collect payment for the accepted order,
   reconcile authoritative Stripe evidence and fulfill the scoped planning work.
   This directive authorizes no live charge, deployment, merge or outreach.

## GitHub access

Connected identity: alpha7auto. Repository API reported pull/push/triage true,
admin/maintain false. Remote isolated branch creation succeeded. Local clone and
push dry-run succeeded with network permission; initial sandbox proxy refusal was
an execution networking issue, not a GitHub repository permission denial.
No account-level permission blocker was observed at these stages. No owner
permission change is justified solely by the initial proxy failure.
