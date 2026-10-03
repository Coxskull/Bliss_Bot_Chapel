# Subscription ledger and factory budget

The first priority contract after Phases 4–29. The subscription register
and the four factory ceilings live on the existing operations console.
A missing cost stays unrecorded. A reached ceiling degrades only that
scope. A review is not a purchase. Bliss hosted acceptance and Economics
Phase 9 stay on their own paths. Green does not send. Delivery stays
`NOT_SENT`.

This contract does not invent a price, plan, owner, or renewal date.

## What works

- Ten known services are stored. PostgreSQL has no classification and no cost until an operator supplies them.
- A partial cost is refused. None is invented.
- An incomplete paid proposal writes no audit and adds no row.
- A complete capability-gap review stays `NOT_PROPOSED`.
- A rejected duplicate is not bought.
- Daily, monthly, provider, and prospect scopes start without a ceiling.
- Spend at a supplied ceiling degrades that scope. The other scopes stay open.
- A recorded cost is the operator's amount. It is not an Economics price.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **363 passed, 0 failed, 0 skipped**.

The new proofs are `SubscriptionRegisterTests`, `FactoryBudgetTests`, and `SubscriptionLedgerApiTests`.

## Browser path

`/operations#/ledger` reads the stored register. Costs stay unrecorded.
Recording a daily ceiling of 25 USD with recorded spend of 25 USD
degrades the daily scope and leaves the monthly scope open. An
incomplete enrichment proposal is refused. Delivery remains `NOT_SENT`.

The recording is `ledger_unrecorded_cost_then_one_scope_degrades.mp4`.
The report is `docs/architecture/evidence/Alpha-Subscription-Ledger-Report.pdf`.
The contract is `docs/architecture/contracts/SUBSCRIPTION-LEDGER-CONTRACT.md`.
