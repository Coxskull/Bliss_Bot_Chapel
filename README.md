# Bliss Bot Chapel

Standalone Bliss Bot Chapel: foundation, deterministic rule evaluation, historical audit, provider-neutral creator ingestion, and a browser-based test dashboard.

This repository and database are **not** connected to Alpha Auto. They are separate products and bounded contexts.

## Stack

- .NET 8 / ASP.NET Core
- Entity Framework Core + Npgsql
- PostgreSQL (Supabase-compatible)
- xUnit

## Solution

- `Bliss.Api` — REST API + Swagger + same-origin operations and creator-ingestion dashboard
- `Bliss.Domain` — entities and constants
- `Bliss.Infrastructure` — EF Core, configurations, migrations, seed data
- `Bliss.Tests` — architecture and persistence proofs

## Configuration

Do not commit credentials. Set the connection string via user secrets or environment variables:

```bash
export ConnectionStrings__DefaultConnection="Host=...;Port=5432;Database=...;Username=...;Password=...;Ssl Mode=Require"
```

`appsettings.json` keeps `ConnectionStrings:DefaultConnection` empty on purpose.

### Supabase PostgreSQL

The dashboard talks to `Bliss.Api`; it does **not** connect directly to PostgreSQL. A Supabase publishable key is intended for browser REST/Auth requests and cannot authenticate the EF Core/Npgsql backend. Do not put the database password or a service-role key in browser code.

For the supplied project host, set the database password only in your shell or deployment secret store. Do not commit it.

The direct host `db.bkutbglzivfdnoerigyb.supabase.co` is IPv6-only. From IPv4-only networks, use the **Session pooler** (port 5432):

```bash
export ConnectionStrings__DefaultConnection="Host=aws-0-ap-southeast-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.bkutbglzivfdnoerigyb;Password=<SUPABASE_DATABASE_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true"
```

Apply migrations to a dedicated test project/database before starting the app:

```bash
dotnet ef database update \
  --project Bliss.Infrastructure \
  --startup-project Bliss.Api \
  --connection "$ConnectionStrings__DefaultConnection"
```

Use `ASPNETCORE_ENVIRONMENT=Development` for the fictional Phase 1–3 seed data. Never point that seed process at production data.

To apply schema and fictional TEST seed from the Supabase SQL Editor instead of `dotnet ef`, run `docs/sql/bliss-supabase-full.sql` (idempotent). Schema-only: `docs/sql/bliss-supabase-schema.sql`. Seed-only: `docs/sql/bliss-supabase-seed.sql`. Do not run these scripts against Alpha Auto.

## Local commands

```bash
dotnet build
dotnet test
dotnet ef migrations script --project Bliss.Infrastructure --startup-project Bliss.Api -o phase1.sql
dotnet run --project Bliss.Api
```

Dashboard: `/`

Swagger (Development): `/swagger`

## Documentation

See `docs/bliss/` for Phase 1 architecture, requirements, and migration review.

Phase 1 final acceptance evidence: `docs/bliss/PHASE-1-FINAL-ACCEPTANCE.md`.

Phase 2 engineering contract: `docs/bliss/PHASE-2-ENGINEERING-CONTRACT.md`.
Phase 2 evidence: `docs/bliss/PHASE-2-EVIDENCE.md`.
Phase 3 engineering contract: `docs/bliss/PHASE-3-ENGINEERING-CONTRACT.md`.
Phase 3 evidence: `docs/bliss/PHASE-3-EVIDENCE.md`.
Phase 4 engineering contract: `docs/bliss/PHASE-4-ENGINEERING-CONTRACT.md`.
Phase 4 evidence: `docs/bliss/PHASE-4-EVIDENCE.md`.
