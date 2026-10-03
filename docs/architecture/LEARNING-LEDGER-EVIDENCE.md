# Research ledger

The priority contract after human creative approval. One research note
is appended after the conversation laboratory graduates. No authorized
traffic is on file. The note does not change production. Research does
not control production. A model is not the record. Seven grooming
models are not configured. This is not a traffic count. Green does not
send. Delivery stays `NOT_SENT`.

## What works

- The laboratory must graduate before a note is appended.
- The same prospect and idempotency key do not append a second row.
- A request to change production is refused.
- A claim of authorized traffic is refused.
- An engagement count is refused.
- Another prospect's reading does not include the note.
- The prospect state stays as it was.

`example.com` fixtures are not claims about real businesses.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **408 passed, 0 failed, 0 skipped**.

The new proofs are `LearningLedgerTests` and `LearningLedgerApiTests`.

## Browser path

`/operations#/learning` reads one stored prospect at a time.

The contract is `docs/architecture/contracts/LEARNING-LEDGER-CONTRACT.md`.
