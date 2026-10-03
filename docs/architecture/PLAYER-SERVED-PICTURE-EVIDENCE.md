# Player-served picture

The priority contract after the subscription ledger. Recipe `overlay-1`
is the served picture. A permanent per-prospect MP4 is not written.
QA still requires the prospect-page QR and the not-sponsored disclosure.
An older picture that is not `overlay-1` still reports a missing file.
Green does not send. Delivery stays `NOT_SENT`.

This contract does not generate video or build a per-prospect website.

## What works

- Mesa Norte stores `overlay-1` with QA `PASSED` and headline `La Mesa`.
- The QR destination ends in `/demonstrations/mesa-norte`.
- The disclosure says Mesa Norte has not sponsored the work.
- `servedPicture` is `PLAYER`. The permanent video route is not found.
- The source slice and the QR file are served.
- The factory manifest passes with no flattened file and zero AI calls.

Mesa Norte and `example.com` are fixtures. They are not a claim about a
real business.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **365 passed, 0 failed, 0 skipped**.

The proofs are `RecipeRuntimeTests`, `FactoryBatchTests`,
`RecipeRuntimeApiTests`, and `ProspectDemonstrationApiTests`.

## Browser path

`/demonstrations/mesa-norte` shows the player on the source slice, the
QR, the disclosure, and the notice that a permanent composite is not
required. The flattened file is not on the page. Delivery remains
`NOT_SENT`.

The contract is `docs/architecture/contracts/PLAYER-SERVED-PICTURE-CONTRACT.md`.
