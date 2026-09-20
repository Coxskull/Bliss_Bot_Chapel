# Mission 001 snapshots

Frozen evidence captured **2026-09-20T05:04:06Z**. These files are the point-in-time record, not live systems.

No passwords, JWT keys, or connection strings are stored here.

## Git freeze

See `00-git-baseline.txt`.

| Repository | Branch | SHA |
| --- | --- | --- |
| Bliss_Bot_Chapel (docs commit at capture) | `cursor/ai-mission-001-repository-audit-cc83` | `48dbe36bce977119582f13c7de05d129fb28e797` |
| Bliss_Bot_Chapel `origin/main` | `main` | `c828628d8df233e29b3b6374daf201bfadac6dc7` |
| Alpha-Auto-MVP | `main` | `d351b81c8ea0989b7fcc633f8c4caf755c0a54b2` |
| alpha-backend | `main` | `84f5ebf66c83ebd5f8ef79b45720b0c6b3163cb5` |
| alpha-frontend | `main` | `0f5850ce8ce0e5a82ca91afef562ba46c6943308` |
| alpha-mvp-v2 | `main` | `7be088a3728f8a8040ba65c90d162ab3db0865e0` |

## Bliss tests

`bliss/04-dotnet-test.txt` and `bliss/mission001-snapshot.trx`

```text
Passed!  Failed: 0  Passed: 23  Skipped: 0  Total: 23
```

## PostgreSQL TEST (local only)

`bliss/05-postgres-test-db.txt`

- Database: `bliss_phase1_test` (not production)
- Migration: `20260919013044_Phase1Foundation`
- Counts: 2 creators, 2 content items, 4 slots, 4 BlissMatches, 2 programs, 2 placements

## Live API JSON

`api/*.json` — GET responses from Development API against that TEST database.

## Screenshots

| File | What it shows |
| --- | --- |
| `screenshots/swagger_ui.png` | Swagger UI, Phase 1 GET surface |
| `screenshots/api_creators.png` | Live `/api/creators` records |
| `screenshots/api_bliss_matches.png` | Live `/api/bliss/matches` (A/B/C/D) |
| `screenshots/api_content_slots.png` | Episode 1 with four inventory slots |

## Alpha file lists (names only)

`alpha/*-file-list.txt` — source file paths. Alpha source was **not** copied into this repo.
