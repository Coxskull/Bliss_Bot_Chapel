# Closed models

The contract after the marketplace reading. One reading stores that
seven grooming models are not configured. Configured models stay at
zero. Model calls stay at the stored count. This is not a traffic
count. The learning page is unchanged. Green does not send. Delivery
stays `NOT_SENT`.

## What works

- The reading is taken from the stored research notes and the laboratory.
- Configured models stay at zero. A model is not named.
- The same reading key does not write a second row.
- A request to configure a grooming model is refused.
- A claim of authorized traffic is refused.
- An engagement count is refused.
- The learning page sentence stays in place.

A model call count above the stored reading is not written back onto
the earlier row.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **441 passed, 0 failed, 0 skipped**.

The new proofs are `ClosedModelsTests` and `ClosedModelsApiTests`.

## Browser path

`/operations#/models` reads the stored model-call count and the laboratory.

The contract is `docs/architecture/contracts/CLOSED-MODELS-CONTRACT.md`.
