# Bliss Economics & Rate Intelligence Engine™

**Status:** future bounded context. Not part of the current approved
Bliss Phases 1–19 sequence and not part of Wedding Planner Phases 1–9.

**Working name:** Bliss Economics & Rate Intelligence Engine™

This folder preserves the requirement so Alpha can later estimate,
recommend, explain, and quote advertising inventory **without**
replacing matching, Wedding Planner, campaign placement, or the
creator/advertiser databases.

| Document | Purpose |
| --- | --- |
| `ARCHITECTURE.md` | Product purpose, pricing principles, entities, governance |
| `INTEGRATION-POINTS.md` | Who may call the engine, and who must not invent prices |
| `FUTURE-ENGINEERING-CONTRACT.md` | Reserved first implementation contract (not authorized yet) |
| `../sql/future-economics-schema.sql` | PostgreSQL/Supabase-capable schema. **Do not apply** until a future contract is accepted |

## Current-phase rule

Do not implement recommendation APIs, n8n research jobs, AI rate
generation, quote workflows, or EF migrations for these tables until a
later engineering contract is issued and accepted.

Wedding Planner Phase 1 remains workspace / session / message / audit
infrastructure only.
