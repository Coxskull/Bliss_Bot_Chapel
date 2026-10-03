# Local batch measurement

The priority contract after the contact route audit. One measurement
reads the local database and stores the clock, the process working set,
and whether conversations stay apart. Hosted acceptance is not claimed.
The 15-minute factory target is not claimed. A rung of 100, 1,000, or
10,000 is not a stored census. No invoice is on file. Green does not
send. Delivery stays `NOT_SENT`.

## What works

- Elapsed time is the clock reading.
- A working set is one process reading in bytes. A missing reading
  stays unrecorded.
- Cost stays unrecorded. Economics remains the only price authority.
- A blank name is a partial failure and the other prospect remains.
- A second attempt records one retry. A third attempt is refused.
- A message that names another prospect is leakage.
- The same idempotency key does not write a second row.

`example.com` fixtures are not claims about real businesses.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **390 passed, 0 failed, 0 skipped**.

The new proofs are `BatchMeasurementTests` and
`BatchMeasurementApiTests`.

## Browser path

`/operations#/measure` reads the stored measurement rows. Hosted
acceptance stays unclaimed before and after the button.

The contract is `docs/architecture/contracts/LOCAL-BATCH-CONTRACT.md`.
