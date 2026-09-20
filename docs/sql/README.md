# Bliss SQL for Supabase

Use these scripts only on a dedicated Bliss PostgreSQL database. Do not run them against Alpha Auto.

| File | Purpose |
| --- | --- |
| `bliss-supabase-full.sql` | Phases 1–6 schema + fictional TEST seed. Paste into Supabase SQL Editor. |
| `bliss-supabase-schema.sql` | Schema only (idempotent EF migrations). |
| `bliss-supabase-seed.sql` | Fictional TEST rows only. Run after schema. |

Both schema and seed are safe to re-run: schema checks `__EFMigrationsHistory`; seed uses `ON CONFLICT ("Id") DO NOTHING`.

Each file is a single `BEGIN` / `COMMIT` transaction, so a failure leaves the database untouched.

These files must stay free of a UTF-8 byte order mark. `dotnet ef migrations script` emits one, and a BOM in the middle of a concatenated file makes PostgreSQL report `syntax error at or near "CREATE"`. Strip it when regenerating:

```bash
sed -i '1s/^\xEF\xBB\xBF//' generated.sql
```

Do not put database passwords in these files.
