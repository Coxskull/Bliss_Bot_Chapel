# Recipe runtime

Phase 10 of the acquisition tracker. A produced demonstration stores
recipe `overlay-1` beside the flattened composite. The page resolves
that stored recipe. It does not research, translate, or rebuild the
overlay on the request.

## What works

- The recipe names the business, market, language, headline, call to
  action, approved source slice, QR destination, and disclosure.
- QA passes only when the QR destination is that prospect's page, the
  disclosure names the business and says it has not sponsored the work,
  and the overlay does not carry a personal name.
- A failing recipe is not written as a permanent composite.
- Restaurant Mesa Norte in Panama City, from
  `https://example.com/mesa-norte`, stores `overlay-1` with QA
  `PASSED`, headline `La Mesa`, and a QR path ending in
  `/demonstrations/mesa-norte`.
- The player draws that recipe on the approved source slice. The
  flattened MP4 remains available. Delivery stays `NOT_SENT`.

Mesa Norte and `example.com` are fixtures. They are not a claim about a
real business.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **233 passed, 0 failed, 0 skipped**.

The new proofs are `RecipeRuntimeTests` and `RecipeRuntimeApiTests`.

## Browser path

On Source Media, score Mesa Norte and produce one demonstration. The
notice names recipe `overlay-1`, QA `PASSED`, and the remaining
flattened composite. The demonstration page shows the just-in-time card
on the source slice, the QR, the disclosure, and the flattened file.

The screen recording is `mesa_norte_recipe_overlay_qa_passed.mp4`.
The PDF report is `evidence/Alpha-Recipe-Runtime-Report.pdf`.
