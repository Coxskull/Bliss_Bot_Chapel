# Wedding Planner Phase 1 — Engineering Contract

## Objective

Build the minimum safe Wedding Planner foundation inside the accepted
Bliss Bot Chapel repository:

Advertiser → authorized Wedding Planner workspace → planning session →
durable conversation message → append-only audit.

Phase 1 is infrastructure. It requires **zero AI agents** and zero
executable AI workers.

## Repository inspection (accepted Bliss main)

Inspected from `origin/main` after Phase 19 verification-history merge.

| Area | Current convention | Phase 1 reuse |
| --- | --- | --- |
| Auth | Provider-neutral OIDC cookie session; Development fail-open; roles `bliss.viewer` / `bliss.operator` / `bliss.reviewer` / `bliss.admin` | Keep OIDC. Add optional `advertiser_id` claim and `bliss.advertiser` role. Do not add a second IdP. |
| Operator vs advertiser | Operator console is the only UI identity today | Chapel staff (operator/admin) may open any existing advertiser workspace. Advertiser principals are bound to one advertiser id. Browser labels are not ownership. |
| Data | EF Core + Npgsql, PostgreSQL/Supabase-compatible, restrict deletes, unique source+idempotency | Same `BlissDbContext`, new tables, restrict FKs, unique primary workspace per advertiser. |
| API | `/api/...` controllers, write rate limits, CSRF when auth enabled, `X-Request-Id` | `/api/wedding-planner/...` with the same write/CSRF/correlation pattern. |
| Audit | Immutable run/decision tables plus in-process operational events | Durable `WeddingPlannerAuditEvents` for workspace/session/message writes. |
| Frontend | Dedicated `frontend/` folder | Public advertiser/creator UI in `frontend/public`, served at `/`. Operator console in `frontend/operations`, served at `/operations`. The API serves those files and does not keep frontend source in `wwwroot`. |
| Tests | xUnit, in-memory EF, `BlissApiFactory`, `OidcSecurityApiFactory` | Phase 1 proofs plus cross-tenant negatives on the OIDC factory. |
| Matching | `DeterministicRuleEvaluator` is scoring authority | Unchanged. Wedding Planner code must not call it. |
| Alpha Auto | Explicitly separate product | No references, no shared identity, no shared writes. |

## Agent allocation (required documentation)

1. Logical AI roles required: none
2. Number of logical roles: 0
3. Responsibilities: not applicable
4. Dedicated executable workers: 0
5. Shared workers: none
6. Provider/model abstraction: none in Phase 1
7. Allowed tools: none
8. Allowed data: authenticated caller may read/write only authorized advertiser workspace/session/message/audit rows
9. Prohibited actions: AI generation, planner-bot replies, Color Intelligence, Curator, matching writes, Alpha Auto, n8n authority
10. Inputs: advertiser id, source system, idempotency key, human/system message body
11. Outputs: workspace, session, message, audit DTOs
12. Human approval: not in Phase 1 (no creative approval state machine yet)
13. Deterministic services used: EF persistence, unique constraints, tenant checks
14. n8n involvement: none
15. Failure behavior: 401 unauthenticated, 403 forbidden bind, 404 missing or cross-tenant resource, 400 validation, replay returns existing row
16. Audit: append-only workspace/session/message events with actor and request id
17. Cost/usage recording: none (no AI calls)
18. Agent/prompt/model version: none
19. Tests proving authority boundaries: OIDC advertiser A cannot read/write B
20. Routing evidence: no specialist routing; all requests stay in Wedding Planner application service

Starting allocation: 0 AI agents. No proposed change.

## Required behavior

1. Link a workspace to an existing `Advertiser` row.
2. One primary workspace per advertiser. A second open is a durable replay, not a duplicate hotel.
3. Create and later retrieve planning sessions for that workspace.
4. Append-only conversation messages with sequence numbers. No silent overwrite.
5. Persist audit events for opens, session creates, and message appends.
6. Development (auth disabled) behaves as chapel staff, matching the rest of Bliss local tests.
7. When OIDC is enabled:
   - anonymous API access is 401
   - `advertiser_id` binds a caller to one advertiser
   - operator/admin may act across advertisers
   - viewer cannot write and cannot list other advertisers' workspaces
8. Invalid ids fail as 404. PUT/PATCH are not provided.
9. Actor types in Phase 1 are `ADVERTISER`, `OPERATOR`, and `SYSTEM` only. `PLANNER` / AI actors are rejected.

## API

- `GET /api/wedding-planner/workspaces`
- `POST /api/wedding-planner/workspaces`
- `GET /api/wedding-planner/workspaces/{id}`
- `GET /api/wedding-planner/workspaces/{id}/sessions`
- `POST /api/wedding-planner/workspaces/{id}/sessions`
- `GET /api/wedding-planner/sessions/{id}`
- `GET /api/wedding-planner/sessions/{id}/messages`
- `POST /api/wedding-planner/sessions/{id}/messages`
- `GET /api/wedding-planner/messages/{id}`
- `GET /api/wedding-planner/workspaces/{id}/audit`

## Public experience boundary

The public root is for advertisers and creators. It presents:

- Alpha Bliss Chapel positioning and advertiser/creator entry points
- the Wedding Planner as the future primary customer-facing personality
- a visual conversation preview matching the approved design direction
- an explicit Phase 1 foundation notice and disabled composer

The internal operator console remains available at `/operations`. Backend
ownership, tenant isolation, and audit remain authoritative. Phase 1 does
not fabricate AI responses or imply that a provider is active.

## Explicit exclusions

- Phase 2–9 capabilities
- AI providers, prompts, fake chatbot replies
- Alpha Color Intelligence
- Curator research
- Creative concepts, assets, palettes, Brand DNA versions
- Campaign-ready state machine and Bliss handshake
- GHL, n8n, Alpha Auto
- Changes to deterministic scoring, review authority, or placement arithmetic
- Economics & Rate Intelligence (quotes, CPMs, compensation). Wedding Planner
  must not invent prices; that engine is a later bounded context.

## Tests

- Authorized create/access of own workspace
- Durable ownership and no duplicate primary workspace
- Session create and later retrieve
- Durable message persist/retrieve and replay
- Advertiser A cannot GET B workspace/session/message
- Advertiser A cannot POST into B session
- Invalid ids 404
- Anonymous 401 when OIDC enabled
- Replay handled
- Existing Bliss tests continue passing
- Deterministic scoring untouched
- Alpha Auto remains separate
- AI actor types rejected

## Sandbox advertisers

Fictional seed rows: TEST Dental Manila and TEST Restaurant Santo Domingo,
in addition to the existing Phase 1 Sunrise Wellness advertiser.
