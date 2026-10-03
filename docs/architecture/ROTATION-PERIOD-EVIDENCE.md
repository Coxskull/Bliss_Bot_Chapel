# Later rotation period

The priority contract after the research ledger. One later period
stores the measured open slots and the creator decision. A theoretical
slot is not filled. No revenue row is on file. This is not a census.
Stored slots are not rewritten. Economics remains the only price
authority. Green does not send. Delivery stays `NOT_SENT`.

## What works

- The period is read from the stored advertisers and the stored slots.
- The same period key does not write a second row.
- A withheld creator decision is a separate row and keeps the open count.
- A request to fill an open slot is refused.
- A revenue amount is refused.
- Census stays false. The slot count is unchanged.

Stored advertiser names are the rows already on file. They are not a
census and they are not invented businesses.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **413 passed, 0 failed, 0 skipped**.

The new proofs are `RotationPeriodTests` and `RotationPeriodApiTests`.

## Browser path

`/operations#/period` reads the stored advertisers and the stored slots.

The contract is `docs/architecture/contracts/ROTATION-PERIOD-CONTRACT.md`.
