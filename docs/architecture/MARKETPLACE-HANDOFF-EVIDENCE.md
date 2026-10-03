# Marketplace handoff

The priority contract after the local batch measurement. One qualified
advertiser and one stored creator are handed to
`DeterministicRuleEvaluator`. The handoff does not open a match
certificate. This is not a win. Green does not send. Delivery stays
`NOT_SENT`.

## What works

- A score of 100 and a public source qualify the advertiser.
- A preserved advertiser does not call the evaluator.
- The status and score on the row are the evaluator's result.
- A stored country name is not converted into a code.
- The same tenant, creator, and source do not write a second row.
- A reading for one advertiser does not include another advertiser's row.
- The existing match certificates are left unchanged.

`example.com` fixtures and the seeded creators are not claims about
real businesses or people.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **396 passed, 0 failed, 0 skipped**.

The new proofs are `MarketplaceHandoffTests` and
`MarketplaceHandoffApiTests`.

## Browser path

`/operations#/handoff` reads one advertiser at a time.

The contract is `docs/architecture/contracts/MARKETPLACE-HANDOFF-CONTRACT.md`.
