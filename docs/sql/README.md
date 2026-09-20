# Bliss SQL for Supabase

Use these scripts only on a dedicated Bliss PostgreSQL database. Do not run them against Alpha Auto.

| File | Purpose |
| --- | --- |
| `bliss-supabase-full.sql` | Phases 1–4 schema + fictional TEST seed. Paste into Supabase SQL Editor. |
| `bliss-supabase-schema.sql` | Schema only (idempotent EF migrations). |
| `bliss-supabase-seed.sql` | Fictional TEST rows only. Run after schema. |

Both schema and seed are safe to re-run: schema checks `__EFMigrationsHistory`; seed uses `ON CONFLICT ("Id") DO NOTHING`.

Do not put database passwords in these files.
