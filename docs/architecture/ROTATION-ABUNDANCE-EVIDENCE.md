# Rotation abundance

Phase 29 of the acquisition tracker. One rotation pass uses a balanced
pair of 2, 4, or 6 as its theoretical slots. Placed names are the
stored advertisers. Open slots stay open. One advertiser is not
required for every theoretical slot. Creator approval accepts the
rotation or withholds it. A later period is not configured. Stored
slots stay unchanged. Green does not send. Delivery stays `NOT_SENT`.

This reading does not invent an advertiser to fill a slot.

## What works

- Four stored advertisers in a pair of 6 leave two slots open.
- A full pair still says open slots were allowed.
- Creator refusal withholds the rotation and keeps the open count.
- A missing advertiser list is not treated as zero.
- More stored advertisers than the pair is refused. None is invented.
- A blank name is skipped and a repeated name is kept once.
- The reading does not rewrite stored slots.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **347 passed, 0 failed, 0 skipped**.

The new proofs are `RotationAbundanceTests` and `RotationAbundanceApiTests`.

## Browser path

`/acquisition/rotation.html` reads the four stored advertisers into a
pair of 6. Two slots stay open. The rotation is accepted. A later
period is not configured. The eight stored slots remain. Delivery
remains `NOT_SENT`.

The recording is `rotation_abundance_open_slots.mp4`.
The report is `docs/architecture/evidence/Alpha-Rotation-Abundance-Report.pdf`.
