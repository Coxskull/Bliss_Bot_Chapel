# Technical debt and missing tests

## Technical debt

### Bliss

| ID | Item | Class |
| --- | --- | --- |
| TD-B1 | Persistence tests use EF InMemory, not PostgreSQL | 🧱 |
| TD-B2 | No HTTP/Swagger integration tests | 🧪 |
| TD-B3 | `dotnet ef` design-time factory ignores env unless `--connection` | 🧱 |
| TD-B4 | No GET for networks, access, provenance, campaigns | 🧱 (scope, not a bug) |
| TD-B5 | JSON omits nulls so unknown demographics disappear from API | 🧱 |
| TD-B6 | `MatchEvaluationRun` unused | ✅ addressed in Phase 3 (`cursor/phase3-evaluation-audit-cc83`) |
| TD-B7 | Controllers depend on DbContext (no application layer) | 🧱 acceptable for Phase 1 |
| TD-B8 | `Campaign` has no BlissMatch/Advertiser FKs | expected gap |

### Alpha Auto

| ID | Item | Class |
| --- | --- | --- |
| TD-A1 | Four overlapping repos | 🔁 ⚠️ |
| TD-A2 | EF migrations far behind AppDbContext | 🧱 |
| TD-A3 | Frontend API surface ≫ in-repo Alpha-Auto-MVP API | ⚠️ |
| TD-A4 | Dead UI: LiveMonitoring, EscalationPanel, unused react-admin | 🧱 |
| TD-A5 | Misplaced `reset-password` page in mvp-v2 | 🧱 |
| TD-A6 | `services/auth.ts` path mismatch `/Auth/login` | 🧱 |
| TD-A7 | Secrets in appsettings | 🔐 |
| TD-A8 | Zero automated tests | 🧪 |
| TD-A9 | Country/currency hard-coded maps | 🧱 |
| TD-A10 | Entrepreneur payout vs media payout naming collision if merged naively | ⚠️ |

## Missing tests (priority)

### Bliss (safe to add later)

- WebApplicationFactory GET `/api/creators` against TestServer + InMemory or Testcontainers Postgres
- Postgres unique-constraint negative tests (already done ad hoc in Phase 1 acceptance)
- Authorization tests once auth exists
- Chaperone/Officiant tests — **only after those engines are specified** (do not write fake engines)

### Alpha Auto

- Entire unit/integration suite missing
- Webhook signature tests
- Authz tests on financial endpoints

Do not add Alpha Auto tests from the Bliss repo.
