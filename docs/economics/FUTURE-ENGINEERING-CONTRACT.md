# Economics & Rate Intelligence — Future Engineering Contract (reserved)

**Authorization:** not issued. This document is a placeholder so the
first real implementation contract does not have to rediscover the
requirement.

Do not implement this contract as part of Wedding Planner Phase 1–9 or
Bliss matching follow-on work unless a later accepted contract explicitly
opens Economics Phase 1.

## Objective (when authorized)

Persist structured economics and produce explainable rate **ranges**
from data + versioned rules, with AI research as assisted input only.

## Required behavior (draft)

1. Country and city/metro are rows in `GeographicMarkets`. No
   per-launch-city calculation classes.
2. Pricing models are catalog data (`CPM`, `CPV`, `FLAT_PLACEMENT`,
   `FIXED_CAMPAIGN`, `SPONSORSHIP`, `HOST_READ`, `CPA`, `CPL`, `CPS`,
   `HYBRID`).
3. Recommendations store low, target, high, currency, basis, estimated
   impressions, confidence, as-of time, pricing-rule version, and
   factor/source children.
4. Observations are append-only with provenance and
   `VERIFIED` / `ESTIMATED` / `INFERRED` / `UNKNOWN`.
5. Quotes are versioned. Negotiation writes a new version plus outcome.
6. Compensation rule versions are independent of market-value
   recommendations. No compiled default split.
7. Duration is an input. The engine must not multiply a one-minute rate
   by duration unless a **named** pricing-rule version documents that
   model, which is not the default.
8. Wedding Planner is a client of the .NET API. Matching is not.
9. n8n may enqueue research; .NET validates and stores observations.
10. Existing Bliss and Wedding Planner tests remain green; no Alpha Auto
    coupling.

## Explicit exclusions (until this contract is accepted)

- EF migrations for economics tables
- HTTP `/api/economics/*`
- n8n research workflows
- AI-generated rates written without observations
- Seeded production CPMs or launch-city rate cards
- Settlement, payouts, payment providers
- Changes to `DeterministicRuleEvaluator`
- Wedding Planner composer / AI conversation

## Acceptance criteria (when a later contract copies this)

- A Manila and a Medellín creator with similar subscriber counts can
  receive different ranges when market observations differ
- A 10-minute slot is not automatically 10× a 1-minute slot under the
  default rule version
- A recommendation without sources cannot be `HIGH` confidence
- Historical observations remain after a newer benchmark is inserted
- Compensation illustration uses a versioned share document, not 20/80
  constants
