# Bliss Economics & Rate Intelligence Engine™

**Status:** separate bounded context. Economics Phase 1 is authorized;
later phases remain gated. It does not alter Bliss Phases 1–19 or
Wedding Planner Phases 1–9.

**Working name:** Bliss Economics & Rate Intelligence Engine™

This folder preserves the requirement so Alpha can later estimate,
recommend, explain, and quote advertising inventory **without**
replacing matching, Wedding Planner, campaign placement, or the
creator/advertiser databases.

| Document | Purpose |
| --- | --- |
| `ARCHITECTURE.md` | Product purpose, pricing principles, entities, governance |
| `INTEGRATION-POINTS.md` | Who may call the engine, and who must not invent prices |
| `PHASE-ROADMAP.md` | Independent Economics phase sequence and gates |
| `PHASE-1-ENGINEERING-CONTRACT.md` | Authorized reference-data foundation |
| `PHASE-1-EVIDENCE.md` | Phase 1 tests, API/database snapshots, and walkthrough |
| `FUTURE-ENGINEERING-CONTRACT.md` | Original reserved contract; superseded for Phase 1 |
| `../sql/future-economics-schema.sql` | PostgreSQL/Supabase-capable schema. **Do not apply** until a future contract is accepted |

## Current-phase rule

Phase 1 authorizes only reference-data EF tables, GET APIs, and
read-only operator visibility. Do not implement recommendation APIs,
n8n research jobs, AI rate generation, quote workflows, or later tables
until their phase contract is issued and accepted.

Wedding Planner Phase 1 remains workspace / session / message / audit
infrastructure only.
