# Wedding Planner Phase 2 — Engineering Contract

Status: authorized for implementation by the request to proceed to Phase 2.

## Objective

Add the first AI-assisted Wedding Planner loop without changing accepted
Bliss matching:

1. a customer-facing Concierge that replies inside an authorized planning
   session;
2. a Brand DNA Interpreter that creates structured, immutable Brand DNA
   proposals from authorized conversation history; and
3. human decisions that alone can approve or reject those proposals.

PostgreSQL remains permanent memory. Providers are interchangeable workers,
not customer memory. AI generation never equals human approval.

## Workforce allocation

| Item | Phase 2 allocation |
| --- | --- |
| Logical AI roles | 2: `CONCIERGE`, `BRAND_DNA_INTERPRETER` |
| Executable workers | 1 shared provider worker, within the blueprint's 1–2 starting range |
| Routing | deterministic: chat turn → Concierge; explicit interpretation → Interpreter |
| Provider abstraction | `IWeddingPlannerAiProvider`; no vendor type in domain or API |
| Prompt packs | `wp-phase2.concierge.v1`, `wp-phase2.brand-dna.v1` |
| Allowed tools | read same-tenant workspace/session/messages and prior approved DNA; append planner replies; propose DNA |
| Prohibited tools/actions | web research, Color Intelligence, Curator, concepts/assets, Bliss matching, Alpha Auto, n8n authority, self-approval |

The shared worker executes two separately versioned role contracts. Splitting
it into two workers later requires a documented cost, quality, and security
rationale but no public API change.

## Authority

- Advertisers can act only inside the `advertiser_id` bound workspace.
- Operators/admins may act across advertisers; viewers cannot write.
- Clients cannot append `PLANNER` messages. Only server orchestration can.
- The Concierge may converse and ask clarifying questions. It cannot approve
  Brand DNA or write matching, review, or placement records.
- The Interpreter may emit a `PROPOSED` Brand DNA document and summary. It
  cannot approve it.
- Only an authenticated advertiser or chapel operator/admin may record an
  `APPROVE` or `REJECT` decision.
- Cross-tenant resource lookup returns 404, consistent with Phase 1.

## Durable records

### `WeddingPlannerAgentRun`

Append-oriented execution receipt with advertiser/workspace/session scope,
logical role, worker key, prompt/provider/model/adapter versions, trigger and
output message links, request and idempotency keys, status/outcome, bounded
error fields, timestamps, token counts, and estimated USD cost.

Statuses: `RUNNING` → `SUCCEEDED` or `FAILED`. Terminal runs are not reopened.
The unique `(SourceSystem, IdempotencyKey)` pair makes a successful or failed
run replayable without another provider call.

### `WeddingPlannerBrandDnaVersion`

Immutable version snapshot with advertiser/workspace scope, monotonic
`VersionNumber`, `SchemaVersion`, structured `DocumentJson`, summary, producing
agent run, status, idempotency, and creation time.

Phase 2 schema `brand-dna.v1` may contain brand voice, audience, offers and
services, markets, tone, language do/don't guidance, compliance notes, and
open questions. It does not contain Color Intelligence palettes, creative
concepts, generated assets, or match data.

Statuses: `PROPOSED`, `APPROVED`, `REJECTED`, `SUPERSEDED`.

### `WeddingPlannerBrandDnaDecision`

Immutable human decision with actor, rationale, source/idempotency, outcome,
and timestamp. Approval sets the workspace's current approved pointer.
Approving a later version supersedes the former approved version without
deleting it. Rejected versions never become current.

## Provider contract

`IWeddingPlannerAiProvider.CompleteAsync` accepts a vendor-neutral request:
logical role, prompt pack version, authorized message context, response format,
and output limit. It returns text or structured JSON plus provider/model
identity, worker key, request id, token counts, and estimated cost.

The shipped default worker is deterministic and local so tests and development do not
send advertiser data to an external provider. A production `OpenAiCompatible`
HTTP adapter may replace it through dependency injection and secret-backed
`WeddingPlannerAi` configuration (`Provider`, `BaseUrl`, `ApiKey`, `ModelId`,
optional `WorkerKey`, timeout, and per-million-token prices). Provider failures
produce a durable failed run; they do not fabricate a planner reply or Brand DNA
version. Secrets are never stored on runs or audit rows. Agent-run `WorkerKey`
comes from the selected provider (and completion result), not a hardcoded local
identity.

## Flow

### Concierge turn

1. authorize the session;
2. replay an existing run when the idempotency key already exists;
3. append the authenticated human message;
4. persist a `RUNNING` Concierge run;
5. build context only from same-session database messages;
6. invoke the provider;
7. append the server-owned `PLANNER` reply;
8. complete the run with usage/cost metadata and audit events.

The human message remains durable if provider execution fails. No synthetic
planner message is inserted on failure.

### Brand DNA interpretation

1. authorize the workspace and select its authorized conversation history;
2. replay by idempotency key when present;
3. invoke the Interpreter with the `brand-dna.v1` response contract;
4. validate and canonicalize JSON;
5. insert the next immutable `PROPOSED` version;
6. complete the run and audit proposal creation.

### Human decision

An `APPROVE` or `REJECT` request inserts a decision record. Approval changes
only status/pointer metadata; document and summary payloads are never edited.
All transitions and replays are audited.

## API

Phase 1 routes remain unchanged. Add:

- `POST /api/wedding-planner/sessions/{id}/turns`
- `GET /api/wedding-planner/sessions/{id}/agent-runs`
- `GET /api/wedding-planner/agent-runs/{id}`
- `POST /api/wedding-planner/workspaces/{id}/brand-dna/interpret`
- `GET /api/wedding-planner/workspaces/{id}/brand-dna`
- `GET /api/wedding-planner/brand-dna/{id}`
- `POST /api/wedding-planner/brand-dna/{id}/decisions`

No PUT, PATCH, or DELETE route is provided for a Brand DNA payload.

## Failure and audit

- 401 unauthenticated; 403 authenticated identity without write authority;
  404 missing or cross-tenant resource; 400 invalid input/transition.
- Provider failure returns 502 after the failed run is persisted.
- Run rows store role, worker, prompt, provider, model, adapter, request id,
  usage, cost, status, and bounded error details.
- Audit actions cover run start/success/failure, DNA proposal, approval,
  rejection, and replay.
- Secrets and raw provider credentials are never stored.

## Tests

- Concierge appends human then planner messages with monotonic sequence.
- Replaying a turn does not call the provider or append/bill twice.
- Clients still cannot forge `PLANNER` messages.
- Interpreter creates a valid immutable `PROPOSED` `brand-dna.v1` version.
- Version numbers increase; old documents remain readable.
- Human approval sets the current pointer; a later approval supersedes it.
- Reject and illegal transition behavior is deterministic.
- Advertiser A cannot access or invoke runs, DNA, or decisions for B.
- Anonymous writes are 401; viewer writes are 403.
- Usage, model, prompt, worker, request, and audit evidence persists.
- Existing Bliss and Wedding Planner Phase 1 tests remain green.
- Architecture tests prove no Phase 2 source references deterministic match
  evaluation, Alpha Auto, or n8n.

## Explicit exclusions

Alpha Color Intelligence, Curator research, concepts/prototypes, image
generation, mature creative-department roles, Chaperone/QA AI, Bliss campaign
handshake, measurement/learning, GHL coupling, n8n authority, Alpha Auto, and
all changes to deterministic Bliss scoring, review, or placement.

## Acceptance gate

Contract → implementation → automated tests → evidence → human review and
acceptance. Phase 2 does not auto-advance to Phase 3.
