# Marketplace balance

Phase 27 of the acquisition tracker. Advertiser pressure and creator
pressure are counts of stored rows. A missing count stays unrecorded
and is not treated as zero. A blank stored name is skipped. Revenue is
not recorded. Inventory is not recorded. The reading claims no market
census. Green does not send. Delivery stays `NOT_SENT`.

This reading does not choose a creative pair and it does not send.

## What works

- Two stored advertisers and one stored creator report advertiser
  pressure ahead of creator pressure.
- Equal stored counts are level.
- More stored creators report creator pressure ahead.
- A missing advertiser count stays unrecorded. It is not treated as zero.
- A blank stored name is skipped. None is invented.
- Revenue and inventory stay unrecorded.
- The reading does not add a prospect.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **328 passed, 0 failed, 0 skipped**.

The new proofs are `MarketplaceBalanceTests` and `MarketplaceBalanceApiTests`.

## Browser path

`/acquisition/balance.html` reads the stored advertisers and creators.
The live rows are the four stored advertisers and the three stored
creators. Advertiser pressure is ahead of creator pressure. Revenue is
not recorded. Inventory is not recorded. The stored prospects remain.
Delivery remains `NOT_SENT`.

The recording is `marketplace_balance_stored_rows_only.mp4`.
The report is `docs/architecture/evidence/Alpha-Marketplace-Balance-Report.pdf`.
