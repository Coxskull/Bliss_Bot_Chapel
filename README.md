# Bliss Bot Chapel

Standalone Bliss Bot Chapel: provider-neutral creator operations, deterministic matching, human review, campaign placement planning, immutable audit, and a complete browser-based operations frontend.

This repository and database are **not** connected to Alpha Auto. They are separate products and bounded contexts.

## Stack

- .NET 8 / ASP.NET Core
- Entity Framework Core + Npgsql
- PostgreSQL (Supabase-compatible)
- xUnit

## Solution

- `Bliss.Api` — REST API + Swagger + same-origin responsive operations frontend
- `Bliss.Domain` — entities and constants
- `Bliss.Infrastructure` — EF Core, configurations, migrations, seed data
- `Bliss.Tests` — architecture and persistence proofs

## Configuration

Do not commit credentials. Set the connection string via user secrets or environment variables:

```bash
export ConnectionStrings__DefaultConnection="Host=...;Port=5432;Database=...;Username=...;Password=...;Ssl Mode=Require"
```

`appsettings.json` keeps `ConnectionStrings:DefaultConnection` empty on purpose.

### Operator authentication

Non-Development deployments fail closed until provider-neutral OIDC is configured:

```bash
export Authentication__Enabled=true
export Authentication__Authority="https://identity.example.com"
export Authentication__ClientId="bliss-chapel"
export Authentication__ClientSecret="<OIDC_CLIENT_SECRET>"
export Runtime__DataProtectionKeysPath="/var/lib/bliss/data-protection"
```

Configure identity-provider role claims for `bliss.viewer`, `bliss.operator`,
`bliss.reviewer`, or `bliss.admin`. The client secret belongs in a deployment
secret store, never in `appsettings.json` or browser code. Development mode keeps
authentication explicitly disabled by default for local tests.

The Data Protection path must be a persistent, access-controlled volume shared
by every API replica. Configure deployment-owned reverse-proxy addresses through
`Runtime__KnownProxies__0`, `Runtime__KnownProxies__1`, and so on. Runtime health
probes are available at `/health/live` and `/health/ready`.

### Supabase PostgreSQL

The dashboard talks to `Bliss.Api`; it does **not** connect directly to PostgreSQL. A Supabase publishable key is intended for browser REST/Auth requests and cannot authenticate the EF Core/Npgsql backend. Do not put the database password or a service-role key in browser code.

For the supplied project host, set the database password only in your shell or deployment secret store:

```bash
export ConnectionStrings__DefaultConnection="Host=db.bkutbglzivfdnoerigyb.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<SUPABASE_DATABASE_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true"
```

If your network has no IPv6 route to the direct database host, copy the **Session pooler** connection string from Supabase Dashboard → Connect and set the same environment variable.

Apply migrations to a dedicated test project/database before starting the app:

```bash
dotnet ef database update \
  --project Bliss.Infrastructure \
  --startup-project Bliss.Api \
  --connection "$ConnectionStrings__DefaultConnection"
```

Use `ASPNETCORE_ENVIRONMENT=Development` for the fictional Phase 1–3 seed data. Never point that seed process at production data.

Hosted PostgreSQL live check: `docs/bliss/SUPABASE-LIVE-TEST.md`.

## Local commands

```bash
dotnet build
dotnet test
dotnet ef migrations script --project Bliss.Infrastructure --startup-project Bliss.Api -o phase1.sql
dotnet run --project Bliss.Api
```

Dashboard: `/`

Swagger (Development): `/swagger`

The frontend covers creator intake and profiles, match formation and evaluation,
human review, placement planning, partner and inventory directories, campaign
placements, and all operational ledgers. Operator labels stored in the browser
are audit labels only; they are not authentication.

## Documentation

See `docs/bliss/` for Phase 1 architecture, requirements, and migration review.

Phase 1 final acceptance evidence: `docs/bliss/PHASE-1-FINAL-ACCEPTANCE.md`.

