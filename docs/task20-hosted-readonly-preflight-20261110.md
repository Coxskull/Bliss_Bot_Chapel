# Task #20 — Hosted read-only budget preflight

Use only in the owner-authorized Bliss Bot Supabase project `bkutbglzivfdnoerigyb`. The exact query is `scripts/task20-readonly-hosted-budget-preflight.sql`.

The query reads **only PostgreSQL function metadata, role permissions, RLS flags and aggregate Ask Alpha meter/receipt counts**. It never reads individual inquiries, transcripts, API keys, customer details, emails or network identities, and never changes rows, DDL, settings, Edge Functions or STOP.

Run it via the Supabase SQL editor or authorized SQL connector as a **read-only SELECT** after preparing a genuine isolated recovery point. Inspect these fields:

- `ai_disabled=true`: the hosted governor is still OFF; changing this to true requires a separate owner-approved rollout.
- `sql_cap_9=true`, `legacy_cap_10=false`: the deployed, forward-only governor cap aligns to the OpenAI project's **$9** limit (and the owner's **$10 funded / $1 margin** policy).
- `rpc_restricted=true` and `private_rls=true`: no anonymous or regular signed-in direct reads, EXECUTE limited to service role, receipt tables have RLS/FORCE.
- `receipt_count`, `unresolved_count`, `metered_month`, `externally_billed_usd`: **aggregate metadata only**. Any UNKNOWN/unreconciled state blocks a positive provider test until an authorized operator resolves usage. Do not reset billed numbers.
- **None** of the above constitutes a certified backup, valid gateway IP provenance, proof of OpenAI key-to-project binding, approval of the public AI consent notice, or proof of provider billing hard limit.

As of November 10, 2026, the real hosted project reports no Supabase development branches and the legacy $10 SQL expression; **do not apply the $9 migration or deploy the Edge function yet**. The $9 source code and isolated PG17 fail-closed rehearsal have passed. The exact deployment checklist remains `docs/task20-project9-hosted-release-checklist-20261110.md`.

This read-only preflight is automatically exercised after the fail-closed PostgreSQL 17 rehearsal. Source-only merges target `codex/alpha-prelaunch-safe-integration-20261110`; production `main` remains unchanged.
