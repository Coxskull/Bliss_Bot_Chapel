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

On this database the stored advertisers are Harbor Audio Labs, Sunrise
Wellness Co., TEST Dental Manila, and TEST Restaurant Santo Domingo.
The stored creators are Test Creator, Test Creator Brazil, and Unknown
Demographics Creator. Slots are 8. Revenue rows are 0. Advertiser
pressure is ahead of creator pressure. `metrics-reading-1` stores that
reading. Census is no. Revenue recorded is no. Slots changed is no.
The same key was not stored again. A request to record a revenue amount
was refused. The balance page still says revenue is not recorded and
inventory is not recorded. Delivery is `NOT_SENT`.

The recording is `marketplace_reading_leaves_revenue_unrecorded.mp4`.
The report is `docs/architecture/evidence/Alpha-Marketplace-Metrics-Report.pdf`.

The contract is `docs/architecture/contracts/MARKETPLACE-METRICS-CONTRACT.md`.
