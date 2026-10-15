# Bliss Chapel development roadmap

Date: 2026-10-12. Status: Phases 1–3 completed; the user authorized Phases 4–6.
Phase 4 implementation and zero-cost local checks complete. Live-provider
validation is deferred until future OpenAI activation; Phase 4 acceptance remains
outstanding. Phase 5 implements manual creative approval and campaign handoff
independently of live AI; local validation is recorded in its evidence file.
Phase 6 implementation and local validation are complete. Hosted acceptance
remains outstanding: the user selected our recommended Azure deployment target,
and actual service bindings and deployment-side validation are still required.

“Phase 1” in this delivery roadmap refers to increment 1 — Reliable foundation.
It is distinct from the previously accepted Wedding Planner foundation phase.

This roadmap extends the existing product. These delivery increments do not
renumber accepted Bliss phases or declare Wedding Planner phases accepted.
Each later increment needs its own engineering contract, implementation,
tests, evidence, review, and acceptance before advancing.

## Existing foundation

- Bliss Bot: deterministic matching, human review, placements, audit exports,
  and Economics services already exist. Preserve their authority.
- Creative Academy: reference curriculum, creative DNA, production briefs,
  guarded image generation, and independent review gates already exist.
  The user confirmed that “Creator Academy” means this existing module.
- Wedding Planner: tenant-bound workspaces, sessions, append-only messages,
  audit, and an Economics recommendation endpoint already exist. The public
  conversation remains a preview. `/ai-workspace.html` adds controlled worker
  requests and private draft history; live providers are disabled in this environment.

## Delivery phases

| Increment | Goal and work | Completion evidence |
| --- | --- | --- |
| 1 — Reliable foundation | Protect Wedding Planner message retries from silently discarding changed text or actor types; preserve exact retries and tenant isolation. Establish regression baseline for all three modules. | Conflict/replay tests; original message and append audit unchanged on rejection; full regression suite with PostgreSQL. |
| 2 — Advertiser intake and Brand DNA | Design versioned Brand DNA attached to authorized workspaces, resumable intake, required/optional fields, and explicit human confirmation. Connect the public workspace UI to durable APIs. | Save/resume and cross-tenant tests; version history; accessible browser flow; no fabricated assistant responses. |
| 3 — Academy production workspace | Connect approved Brand DNA to existing Academy briefs and reference selection; persist provenance, candidate assets, revisions, and review history. Keep creator-training scope separate if requested. | A saved brief can be resumed and traced to its DNA/reference versions; unsupported references fail explicitly; no automatic approval. |
| 4 — Controlled AI conversation and production | Define provider-neutral role/tool contracts, quotas, timeouts, cancellation, cost recording, and privacy boundaries. Enable a configured provider only after credentials and spending constraints are supplied securely. | Provider contract tests, failure recovery, durable run records, and an authorized live check. AI output remains pending human review. |
| 5 — Creative review and Bliss handoff | Reuse advertiser approval, Alpha QA, deterministic matching, compatible inventory, and Economics authority. Design an explicit handoff from eligible creative to planned placement. | Unapproved or incompatible creative cannot advance; prices come from Economics; full authorized end-to-end test; no automatic sending. |
| 6 — Measurement and operating readiness | Link campaign outcomes to approved asset/brand versions, with reviewed learning proposals. Validate deployment authentication, database TLS, backups, observability, and recovery. | Attribution/replay tests, no silent changes to matching or pricing, and separately documented hosted acceptance. |

## Dependencies and limits

Local development needs no external keys. Live AI requires a selected provider,
secure credential binding, approved data scope, and a usage budget. Hosted
readiness requires OIDC, deployment-owned database and certificates, backup
policy, and operational access. Request those only for their relevant phase.

Do not invent missing quality anchors, treat generation as approval, price
inside Wedding Planner, couple to Alpha Auto, or activate campaigns from a
passing local test. External publication and outreach need explicit scope.

## Foundation engineering contract: increment 1

For POST `/api/wedding-planner/sessions/{id}/messages`, the existing source
system and idempotency key identify a retry. After access checks and existing
validation, an existing message in the same authorized session is replayed
only when normalized text and actor type match. Body comparison is ordinal;
actor comparison is case insensitive, consistent with stored uppercase actors.
Outer whitespace continues to use existing normalization.

Reusing the key with different text or actor type returns HTTP 409 with an
actionable instruction to use a new key. It must not overwrite the old message,
append another message, change the session timestamp, or append another
MESSAGE_APPENDED event. A key found in a different session retains HTTP 404.
Invalid and unauthorized actor requests retain their existing 400/403 result.

This increment requires no schema migration, AI calls, or matching changes.
Concurrent-writer retry behavior is an existing separate concern, not solved
by this payload comparison.

Phase 2: [engineering contract](PHASE-2-CONTRACT.md) and
[validation evidence](PHASE-2-EVIDENCE.md).

Phase 3: [engineering contract](PHASE-3-CONTRACT.md) and
[validation evidence](PHASE-3-EVIDENCE.md).

Phase 4: [engineering contract](PHASE-4-CONTRACT.md),
[provider setup](PHASE-4-PROVIDER-SETUP.md), and
[validation evidence](PHASE-4-EVIDENCE.md).

Phase 5: [engineering contract](PHASE-5-CONTRACT.md) and
[validation evidence](PHASE-5-EVIDENCE.md). Its explicit authorization allows
manual creative work while Phase 4 live-provider validation remains deferred.

Phase 6: [engineering contract](PHASE-6-CONTRACT.md),
[validation evidence](PHASE-6-EVIDENCE.md), and
[recommended hosted runbook](PHASE-6-HOSTED-RUNBOOK.md). Local recovery proof does
not establish hosted backup retention or the one-hour RPO/four-hour RTO targets.
