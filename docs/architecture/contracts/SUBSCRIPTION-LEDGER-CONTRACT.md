# Engineering Contract: Subscription ledger and factory budget

**Status:** the one priority contract authorized after the amendment queue.
It does not open the next catalog item. It does not finish Bliss hosted
acceptance or Economics Phase 9 owner acceptance.

## Objective

Persist the subscription register that `SUBSCRIPTION-LEDGER.md` already
keeps by hand, and record daily, monthly, provider, and prospect factory
ceilings on the existing operations console.

## AI capability

None. Classifying a stored row, refusing an incomplete review, and
comparing an operator-supplied spend with an operator-supplied ceiling
are ordinary software.

## Deterministic capability

The register, the capability-gap review, and the four budget scopes.

## Existing reusable assets

`SUBSCRIPTION-LEDGER.md` remains the classification source. Lane tempo
remains the per-lane pause control. This contract does not replace it.
PostgreSQL through `BlissDbContext` is the system of record.

## Required provider

None.

## Subscription impact

`FREE_SELF_HOSTED`. No new row is required. No new subscription is
purchased.

## Build-versus-buy review

Existing Alpha infrastructure already names the services. .NET and
PostgreSQL can store the rows. An existing subscription is not required.
A free or usage-based gap-fill is not required. A new recurring
subscription is not required.

## Estimated usage cost

Zero model calls. Compute cost stays `UNRECORDED`. None is invented.

## Maximum cost

An operator may record a ceiling. A missing ceiling stays unrecorded.
A reached ceiling degrades only that scope.

## Inputs

The ten known services. An operator-supplied cost with a billing source.
A capability-gap proposal. A factory ceiling with an optional recorded
spend and a reason.

## Outputs

`SubscriptionRegisterRows`, `SubscriptionLedgerAudits`,
`FactoryBudgetStates`, and `FactoryBudgetAudits`. The reading at
`/operations#/ledger` and `GET /api/operations/ledger`.

## Cache policy

The known services are seeded when missing. An operator-supplied cost is
kept. It is not refreshed from a vendor. A bill that is not supplied
stays unrecorded.

## Failure handling

A missing register row, a partial price, an unknown service, an
incomplete paid review, a partial ceiling, and a negative spend are
refused. Nothing is purchased. Delivery remains `NOT_SENT`.

## Authorization

The reading is available on the operations console. A write uses the
existing operations write policy. The software cannot buy a
subscription, set a price, or send.

## Persistence

PostgreSQL tables on `BlissDbContext`. n8n is not the system of record.

## Tests

`SubscriptionRegisterTests`, `FactoryBudgetTests`, and
`SubscriptionLedgerApiTests`.

## Evidence

`docs/architecture/LEDGER-BUDGET-EVIDENCE.md` and the PDF report beside it.

## Acceptance criteria

- The ten known services are stored.
- A missing cost and a missing classification stay unrecorded.
- A recorded cost is the amount the operator supplied and is not an
  Economics price.
- An incomplete `NEW_PAID_SUBSCRIPTION` review is refused and writes no
  audit.
- A complete review stays `NOT_PROPOSED` and does not add a paid row.
- A rejected duplicate is not bought.
- The four scopes start without a ceiling.
- Spend at the ceiling degrades that scope and leaves the others open.
- Green does not send. Delivery remains `NOT_SENT`.

## Out of scope

Bliss hosted acceptance. Economics Phase 9 owner acceptance. Fishing
Fleet discovery. Decision-maker enrichment. Live send. Crawlers. A new
subscription purchase. A second CRM, workflow engine, or messaging
system. Rotation periods. Wedding Planner phases. Inventing a price,
plan, owner, or renewal date.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. This contract is software.

Can we reuse something? Yes. The markdown register, PostgreSQL, and the
operations console.

Do we already pay for this capability? The application is
`FREE_SELF_HOSTED`. Hosted database cost stays unrecorded.

Do we actually need a new subscription? No.
