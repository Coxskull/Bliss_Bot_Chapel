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

## Phase 5 (implemented)

- New logical AI roles: 4 — `BRAND_STRATEGIST`, `ART_DIRECTOR`,
  `COPYWRITER`, `PRODUCTION_ARTIST`.
- New executable worker profiles: exactly 3 per successful job
  (`CONCEPT_STRATEGY_V1`, `CONCEPT_CREATIVE_V1`,
  `PROTOTYPE_PRODUCTION_V1`).
- Exact 4→3 mapping: strategy owns Brand Strategist; creative owns Art
  Director + Copywriter; production owns Production Artist. Successful jobs
  create exactly 3 agent-run receipts and exactly 4 immutable role
  contributions; they do not create four fake model calls.
- Prerequisites: current-approved Brand DNA, Color Profile, and Research
  Report are required and pinned. Brand DNA and color constrain creative
  direction only; only pinned research source ids may support factual
  claims.
- Safe low-fi prototypes: structured `prototype-spec.v1` with exact channel
  canvases, bounded regions/templates/text refs/palette refs, and asset
  placeholders only — no image generation, media URL fetch, or arbitrary
  markup. UI uses an escaped fixed renderer.
- Humans alone approve/reject immutable concept packages and select
  `selectedConceptId` on APPROVE. Approval is concept-direction only.
- Out of scope: campaign-ready state, Chaperone/QA, asset/revision
  production, and Bliss matching writes.

## Phase 6 (implemented)

- New logical AI roles: exactly 13 —
  `CREATIVE_DIRECTOR`, `CAMPAIGN_STRATEGIST`, `AUDIENCE_STRATEGIST`,
  `OFFER_STRATEGIST`, `CHANNEL_STRATEGIST`, `VISUAL_DESIGNER`,
  `LAYOUT_DESIGNER`, `TYPOGRAPHY_DESIGNER`, `IMAGE_PROMPT_DESIGNER`,
  `HEADLINE_SPECIALIST`, `BODY_COPY_SPECIALIST`, `CTA_SPECIALIST`,
  `VARIANT_PRODUCER`.
- New executable worker profiles: exactly 6 per successful job.
- Exact 13→6 mapping:
  - `CREATIVE_DIRECTION_V1` → `CREATIVE_DIRECTOR`, `CAMPAIGN_STRATEGIST`
  - `STRATEGY_ADAPTATION_V1` → `AUDIENCE_STRATEGIST`, `OFFER_STRATEGIST`,
    `CHANNEL_STRATEGIST`
  - `VISUAL_SYSTEM_V1` → `VISUAL_DESIGNER`, `LAYOUT_DESIGNER`,
    `TYPOGRAPHY_DESIGNER`
  - `IMAGE_DIRECTION_V1` → `IMAGE_PROMPT_DESIGNER`
  - `COPY_SYSTEM_V1` → `HEADLINE_SPECIALIST`, `BODY_COPY_SPECIALIST`,
    `CTA_SPECIALIST`
  - `VARIANT_PRODUCTION_V1` → `VARIANT_PRODUCER`
- Successful jobs create exactly 6 agent-run receipts and exactly 13
  immutable role contributions; they do not create thirteen fake model
  calls. Phase 5 roles remain pinned concept provenance only.
- Separate non-AI asset provider:
  `IWeddingPlannerCreativeAssetProvider` (Local deterministic +
  `RemoteHttp` raw PNG). Not an AI worker/run. PNG-only ≤2 MiB, max 4,
  exact canvas, strict IHDR/IDAT/IEND CRC/zlib/filter validation, `bytea`
  transactional storage, authenticated same-origin content endpoint with
  no-store/nosniff/ETag; no URL/base64/SVG/HTML.
- `INITIAL` / `REVISION` one-parent immutable jobs; revision reruns all
  six profiles. Human `APPROVE` selects variant decision-only; draft
  creative approval only. No partial packages.
