# Grooming and external research

Phase 22 of the acquisition tracker. The grooming note is counted from
production events already stored on the prospect. An external excerpt
can be staged only after those events. The excerpt is untrusted, it is
not copied onto the prospect, and it does not change production.
Delivery stays `NOT_SENT`.

This is not a research agent, not seven models, and not a deployment.
No research cost is invented.

## What works

- A prospect with no production event keeps external reading waiting.
- Stored events are counted. The latest event sets the next action.
- A page open says the watcher is not named and nothing is sent.
- Suppression on the record is friction. The prohibited action stays
  stopped.
- The cost line says none was invented and states no number.
- A weekly note is not a deployment. No experiment is open.
- An excerpt that asks for a send and a price is staged as untrusted.
  The reading does not repeat the excerpt or the number.
- Mesa Norte stays in its stored state. Its events are not rewritten.
  AI calls stay at 0.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **300 passed, 0 failed, 0 skipped**.

The new proofs are `GroomingReportTests` and `GroomingReportApiTests`.

## Browser path

`/acquisition/grooming.html` reads Mesa Norte's stored events, then
stages an untrusted excerpt. The notice says research cannot change
production, the instructions were not executed, and delivery remains
`NOT_SENT`.

The recording is `grooming_research_leaves_production.mp4`.
The report is `docs/architecture/evidence/Alpha-Grooming-Research-Report.pdf`.
