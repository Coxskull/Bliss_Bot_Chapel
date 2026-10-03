# Spend and pause

Phase 21 of the acquisition tracker. One operations console records
tempo for six lanes. Stopping one lane leaves the others moving. A
spend ceiling is recorded only when an operator supplies both an amount
and a currency. None is invented. A ceiling is not an Economics price.
Green does not send. Delivery stays `NOT_SENT`.

The Bot Party amendment is recorded in `BOT-PARTY-AMENDMENT.md` and
queued as Phases 24–29. Those phases are not started. This control is
not the Fishing Fleet and it does not send.

## What works

- Six lanes start at full tempo with no ceiling: discovery,
  verification, road finding, policy, outreach, and creator discovery.
- Stopping outreach keeps discovery at full tempo.
- The stopped notice says the prospect record is preserved, other lanes
  keep moving, green does not send, and delivery remains `NOT_SENT`.
- A recorded ceiling of 40 USD is labeled an operational limit, not an
  Economics price.
- A partial ceiling is refused. The refusal says none is invented.
- Each change appends an audit row.
- Mesa Norte stays `OPPORTUNITY_SCORED` and `NOT_SENT`.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **295 passed, 0 failed, 0 skipped**.

The new proofs are `LaneTempoTests` and `LaneTempoApiTests`.

## Browser path

`/operations#/tempo` is the existing console. The recording stops the
outreach lane with a reason and no ceiling. Discovery stays at full
tempo. The notice says other lanes keep moving, green does not send,
none was invented, and delivery remains `NOT_SENT`.

The recording is `spend_pause_one_lane_stopped.mp4`.
The report is `docs/architecture/evidence/Alpha-Spend-Pause-Report.pdf`.