- Out of scope: Phase 7 Chaperone/QA/escalation, Phase 8
  campaign-ready/handshake, Bliss matching writes, measurement, Alpha
  Auto, and n8n/GHL.

## Phase 7 (implemented)

- New logical control roles: exactly 3 —
  `CREATIVE_CHAPERONE`, `QA_INSPECTOR`, `HUMAN_ESCALATION_STEWARD`.
- New executable AI profiles: exactly 2 per successful job
  (`CHAPERONE_REVIEW_V1`, `QA_INSPECTION_V1`).
- Exact 3→2 + rules/humans mapping: Chaperone and QA Inspector are AI
  contributions; Human Escalation Steward is `RULES_HUMAN` only
  (deterministic routing + reviewer/operator/admin authority;
  `ProducingAgentRunId` null; no third fake agent run).
- Deterministic `qa-rules.v1` runs before AI (authoritative
  PASS/WARN/BLOCK; AI cannot downgrade). Structural integrity /
  provenance only — not semantic, legal, visual, or campaign
  certification.
- Prerequisites: current-approved Phase 6 selected variant + exactly one
  PNG; all package/decision/variant/asset/DNA/color/research pins.
  AI never receives image bytes/base64/URL/pixels; humans confirm via
  same-origin PNG.
- Dedicated review authority: advertisers create/read only; reviewer /
  operator / admin decide and resolve returns; `WAIVE_AND_ACCEPT` is
  operator/admin only. Later creative APPROVE clears the QA pointer only.
- Out of scope: Phase 8 handshake/campaign-ready, Bliss writes, visual
  AI/OCR, legal/accessibility certification, measurement, Alpha Auto, and
  n8n/GHL. Phase 7 does not auto-advance.

## Phase 8 (implemented)

- New logical AI roles: 0.
- New executable AI workers: 0.
- Deterministic Bliss handshake / campaign-readiness only.
- Operator/admin alone may MARK_CAMPAIGN_READY / REVOKE_CAMPAIGN_READY.
- Placement remains PLANNED — not activation or reservation.

## Phase 9 (implemented)

- New logical intelligence roles: exactly 3 —
  `PERFORMANCE_ANALYST`, `LEARNING_SYNTHESIZER`, `OPTIMIZATION_ADVISOR`.
- New executable worker profiles: exactly 2 per successful job.
- Exact 3→2 mapping:
  - `PERFORMANCE_ANALYSIS_V1` → `PERFORMANCE_ANALYST`
  - `LEARNING_SYNTHESIS_V1` → `LEARNING_SYNTHESIZER`,
    `OPTIMIZATION_ADVISOR`
- Successful jobs create exactly 2 agent-run receipts and exactly 3
  immutable role contributions; they do not create three fake model
  calls.
- Human-supplied aggregate observations only, with exact attestation.
  Labels: `HUMAN-SUPPLIED AGGREGATES`, `ASSOCIATION — NOT CAUSATION`,
  `ADVISORY ONLY`. Linked placement remains PLANNED and does not prove
  delivery.
- `measurement-rules.v1` runs before AI (10 PASS/BLOCK). Humans alone
  ACCEPT/REJECT advisory reports (operator/admin). Recommendations
  cannot activate, revise creative, change spend, or mutate Phase 1–8
  artifacts.
- Phase 9 is the final phase listed in Master Blueprint V1.1; it does
  not invent a Phase 10.

## Later non-phase modules (not Wedding Planner workforce)

- Bliss Economics & Rate Intelligence Engine is a **separate** Bliss
  Chapel bounded context. See
  `docs/bliss-economics/FUTURE-BOUNDED-CONTEXT.md`.
- Wedding Planner may later **ask** that engine for explainable rate
  ranges. Concierge, Curator, creative, QA, handshake, and Phase 9
  learning workers must not become pricing authorities.
- Compensation splits (including any historical 20/80 example) are not
  Wedding Planner constants.

Any change from these starting allocations must record:

current recommendation → proposed change → engineering reason →
cost/quality/security effect.
