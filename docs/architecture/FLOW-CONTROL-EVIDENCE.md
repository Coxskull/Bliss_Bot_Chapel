# Flow control

Phase 25 of the acquisition tracker. A downstream lane can take fewer
prospects than are already stored. The excess stays queued. A
suppressed record stays withheld. None are discarded. The opening
count is a flow limit, not an Economics price. Green does not send.
Delivery stays `NOT_SENT`.

This reading does not invent a census of 250,000 prospects and it does
not build a second fleet.

## What works

- Four stored fixtures and one opening release the first name, hold
  the other legitimate prospects, and withhold the suppressed record.
- A larger opening count does not invent extra prospects.
- A missing or zero opening count is refused. None is invented.
- The stored library is the same after the reading. Puerto Azul stays
  preserved, suppressed, and unsent.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **315 passed, 0 failed, 0 skipped**.

The new proofs are `FlowControlTests` and `FlowControlApiTests`.

## Browser path

`/acquisition/flow.html` regulates the stored prospects with one
downstream opening. ABC Pharmacy is released this pass. Casa Verde and
Mesa Norte stay held. Puerto Azul stays withheld. Discarded is zero.
The stored names remain. Delivery remains `NOT_SENT`.

The recording is `flow_control_none_discarded.mp4`.
The report is `docs/architecture/evidence/Alpha-Flow-Control-Report.pdf`.
