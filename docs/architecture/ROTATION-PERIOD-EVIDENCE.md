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

On the live library there are 4 stored advertisers and 8 stored slots.
Harbor Audio Labs, Sunrise Wellness Co., TEST Dental Manila, and TEST
Restaurant Santo Domingo are the advertisers already on file. Pair 6
stores `later-period-1` as theoretical 6, placed 4, open 2, stored
slots 8, creator Approved, census no, slots changed no. Revenue says
no row is on file. The same key was not stored again, and the open
slots were not filled. `later-period-withheld` keeps open 2 and leaves
the approved row Approved. Advertisers stayed 4. Slots stayed 8.
Delivery is `NOT_SENT`.

The recording is `later_period_open_slots_stay_empty.mp4`.
The report is `docs/architecture/evidence/Alpha-Later-Rotation-Period-Report.pdf`.

The contract is `docs/architecture/contracts/ROTATION-PERIOD-CONTRACT.md`.
