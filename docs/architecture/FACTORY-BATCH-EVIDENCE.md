# Factory batch

Phase 11 of the acquisition tracker. One batch inspects the prospect
library with deterministic software checks and writes a cost manifest.
The check does not call a model and does not invent a price. Nothing is
sent.

## What works

- A preserved business with no demonstration passes as withheld.
- A produced recipe passes when `overlay-1` matches the prospect page,
  the disclosure names the missing sponsorship, the source slice is
  qualified, and the QR file is present. Recipe `overlay-1` does not
  require a flattened composite.
- A QR that leaves the prospect page, a preserved business that has a
  demonstration, and a missing flattened file on a picture that is not
  `overlay-1` are exceptions.
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

On Source Media, run factory QA. The live library returns status
`EXCEPTIONS`. Prospects 4. Preserved 1. Suppressed 1. Demonstrations 3.
Concepts 6. Checks passed 20. Exceptions 5. AI calls 0. The cost line
records no dollar amount and names Economics as the only price
authority. Delivery is `NOT_SENT`.

The five exceptions are the concepts produced before recipe `overlay-1`:
ABC Pharmacy family-health, convenience, wellness, and neighborhood,
and Casa Verde table. Puerto Azul stays `PRESERVED` in Quito, suppressed,
with no demonstration. Mesa Norte stays `DEMONSTRATION_PREPARED` on
`overlay-1`. Nothing is sent.

ABC Pharmacy, Casa Verde, Puerto Azul, Mesa Norte, and `example.com`
are fixtures. They are not claims about real businesses or people.
