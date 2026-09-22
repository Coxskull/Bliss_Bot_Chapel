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

## Later starting allocations

- Phase 3: Alpha Color Intelligence is deterministic software.
- Phase 4: eight Curator responsibilities on about three workers
  (research, analysis/evidence, synthesis/risk).
- Phase 5: four workshop roles on about 2–4 workers.
- Phase 6: about thirteen primary logical roles on about 5–7 workers.
- Phase 7: three control roles on about 1–2 AI workers plus deterministic
  rules and human authority.
- Phase 8: no new AI agents.
- Phase 9: three intelligence roles on about 1–2 workers.

Any change from these starting allocations must record:

current recommendation → proposed change → engineering reason →
cost/quality/security effect.
