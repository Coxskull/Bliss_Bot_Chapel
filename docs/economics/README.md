# Bliss Economics & Rate Intelligence Engine™

**Status:** separate bounded context. Economics Phases 1–7 are
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
| `PHASE-5-ENGINEERING-CONTRACT.md` | Versioned quote and negotiation-history scope |
| `PHASE-5-EVIDENCE.md` | Phase 5 tests, API/database snapshots, and walkthrough |
| `PHASE-6-ENGINEERING-CONTRACT.md` | Versioned compensation illustration scope |
| `PHASE-6-EVIDENCE.md` | Phase 6 tests, API/database snapshots, and walkthrough |
| `PHASE-7-ENGINEERING-CONTRACT.md` | Bounded public-research orchestration scope |
| `PHASE-7-EVIDENCE.md` | Phase 7 tests, API/database snapshots, and walkthrough |
| `FUTURE-ENGINEERING-CONTRACT.md` | Original reserved contract; superseded for Phase 1 |
| `../sql/future-economics-schema.sql` | PostgreSQL/Supabase-capable schema. **Do not apply** until a future contract is accepted |

## Current-phase rule

Phase 7 authorizes bounded public-research jobs, untrusted candidate
staging, and explicit human promotion into append-only benchmark
observations. Do not implement historical-learning ingestion, AI rate
generation, settlement, payout, or later tables until their phase
contract is issued and accepted.

Wedding Planner Phase 1 remains workspace / session / message / audit
infrastructure only.
