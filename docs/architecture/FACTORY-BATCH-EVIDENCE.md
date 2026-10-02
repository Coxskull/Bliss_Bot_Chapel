# Factory batch

Phase 11 of the acquisition tracker. One batch inspects the prospect
library with deterministic software checks and writes a cost manifest.
The check does not call a model and does not invent a price. Nothing is
sent.

## What works

- A preserved business with no demonstration passes as withheld.
- A produced recipe passes when `overlay-1` matches the prospect page,
  the disclosure names the missing sponsorship, the source slice is
  qualified, and the flattened composite and QR file are present.
- A QR that leaves the prospect page, a preserved business that has a
  demonstration, and a missing flattened file are exceptions.
- The manifest records prospect, preserved, demonstration, and concept
  counts, checks passed, exceptions, elapsed time, and AI calls `0`.
- The cost line is: no dollar amount is recorded. Economics remains the
  only price authority.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **238 passed, 0 failed, 0 skipped**.

The new proofs are `FactoryBatchTests` and `FactoryBatchApiTests`.

## Browser path

On Source Media, run factory QA. The notice names the status, the
counts, AI calls 0, the cost line, and delivery `NOT_SENT`.
