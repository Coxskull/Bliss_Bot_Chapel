# Wedding Planner Phase 1 Evidence

## Result

Phase 1 foundation is implemented as a bounded module on the accepted
Bliss main line (Phases 1–19). Automated tests: **114 passed, 0 failed**.

AI agents: **0**. Color Intelligence, Curator, creative generation,
Bliss handshake, and Alpha Auto were not added.

## Base

- Branch: `cursor/wedding-planner-phase1-foundation-4deb`
- Base: `origin/main` (`be7ec41` — Phase 19 verification history)

## Migration

EF Core migration `WeddingPlannerPhase1Foundation`
(`20260922052555_WeddingPlannerPhase1Foundation`):

- `WeddingPlannerWorkspaces` unique on `AdvertiserId`
- `WeddingPlannerPlanningSessions`
- `WeddingPlannerConversationMessages` unique on session+sequence
- `WeddingPlannerAuditEvents`
- Restrict delete to existing advertisers
- Unique source+idempotency keys on workspace, session, and message

## Automated tests

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **114 passed, 0 failed, 0 skipped**.

Prior Bliss suite was 107 on the earlier reconciliation note and 105 on
Phase 18 evidence; current main plus Phase 19 plus Phase 1 Wedding
Planner proofs total 114.

New proofs:

- authorized open/resume of a primary workspace
- session create, replay, later retrieve
- durable message persist/replay
- invalid ids 404, PUT 405, AI `PLANNER` actor 400
- OIDC anonymous 401
- advertiser A cannot read or write advertiser B
- viewer cannot write
- unique primary workspace persistence
- frontend includes the foundation ledger, not a chatbot
- Wedding Planner source does not call scoring, n8n, or Alpha Auto

Log: `docs/wedding-planner/evidence/phase1/tests/dotnet-test.txt`

## Authorization model used

Existing operator OIDC is preserved.

Advertiser tenancy is a server-side `advertiser_id` claim. Chapel
operators/admins may open workspaces for existing advertisers.
Development auth-disabled mode remains chapel-staff, matching other
Bliss tests. The browser is not the owner.

## UI

Operations shell gained a **Wedding Planner** resume ledger:

- open/resume primary workspace
- create/resume planning session
- append human/system messages

It is not the marketing chapel dashboard and does not call an LLM.

## Explicitly out of scope (not done)

- Phase 2 conversational AI / Brand DNA interpreter
- Alpha Color Intelligence
- Curator
- concepts, prototypes, image providers
- chaperone/QA AI
- Bliss campaign handshake
- measurement/learning agents
- GHL / n8n / Alpha Auto coupling
- changes to deterministic matching arithmetic
