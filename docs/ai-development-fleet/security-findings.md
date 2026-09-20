# Security findings

**If a value looks like a password, it is redacted.** Do not paste production secrets into chat, WhatsApp, or git.

## 🚨 STOP — exposed production-style credentials

Discovered during Mission 001 **read-only** clone of Alpha Auto repos. **Not introduced by Bliss Phase 1.**

| Secret type | File/path | Severity | Remediation |
| --- | --- | --- | --- |
| PostgreSQL / Supabase connection string (user + password) | `Coxskull/alpha-backend` → `appsettings.json` → `ConnectionStrings:DefaultConnection` | **CRITICAL** | Rotate DB password immediately; remove from git; use env/secret store; assume compromise |
| JWT signing key | `Coxskull/alpha-backend` → `appsettings.json` → `Jwt:Key` | **CRITICAL** | Rotate JWT key; invalidate sessions; env-only |
| PostgreSQL / Supabase connection string | `Coxskull/Alpha-Auto-MVP` → `Alpha.API/appsettings.json` | **CRITICAL** | Same as above (may be same or related instance) |
| JWT signing key | `Coxskull/Alpha-Auto-MVP` → `Alpha.API/appsettings.json` | **HIGH** | Rotate |

This agent **did not** write those values into Bliss documentation and **did not** connect to those databases.

Bliss `appsettings.json` `ConnectionStrings:DefaultConnection` is **empty**. Bliss design-time factory uses a **local disposable** `postgres` placeholder for `localhost` — not a hosted production secret.

## Bliss (this repo)

| Finding | Path | Severity | Notes |
| --- | --- | --- | --- |
| No authentication on APIs | `Bliss.Api/Program.cs`, controllers | MEDIUM (Phase 1 expected) | Anonymous GET of seed/test data |
| `UseAuthorization` without authentication | `Program.cs` | LOW | Dead middleware |
| Swagger in Development only | `Program.cs` | OK | |
| EF InMemory tests | `TestDb.cs` | LOW (coverage gap) | Not a secret issue |
| No rate limiting | API | LOW until public | |
| CORS not configured | API | LOW | Defaults |
| SQL injection | EF parameterized | LOW | No raw SQL in Bliss app code reviewed |
| PII | Creator demographics | MEDIUM later | Test data is fictional CRT-TEST-001 |

## Alpha Auto (additional, values redacted)

| Finding | Path | Severity |
| --- | --- | --- |
| Unauthenticated settlement queue GET | `FinancialsController` | HIGH |
| Unauthenticated DB diagnostic (`current_user`, server) | `DatabaseDiagnosticController` | HIGH |
| Unauthenticated commission test endpoint | `AutoPartsCommissionTestController` | HIGH |
| Admin commission API not role-restricted | `AutoPartsCommissionController` | HIGH |
| PayMongo webhook without signature | `PayMongoWebhookController` | HIGH |
| Many write APIs without `[Authorize]` | Orders/Products/Cart/Payments/Customers | HIGH |
| Exception `ToString()` returned to clients | `Program.cs` | MEDIUM |
| `EnableSensitiveDataLogging` | `Program.cs` | MEDIUM |
| Swagger always on | `Program.cs` | MEDIUM |
| `alpha-frontend` Mission Control with **no login** | layout/api.ts | HIGH if pointed at real API |
| Demo login defaults `{role}@alpha.com` / `{role}123` | `LoginForm.tsx` in mvp-v2 | MEDIUM |

## What this mission did not do

- Did not dump secret values
- Did not rotate credentials (requires operator)
- Did not scan git history for other leaked keys
- Did not assess hosted n8n credential stores
