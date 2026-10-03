# Marketplace metrics

The contract after the coverage week. One marketplace reading stores
the measured advertiser count, the measured creator count, and the
stored slot count. A revenue amount is not on file. This is not a
census. The balance page is unchanged. Green does not send. Delivery
stays `NOT_SENT`.

## What works

- The reading is taken from the stored advertisers, creators, and slots.
- The same reading key does not write a second row.
- A request to record a revenue amount is refused.
- A request to add a slot is refused.
- A stored Economics row is counted and its amount is not copied.
- Census stays false. Revenue recorded stays false. Slots stay unchanged.

The balance page still says revenue is not recorded and inventory is
not recorded. Those sentences are left in place.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **435 passed, 0 failed, 0 skipped**.

The new proofs are `MarketplaceMetricsTests` and `MarketplaceMetricsApiTests`.

## Browser path

`/operations#/metrics` reads the stored advertisers, creators, and slots.

The contract is `docs/architecture/contracts/MARKETPLACE-METRICS-CONTRACT.md`.
