# Wedding Planner Phase 2 — Evidence

## Result

Phase 2 Conversation + Brand DNA is implemented against
`PHASE-2-ENGINEERING-CONTRACT.md`.

The implementation adds two logical AI roles on one shared executable worker:

1. Wedding Planner / Concierge
2. Brand DNA Interpreter

It does not advance to Phase 3.

## Durable model

Migration:
`20260922080549_WeddingPlannerPhase2ConversationBrandDna`

The migration was applied successfully from an empty PostgreSQL database after
all accepted Bliss and Wedding Planner Phase 1 migrations.

New durable records:

- `WeddingPlannerAgentRuns`
- `WeddingPlannerBrandDnaVersions`
- `WeddingPlannerBrandDnaDecisions`
- nullable current-approved Brand DNA pointer on the workspace

All tenant relationships restrict deletes. Run, version, and decision writes
use idempotency constraints. Brand DNA versions are unique by workspace and
version number. Decision rationale is required. Document JSON and summary have
no update endpoint and remain unchanged when status/pointer metadata changes.

## Agent/provider evidence

`IWeddingPlannerAiProvider` keeps vendor types outside the domain and API.

- Development and CI use a deterministic local worker with no network calls.
- Non-development startup requires `OpenAiCompatible`.
- The production adapter uses a chat-completions-compatible HTTP contract and
  secret-backed `BaseUrl`, `ApiKey`, and `ModelId`.
- Interpreter requests require JSON response format.
- Provider errors are bounded and do not include response bodies or secrets.
- Every completed run records logical role, worker, prompt pack, provider,
  model, adapter, provider request id, tokens, estimated cost, and outcome.
- Failed provider calls leave the human message and a durable failed run, but
  do not fabricate a planner message or Brand DNA proposal.

## Authority and tenancy

- The browser cannot append `PLANNER` messages through the Phase 1 message API.
- Only server orchestration can persist Concierge replies.
- The Interpreter creates `PROPOSED` versions only.
- An authenticated advertiser or operator/admin must explicitly APPROVE or
  REJECT with a rationale.
- Approval updates current/superseded metadata; it does not rewrite DNA.
- OIDC advertiser A receives 404 for advertiser B's sessions, turns, runs,
  versions, interpretation requests, and decisions.
- Anonymous writes are 401 and viewer writes are 403.

## API and UI

Phase 2 adds:

- `POST /api/wedding-planner/sessions/{id}/turns`
- `GET /api/wedding-planner/sessions/{id}/agent-runs`
- `GET /api/wedding-planner/agent-runs/{id}`
- `POST /api/wedding-planner/workspaces/{id}/brand-dna/interpret`
- `GET /api/wedding-planner/workspaces/{id}/brand-dna`
- `GET /api/wedding-planner/brand-dna/{id}`
- `POST /api/wedding-planner/brand-dna/{id}/decisions`

The public experience enables live conversation only for an authenticated,
writable identity with an `advertiser_id` claim. It opens and resumes durable
workspace/session records, renders only server-returned messages, and exposes
Brand DNA proposal and decision controls. Authentication-disabled development
does not silently bind the public browser to a test advertiser.

The operations console lists Concierge runs and Brand DNA versions and exposes
operator interpretation/decision controls while preserving Phase 1 forms.

## Automated verification

Command:

`dotnet test Bliss.Tests/Bliss.Tests.csproj --nologo`

Result:

- passed: 136
- failed: 0
- skipped: 0

Coverage includes:

- turn ordering, metadata, replay, failure, and anti-forgery;
- immutable interpretation versions, approval, supersession, and rejection;
- required human rationale;
- cross-tenant, anonymous, and viewer boundaries;
- EF foreign keys and unique indexes;
- OpenAI-compatible request shape, JSON mode, cost mapping, and secret-safe
  errors with a fake HTTP handler;
- public and operations frontend wiring;
- architecture guards against deterministic matching, Alpha Auto, n8n, and
  vendor SDK coupling.

## PostgreSQL workflow smoke test

A real PostgreSQL workflow completed:

1. open workspace;
2. create session;
3. execute Concierge turn;
4. create `brand-dna.v1` proposal;
5. approve with human rationale.

Observed result:

- Concierge run `SUCCEEDED`;
- response actor `PLANNER`;
- token usage recorded;
- Brand DNA v1 started `PROPOSED`;
- human decision changed it to `APPROVED`;
- workspace current-approved pointer was set.

## Browser verification

Desktop browser validation confirmed:

- the public Phase 2 heading and explicit authentication gate;
- no automatic development advertiser binding;
- disabled public composer and Brand DNA controls without an advertiser claim;
- operations console with no backend error cards;
- TEST Dental Manila workspace/session resume;
- durable operator message;
- visible successful Brand DNA Interpreter run;
- Brand DNA v1 `APPROVED` and `CURRENT APPROVED`;
- clean responsive layout with no overlap or horizontal overflow.

A 38-second walkthrough artifact records the public and operations states with
no HTTP error, missing image, blank data card, or long dead segment.

## Preserved boundaries

No Phase 2 source calls or changes deterministic Bliss evaluation, formation,
review, or placement. Alpha Auto, n8n authority, Color Intelligence, Curator,
concepts, generated assets, Chaperone/QA, Bliss handshake, and measurement
learning remain out of scope.
