# Wedding Planner Phase 3 — Evidence

## Result

Phase 3 Alpha Color Intelligence is implemented against
`PHASE-3-ENGINEERING-CONTRACT.md`.

New AI roles: **0**. New executable AI workers: **0**.

Color Intelligence is deterministic .NET software. It does not call
`IWeddingPlannerAiProvider`, create `WeddingPlannerAgentRun` rows, parse Brand
DNA prose to invent colors, or advance to Phase 4.

## Deterministic contract

`AciHslV1` implements:

- `#RGB` / `#RRGGBB` validation and uppercase canonicalization;
- sRGB↔HSL geometry;
- exact sRGB linearization and relative luminance;
- WCAG contrast ratios and threshold flags;
- hue+180 secondary and hue+30 accent derivation;
- black/white on-color selection by higher contrast;
- deterministic neutral ladder from the declared background;
- stable `color-profile.v1` JSON;
- canonical input JSON and lowercase SHA-256 evidence.

Approved Brand DNA id/version is provenance only. Its document and summary are
never read by the color algorithm. Human notes are stored in the immutable
profile document but excluded from the input hash.

WCAG figures are arithmetic evidence, not legal/accessibility certification.
HSL offsets are geometry, not psychological or brand-strategy claims.

## Durable model

Migration:
`20260922085710_WeddingPlannerPhase3ColorIntelligence`

The full migration chain was applied successfully to an empty PostgreSQL
database through Phase 3.

New durable records:

- `WeddingPlannerColorProfileVersions`
- `WeddingPlannerColorProfileDecisions`
- nullable current-approved color profile pointer on the workspace

Profile versions are immutable computation receipts. They store schema,
algorithm, approved Brand DNA provenance, canonical input JSON/SHA-256,
output JSON, summary, actor, status, idempotency, and creation time. They do
not store provider/model/prompt/token/cost fields.

Decisions are append-only and require a human rationale. Approval updates
status/current-pointer metadata only. A later approval supersedes the former
current profile without rewriting either payload.

## API and authority

Phase 3 adds:

- `POST /api/wedding-planner/workspaces/{id}/color-profiles/compute`
- `GET /api/wedding-planner/workspaces/{id}/color-profiles`
- `GET /api/wedding-planner/color-profiles/{id}`
- `POST /api/wedding-planner/color-profiles/{id}/decisions`

Controls preserve Phase 1–2 authority:

- compute requires current-approved Brand DNA;
- profiles begin `PROPOSED`;
- authenticated humans alone may `APPROVE` or `REJECT`;
- idempotent replays do not allocate another version;
- advertiser A receives 404 for advertiser B's profiles/actions;
- anonymous writes are 401 and viewer writes are 403;
- no PUT/PATCH/DELETE payload mutation route exists.

## Automated verification

Command:

`dotnet test Bliss.Tests/Bliss.Tests.csproj --nologo`

Result:

- passed: 167
- failed: 0
- skipped: 0

Coverage includes:

- canonical and invalid hex inputs;
- black/white luminance and 21.00/1.00 contrast fixtures;
- stable red-seed palette golden vector;
- HSL offset derivation, defaults, neutrals, on-colors, and threshold flags;
- byte-stable document/input JSON and SHA-256;
- notes excluded from SHA but retained in output;
- approved Brand DNA prerequisite;
- idempotent compute, monotonic versions, immutability, approval,
  supersession, rejection, and rationale;
- OIDC tenant/anonymous/viewer boundaries;
- EF foreign keys and unique indexes;
- frontend server-document-only rendering;
- architecture guards against AI providers, agent runs, deterministic Bliss
  matching, Alpha Auto, and n8n.

JavaScript syntax checks pass for public and operations bundles.

## PostgreSQL workflow smoke test

A real PostgreSQL workflow completed:

1. open workspace and planning session;
2. create and approve Brand DNA;
3. compute profile from primary `#36c`;
4. approve profile with human rationale.

Observed:

- canonical primary `#3366CC`;
- deterministic secondary `#CC9933`;
- schema `color-profile.v1`;
- algorithm `aci.hsl.v1`;
- input SHA-256 recorded;
- contrast evidence recorded;
- profile moved `PROPOSED` → `APPROVED` and became current;
- Color Intelligence agent-run count: **0**.

## Browser verification

The running PostgreSQL-backed application was exercised in Chrome at a
1,440 × 1,000 desktop viewport.

Public advertiser surface:

- Phase 3 heading, deterministic/no-AI copy, five seed inputs, notes,
  compute/version controls, and prerequisite status render without horizontal
  overflow;
- without an authenticated writable advertiser, compute remains disabled and
  no test advertiser is bound;
- the empty profile/decision region remains hidden until a server profile
  exists.

Operations surface:

- no backend error cards appeared;
- TEST Dental Manila loaded its approved Brand DNA and color profiles;
- profile v1 displayed stored swatches, contrast ratios/AA flags, schema,
  algorithm, complete input SHA-256, Brand DNA provenance, and both
  disclaimers;
- the compute form created v2 from primary `#E11D48` with omitted optional
  seeds and note `Browser Phase 3 validation`;
- a confirmed human approval with rationale made v2 `APPROVED · CURRENT
  APPROVED` and moved v1 to `SUPERSEDED`;
- v2 displayed canonical/derived/default palette roles and computed contrast
  evidence from stored server JSON;
- no horizontal overflow was observed.

## Preserved boundaries

Phase 3 does not modify Bliss evaluation, formation, review, placement,
verification, Alpha Auto, or Phase 2 AI role contracts. Curator research,
concepts, generated assets, Chaperone/QA, campaign handshake, and measurement
learning remain out of scope.
