# Bliss Economics & Rate Intelligence Engine™

**Status:** separate bounded context. Economics Phases 1–4 are
implemented; later phases remain gated. It does not alter Bliss Phases 1–19 or
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
| `PHASE-2-ENGINEERING-CONTRACT.md` | Creator audience/performance snapshot scope |
| `PHASE-2-EVIDENCE.md` | Phase 2 tests, API/database snapshots, and walkthrough |
| `PHASE-3-ENGINEERING-CONTRACT.md` | Market, industry, inventory, and FX scope |
| `PHASE-3-EVIDENCE.md` | Phase 3 tests, API/database snapshots, and walkthrough |
| `PHASE-4-ENGINEERING-CONTRACT.md` | Deterministic rate recommendation scope |
| `PHASE-4-EVIDENCE.md` | Phase 4 tests, API/database snapshots, and walkthrough |
| `FUTURE-ENGINEERING-CONTRACT.md` | Original reserved contract; superseded for Phase 1 |
| `../sql/future-economics-schema.sql` | PostgreSQL/Supabase-capable schema. **Do not apply** until a future contract is accepted |

## Current-phase rule

Phase 4 authorizes deterministic, versioned, explainable rate ranges and
one controlled recommendation write API. Do not implement n8n research
jobs, AI rate generation, quote workflows, compensation, or later tables
until their phase contract is issued and accepted.

Wedding Planner Phase 1 remains workspace / session / message / audit
infrastructure only.
