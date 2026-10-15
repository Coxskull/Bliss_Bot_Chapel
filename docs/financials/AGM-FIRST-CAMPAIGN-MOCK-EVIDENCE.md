# AGM-LAUNCH-010 — first fictional campaign ledger integration

**Scope:** controlled, isolated PostgreSQL mock only. No real Stripe test-mode call
has been made, and no payment provider authorization, paid campaign, or payout
authority is implied.

## Reused, not rebuilt

- Accepted-current-version USD Economics quote in `BlissDbContext`.
- Authenticated advertiser preparation via `POST /api/financials/payment-operations`.
- Immutable `financials.payment_operations`, server-registered
  `FundingBinding`, unique idempotency, mock-only dispatch binding.
- Server-profile-bound synthetic HMAC-signed `payment_intent.succeeded` event
  verification, exactly one capture and balanced PostgreSQL journal.
- Role-scoped Mission Control mock financial review with no payout/hold release.

## New read-only operator evidence

`GET /api/financials/mock-review/cases/{operationId}/campaign-evidence`

Returns the exact server-frozen campaign, quote version, USD minor-unit amount,
observed mock capture journal ID and ordered debit/credit ledger lines, plus
a correlated persisted financial-evidence SHA-256. Money values are serialized
as decimal **strings**, not JavaScript numbers. The server verifies the mock
journal debit and credit totals against the quote, or refuses an incomplete
captured journal. It does not mutate money or contact anyone.

Access requires authenticated `bliss.operator` or `bliss.admin`, an exact
server-configured MockReviewerScope and FundingBinding, `Mode=Mock`,
and the named `127.0.0.1/financial_test` fixture. An unrelated operator,
advertiser or forged operation ID is refused.

The existing Mission Control financial-review UI includes a separate lookup
by persisted operation UUID, displays original/test ledger lines, and
**always** marks `paymentCompleted=false`, `providerCalled=false`,
`alphaCommissionStatus=NO_APPROVED_ALLOCATION`,
`creatorCompensationStatus=NO_APPROVED_ALLOCATION`,
`creatorPayoutAuthorized=false` and
`campaignDeliveryAuthorized=false`. These statuses are essential:
the existing two-line `TEST_CASH` / `TEST_RECIPIENT` capture ledger
is **not** a commission-split calculation or creator payable.

## CI demo evidence

The existing `FinancialBoundMockApiTests` loops across Bliss Bot, Creative
Academy, and Wedding Planner with synthetic accepted quotes, isolated
PostgreSQL, distinct registered test profile and approved/denied roles.
For each fictional advertiser operation it now proves:

1. Pre-capture Mission Control case shows exact amount and **no journal**;
   unqualified advertiser cannot read operator-only case.
2. Invalid signatures/metadata, revoked quote, and wrong authenticated actor
   cannot book a mock capture.
3. Eight concurrent identical signed callbacks produce **one** immutable
   capture/journal, and a second event cannot create a new journal.
4. Mission Control reads the exact campaign/source amount and two balanced
   **TEST-only** lines, with no claimed commission, payable, provider call,
   live payment, or campaign activation.
5. Existing refund and reconciliation tests still reject excess refunds.

The dedicated financial/gateway PostgreSQL Actions workflow
`.github/workflows/isolated-postgres-regressions.yml` executes the real local
database fixture and records TRX/log evidence. Source/CI SHA and run URLs are
recorded in the PR after execution.

## Still blocking a real commercial demonstration

Owner decisions: legal collecting merchant, creator and ALPHA payees, accepted
fee/split rules, legitimate recipient identity, money movement and refund
policy. Separately: rotated funded Stripe **test** account key in an approved
secret store, distinct signed webhook endpoint and account-level provider
receipt reconciliation. Real provider `sk_test` integration is not wired by
this change. A genuine test payment must be observed before it can be claimed.
For the first live creative, obtain original-generation and human visual QA
approval; these mock transaction tests do not authorize outreach, delivery,
publisher inventory or payout.
