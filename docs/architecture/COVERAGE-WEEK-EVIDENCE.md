# Coverage week

The contract after the hosted acceptance reading. One calendar week
stores the measured coverage. A missing market is not added. This is
not a census. The fuel gauge is unchanged. Green does not send.
Delivery stays `NOT_SENT`.

## What works

- The week is read from the stored qualified slices.
- The same week key does not write a second row.
- A request to add a missing market is refused.
- The fuel qualified count is unchanged.
- Census stays false.

Stored market names are the slices already on file. They are not a
census.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **429 passed, 0 failed, 0 skipped**.

The new proofs are `CoverageWeekTests` and `CoverageWeekApiTests`.

## Browser path

`/operations#/week` reads the stored markets and the fuel gauge.

The contract is `docs/architecture/contracts/COVERAGE-WEEK-CONTRACT.md`.
