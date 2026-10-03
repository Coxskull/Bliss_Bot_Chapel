# Prospect memory in PostgreSQL

Phase 17 of the acquisition tracker. Prospect rows, clip metadata, and
factory batches are stored in the existing Bliss database. There is no
second database. Video files and QR files stay on disk. This phase does
not claim the 100, 1,000, or 10,000 scale ladder.

## What works

- Discovering Mesa Norte writes one `ProspectMemories` row. The business
  name, state `OPPORTUNITY_SCORED`, and score 100 are columns. The
  payload carries the same prospect.
- Puerto Azul is a separate row in state `PRESERVED` with score 75. Its
  payload does not contain Mesa Norte, and the Mesa Norte payload does
  not contain Puerto Azul.
- Suppression updates the `Suppressed` column. Delivery stays `NOT_SENT`.
- A new prospect does not write `library.json`. The file is not the
  record.
- When the database is empty and a `library.json` file already exists,
  that file is copied in once. Later reads come from the database, so a
  later edit to the file does not rename the prospect.
- The library response names the provider. Automated tests use the
  in-memory provider and report `IN_MEMORY`. The Bliss model still has
  one `DbContext`, and the table name is `ProspectMemories`.
- The development server, pointed at PostgreSQL, reports `POSTGRESQL`.

`example.com` fixtures are not claims about real businesses.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **268 passed, 0 failed, 0 skipped**.

The new proofs are `ProspectMemoryApiTests` and
`ProspectMemoryImportTests`.

## Browser path

Source Media shows `Prospect memory: POSTGRESQL. One database. Media
files stay on disk.` The imported prospects remain on the page.
