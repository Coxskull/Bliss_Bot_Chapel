# Scale proof

Phase 23 of the acquisition tracker. The ladder runs 100, then 1,000,
then 10,000 repeated in-memory checks of the recipe and the
conversation. A passed check is a measurement. It is not a claim of
stored prospects, a hosted platform, or a send. The 15-minute factory
target is not claimed. No dollar amount is invented. Delivery stays
`NOT_SENT`.

## What works

- Each rung runs the recipe check, an unpriced question, a send
  question, a suppressed send, the other fixture's question, and a
  repeated price question.
- A leaked name, a stated number, a missed suppression, or a repeated
  question treated as a new priced action fails the rung.
- A count outside 100, 1,000, and 10,000 is refused. None is invented.
- Every passed rung stays unclaimed.
- The measurement does not write Mesa Norte.
- AI calls stay at 0.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **305 passed, 0 failed, 0 skipped**.

The new proofs are `ScaleProofTests` and `ScaleProofApiTests`.

## Browser path

`/acquisition/scale.html` shows the three rungs measured, each marked
not claimed, the cost sentence with no number, and the factory target
not claimed. Mesa Norte stays unchanged. Delivery remains `NOT_SENT`.

The recording is `scale_proof_rungs_not_claimed.mp4`.
The report is `docs/architecture/evidence/Alpha-Scale-Proof-Report.pdf`.
