# Bliss Bot Chapel

Standalone Phase 1 foundation for Creator Intelligence, advertiser programs, matches, and campaign placement architecture.

This repository is **not** connected to Alpha Auto.

## Stack

- .NET 8 / ASP.NET Core
- Entity Framework Core + Npgsql
- PostgreSQL (Supabase-compatible)
- xUnit

## Solution

- `Bliss.Api` — read-only REST API + Swagger
- `Bliss.Domain` — entities and constants
- `Bliss.Infrastructure` — EF Core, configurations, migrations, seed data
- `Bliss.Tests` — architecture and persistence proofs

## Configuration

Do not commit credentials. Set the connection string via user secrets or environment variables:

```bash
export ConnectionStrings__DefaultConnection="Host=...;Port=5432;Database=...;Username=...;Password=...;Ssl Mode=Require"
```

`appsettings.json` keeps `ConnectionStrings:DefaultConnection` empty on purpose.

## Local commands

```bash
dotnet build
dotnet test
dotnet ef migrations script --project Bliss.Infrastructure --startup-project Bliss.Api -o phase1.sql
dotnet run --project Bliss.Api
```

Swagger (Development): `/swagger`

## Documentation

See `docs/bliss/` for Phase 1 architecture, requirements, and migration review.
