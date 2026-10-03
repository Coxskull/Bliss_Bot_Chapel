# Prospect progression

Phase 24 of the acquisition tracker. A prospect can be read as green,
yellow, or red. Green moves to the next authorized action. Green does
not send. Yellow keeps a preserved business and withholds the
demonstration. Red stops a suppressed action and keeps the business,
the state, and the reason. Delivery stays `NOT_SENT`.

## What works

- A scored prospect with no road is green for road finding.
- A stored public road that is not eligible is green for the policy
  check. A public road is not permission to send.
- A preview-eligible road is green for the approved queue. The
  authorized action prepares the preview.
- A preserved business is yellow. The record stays. The demonstration
  stays withheld.
- A suppressed business is red even when a road was eligible. The
  reason is kept. The business is not erased.
- The prospect state is not rewritten by the reading. AI calls stay at 0.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **310 passed, 0 failed, 0 skipped**.

The new proofs are `ProspectProgressionTests` and
`ProspectProgressionApiTests`.

## Browser path

`/demonstrations/mesa-norte` reads the progression. Mesa Norte is green
for the approved queue because a preview-eligible road is already
stored. The notice says green does not send. The business stays
`DEMONSTRATION_PREPARED`. Delivery remains `NOT_SENT`.

The recording is `prospect_progression_green_does_not_send.mp4`.
The report is `docs/architecture/evidence/Alpha-Prospect-Progression-Report.pdf`.
