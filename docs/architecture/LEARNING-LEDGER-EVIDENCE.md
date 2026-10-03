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

On the live library, the laboratory is 10 of 10 graduated. Casa Verde
received one research note. Authorized traffic, production changed, and
behavior changed stay no. Model calls are 0. The same note was not
appended again. Casa Verde stayed `DEMONSTRATION_PREPARED`. Mesa Norte
has no research note and stayed `DEMONSTRATION_PREPARED`. Delivery is
`NOT_SENT`.

The recording is `research_ledger_note_leaves_production.mp4`.
The report is `docs/architecture/evidence/Alpha-Research-Ledger-Report.pdf`.

The contract is `docs/architecture/contracts/LEARNING-LEDGER-CONTRACT.md`.