Phase 2 engineering contract: `docs/bliss/PHASE-2-ENGINEERING-CONTRACT.md`.
Phase 2 evidence: `docs/bliss/PHASE-2-EVIDENCE.md`.
Phase 3 engineering contract: `docs/bliss/PHASE-3-ENGINEERING-CONTRACT.md`.
Phase 3 evidence: `docs/bliss/PHASE-3-EVIDENCE.md`.
Phase 4 engineering contract: `docs/bliss/PHASE-4-ENGINEERING-CONTRACT.md`.
Phase 4 evidence: `docs/bliss/PHASE-4-EVIDENCE.md`.
Phase 5 engineering contract: `docs/bliss/PHASE-5-ENGINEERING-CONTRACT.md`.
Phase 5 evidence: `docs/bliss/PHASE-5-EVIDENCE.md`.
Phase 6 engineering contract: `docs/bliss/PHASE-6-ENGINEERING-CONTRACT.md`.
Phase 6 evidence: `docs/bliss/PHASE-6-EVIDENCE.md`.
Phase 7 engineering contract: `docs/bliss/PHASE-7-ENGINEERING-CONTRACT.md`.
Phase 7 evidence: `docs/bliss/PHASE-7-EVIDENCE.md`.
Full frontend acceptance evidence: `docs/bliss/FULL-FRONTEND-EVIDENCE.md`.
Phase 8 OIDC security contract: `docs/bliss/PHASE-8-ENGINEERING-CONTRACT.md`.
Phase 8 OIDC security evidence: `docs/bliss/PHASE-8-EVIDENCE.md`.
Phase 9 runtime hardening contract: `docs/bliss/PHASE-9-ENGINEERING-CONTRACT.md`.
Phase 9 runtime hardening evidence: `docs/bliss/PHASE-9-EVIDENCE.md`.
Phase 10 operator observability contract: `docs/bliss/PHASE-10-ENGINEERING-CONTRACT.md`.
Phase 10 operator observability evidence: `docs/bliss/PHASE-10-EVIDENCE.md`.
Phase 11 operator audit export contract: `docs/bliss/PHASE-11-ENGINEERING-CONTRACT.md`.
Phase 11 operator audit export evidence: `docs/bliss/PHASE-11-EVIDENCE.md`.
Phase 12 match case file export contract: `docs/bliss/PHASE-12-ENGINEERING-CONTRACT.md`.
Phase 12 match case file export evidence: `docs/bliss/PHASE-12-EVIDENCE.md`.
Phase 13 creator case file export contract: `docs/bliss/PHASE-13-ENGINEERING-CONTRACT.md`.
Phase 13 creator case file export evidence: `docs/bliss/PHASE-13-EVIDENCE.md`.
Phase 14 campaign case file export contract: `docs/bliss/PHASE-14-ENGINEERING-CONTRACT.md`.
Phase 14 campaign case file export evidence: `docs/bliss/PHASE-14-EVIDENCE.md`.
Phase 15 export integrity contract: `docs/bliss/PHASE-15-ENGINEERING-CONTRACT.md`.
Phase 15 export integrity evidence: `docs/bliss/PHASE-15-EVIDENCE.md`.
Phase 16 export pack verification contract: `docs/bliss/PHASE-16-ENGINEERING-CONTRACT.md`.
Phase 16 export pack verification evidence: `docs/bliss/PHASE-16-EVIDENCE.md`.
Phase 17 verification receipt contract: `docs/bliss/PHASE-17-ENGINEERING-CONTRACT.md`.
Phase 17 verification receipt evidence: `docs/bliss/PHASE-17-EVIDENCE.md`.
Phase 18 last verification contract: `docs/bliss/PHASE-18-ENGINEERING-CONTRACT.md`.
Phase 18 last verification evidence: `docs/bliss/PHASE-18-EVIDENCE.md`.
Phase 19 verification history contract: `docs/bliss/PHASE-19-ENGINEERING-CONTRACT.md`.
Phase 19 verification history evidence: `docs/bliss/PHASE-19-EVIDENCE.md`.
