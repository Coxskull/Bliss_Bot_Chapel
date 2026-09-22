# Wedding Planner Agent / Bot Workforce Allocation

Logical roles are job positions. They are not automatically separate
models, subscriptions, servers, or processes.

## Phase 1 (implemented)

- Logical AI roles: 0
- Executable AI workers: 0
- Reason: infrastructure, tenancy, durable workspace/session/message/audit.

## Phase 2 (implemented)

- Logical AI roles: 2 — Wedding Planner / Concierge and Brand DNA Interpreter.
- Executable workers: 1 shared worker selected behind
  `IWeddingPlannerAiProvider`.
- Development/CI worker: deterministic local adapter; no network.
- Production worker: OpenAI-compatible HTTP adapter configured through secrets.
- Routing: deterministic by endpoint and prompt pack; no intelligent router.
- Human authority: workers may converse and propose immutable Brand DNA
  versions, but only an authenticated human may approve or reject.
- Recorded evidence: worker, provider, model, adapter, prompt pack, request,
  tokens, estimated cost, status, outcome, and bounded errors.

The implementation uses the low end of the blueprint's 1–2 worker range.
Splitting the roles later requires a documented cost, quality, and security
rationale.

## Phase 3 (implemented)

- New logical AI roles: 0.
- New executable AI workers: 0.
- Alpha Color Intelligence is pure deterministic .NET software using
  `aci.hsl.v1`.
- Inputs are explicit human seed colors. Approved Brand DNA is provenance
  only and is never parsed to invent colors.
- Outputs are immutable `color-profile.v1` versions with canonical input
  SHA-256 and WCAG contrast arithmetic.
- Human approval remains required; Color Intelligence cannot self-approve.
- No provider, prompt, model, token, cost, or agent-run record is created.

## Phase 4 (implemented)

- New logical AI roles: 8 Curator research responsibilities.
- New executable worker profiles: exactly 3 per successful job.
- `CURATOR_RESEARCH_V1` covers market, audience, competitor, and
  channel/format research.
- `CURATOR_EVIDENCE_V1` covers evidence analysis and source verification.
- `CURATOR_SYNTHESIS_RISK_V1` covers claims-risk review and research
  synthesis.
- Successful jobs create exactly 3 agent-run receipts and exactly 8 durable
  role contributions; they do not create eight fake model calls.
- Source acquisition is provider-neutral. Local Development/CI evidence is
  visibly `SYNTHETIC` and uses `.invalid` hosts; Production requires the
  remote HTTP adapter.
- Factual, inferential, and risk findings require citations to the validated
  source catalog. The app never fetches returned citation URLs.
- Humans alone approve/reject immutable research reports. Approval is
  research approval only.

## Later starting allocations

- Phase 5: four workshop roles on about 2–4 workers.
- Phase 6: about thirteen primary logical roles on about 5–7 workers.
- Phase 7: three control roles on about 1–2 AI workers plus deterministic
  rules and human authority.
- Phase 8: no new AI agents.
- Phase 9: three intelligence roles on about 1–2 workers.

Any change from these starting allocations must record:

current recommendation → proposed change → engineering reason →
cost/quality/security effect.
