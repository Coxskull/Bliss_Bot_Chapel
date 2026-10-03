# Balanced creative inventory

Phase 28 of the acquisition tracker. A balanced pair is 2, 4, or 6.
Creator approval accepts that density or withholds it. A two-over-four
stack is refused. Stored slot rows stay unchanged and are not treated
as a balanced pair. Rotation is not configured. Green does not send.
Delivery stays `NOT_SENT`.

This reading does not assign an advertiser to a slot.

## What works

- A pair of 4 with creator approval is accepted.
- Pairs of 2 and 6 are accepted with the same approval.
- A refused creator approval withholds the pair.
- A two-over-four stack is refused even when approval is present.
- Eight is not a balanced pair. A missing approval is refused.
- Stored titles and slot types stay a count of stored rows.
- The reading does not add or rewrite a slot.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **338 passed, 0 failed, 0 skipped**.

The new proofs are `CreativeInventoryTests` and `CreativeInventoryApiTests`.

## Browser path

`/acquisition/inventory.html` reads the eight stored slots. Chapel
Conversations Episode 1 and Chapel Studio Diary 1 keep their stored
slot types. A pair of 4 with creator approval is accepted. The
two-over-four stack is refused. The stored slots remain. Delivery
remains `NOT_SENT`.

The recording is `creative_inventory_pair_of_four.mp4`.
The report is `docs/architecture/evidence/Alpha-Creative-Inventory-Report.pdf`.
