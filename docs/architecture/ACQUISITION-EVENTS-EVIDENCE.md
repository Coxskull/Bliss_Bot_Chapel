# Acquisition events

Phase 12 of the acquisition tracker. The ledger records observable
steps. An opinion is not an event. A page open does not name who
watched. Nothing is sent.

## What works

- A newly stored business records `PROSPECT_RECORDED`.
- A manufactured demonstration records `DEMONSTRATION_PREPARED`.
- A visitor message records `MESSAGE_RECEIVED`.
- A page open records `PAGE_OPENED` with the observation that the
  watcher is not named.
- `INTERESTED` is refused: an event records what happened.
- A named watcher is refused.
- `SENT` is refused: nothing was sent, so a delivery event is not
  recorded.
- Every stored event has AI calls `0`.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **244 passed, 0 failed, 0 skipped**.

The new proofs are `AcquisitionEventTests` and `AcquisitionEventApiTests`.

## Browser path

On Source Media, record an event for Mesa Norte. `INTERESTED` is
refused and is not stored. `PAGE_OPENED` is stored. The notice names
the observation, AI calls 0, and delivery `NOT_SENT`.

Mesa Norte, Puerto Azul, Ana Ruiz, and `example.com` are fixtures. They
are not claims about real businesses or people.
