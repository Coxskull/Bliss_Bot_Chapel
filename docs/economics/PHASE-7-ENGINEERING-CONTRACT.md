# Economics Phase 7 — Public Research Orchestration

## Authorization

Authorized by the owner on 2026-09-23 after Economics Phase 6 was
accepted and merged. Phase 8 must not begin until Phase 7 evidence is
reviewed and accepted.

## Objective

Allow n8n and approved AI extraction services to assist public-market
research while .NET validates every staged candidate and PostgreSQL
retains the durable run, source, candidate, review, and promoted
observation history.

## Required behavior

1. Persist `EconomicsResearchRun`, `EconomicsResearchCandidate`, and
   `EconomicsResearchReviewDecision` records.
2. Queue a bounded research request with market, metric, optional
   industry/platform/inventory context, requester, and idempotency key.
3. Accept extracted candidates only through a controlled staging API.
   Candidates preserve source name, HTTPS URL, source type,
   publication/retrieval dates, extraction model, raw payload, proposed
   value/range, currency, confidence, and verification status.
4. AI/n8n candidates can be `ESTIMATED`, `INFERRED`, or `UNKNOWN`.
   They cannot submit `VERIFIED` or `HIGH`.
5. .NET validates shape, range consistency, URL scheme, bounded payload,
   run context, and allowed statuses before staging.
6. A human reviewer explicitly accepts or rejects each candidate.
7. Acceptance creates a new append-only `MarketBenchmarkObservation`
   and a durable `ResearchSource` link. It never overwrites an existing
   observation and never silently upgrades the candidate to `VERIFIED`.
8. Rejection preserves the candidate and rationale without creating an
   observation.
9. Every queue, stage, and review write is idempotent.
10. n8n owns fetch/retry/orchestration only. AI extracts candidate
    fields only. Neither owns truth, pricing rules, recommendations,
    quotes, compensation, or settlement.

## Research lifecycle

```text
QUEUED run
   ↓ n8n fetch + approved AI extraction
STAGED candidate (not truth)
   ↓ .NET validation
AWAITING_REVIEW
   ↓ human decision
PROMOTED → append-only observation
or
REJECTED → retained evidence, no observation
```

## n8n deliverable

Provide an importable, disabled-by-default workflow template under
`n8n/workflows/`. It must:

- receive a bounded run id
- load run context from the .NET API
- call only environment-configured approved research/extraction
  endpoints
- constrain AI output to candidate status
- post candidates to the .NET staging API
- contain no credentials, production URLs, rate calculations, or
  database writes

Hosted n8n credentials and provider configuration remain outside git.

## API

- `GET /api/economics/research-runs`
- `GET /api/economics/research-runs/{id}`
- `POST /api/economics/research-runs`
- `POST /api/economics/research-runs/{id}/candidates`
- `POST /api/economics/research-candidates/{id}/review`

Queue/stage POSTs require write authority. Review POST requires reviewer
authority.

## Explicit exclusions

- autonomous web crawling from the API process
- arbitrary user-supplied fetch destinations
- provider credentials committed to git
- AI-created `VERIFIED` or `HIGH` observations
- direct n8n/AI writes to PostgreSQL
- recommendation, quote, compensation, or settlement execution
- Wedding Planner integration
- changes to matching or placement

## Acceptance criteria

- staged AI output is visibly not permanent truth
- `VERIFIED` or `HIGH` candidate submissions fail validation
- malformed/insecure source URLs and incomplete ranges fail validation
- accepted candidate creates a sourced append-only observation
- rejected candidate creates no observation
- candidate raw payload and extraction model remain auditable
- retries replay the same run, candidate, and review decision
- workflow template contains no secret or hard-coded production host
- full suite and live PostgreSQL verification pass
- API/database/browser snapshots and recording are preserved under
  `docs/economics/evidence/phase7/`
