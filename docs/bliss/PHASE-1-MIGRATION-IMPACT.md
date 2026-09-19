# PHASE-1-MIGRATION-IMPACT

```
Migration name: Phase1Foundation
Database target: PostgreSQL (Supabase-compatible Phase 1 database)
Destructive changes: none
```

## Generated artifacts

- `Bliss.Infrastructure/Migrations/20260919013044_Phase1Foundation.cs`
- `phase1.sql` (also copied to `docs/sql/phase1.sql`)

## Review

Inspected `phase1.sql` before any apply step.

| Check | Result |
| --- | --- |
| `DROP TABLE` | Not present |
| Destructive data operations | Not present |
| Foreign keys | Present, `ON DELETE RESTRICT` |
| Indexes | Present on FKs and lookup fields |
| Nullable demographics | `FemalePercentage` / `MalePercentage` nullable |
| Unique `BlissMatches.CreatorId` | Not present |
| Unique `CampaignPlacements.ContentItemId` | Not present |
| Accidental 1:1 inventory→ad | Not present |

## Apply policy

Do not apply this migration to any shared/production database without operator review.

Local Phase 1 test databases may apply:

```bash
dotnet ef database update --project Bliss.Infrastructure --startup-project Bliss.Api
```

Connection strings must come from environment variables or user secrets.
