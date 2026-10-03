# Source media coverage

The priority contract after the player-served picture. Market coverage
counts stored qualified slices that have a fingerprint and provenance.
A duplicate is not coverage. A missing market is not listed. The fuel
gauge counts stored clips. A calendar week is not configured. This is
not a census. Green does not send. Delivery stays `NOT_SENT`.

A copyrighted podcast is not downloaded.

## What works

- One qualified Panama City slice covers Panama City and no other market.
- A duplicate in that market does not raise the count.
- A qualified slice without a frame hash is withheld.
- A blank market is not given a city name.
- Missing provenance is withheld.
- A missing clip list is refused.
- The operations fuel reading starts at shortage when no slice is stored.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **371 passed, 0 failed, 0 skipped**.

The new proofs are `SourceMediaCoverageTests` and `SourceMediaCoverageApiTests`.

## Browser path

`/operations#/fuel` reads the stored clips. The fuel row is the stored
count. Each market row is a stored qualified slice with a fingerprint
and provenance. A market that is not stored is absent.

The contract is `docs/architecture/contracts/SOURCE-MEDIA-COVERAGE-CONTRACT.md`.
