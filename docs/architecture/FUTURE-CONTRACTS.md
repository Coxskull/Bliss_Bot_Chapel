# Future acquisition contracts

**Status:** catalog only. None of these contracts are authorized.

Do not implement an item on this list because it appears here. The
Bliss package must reach its required acceptance point first. After
that, each item still needs its own Engineering Contract under
`ENGINEERING-CONTRACT-RULE.md`.

Dependency order below is planning guidance so later contracts do not
invert the acquisition flow. A planning review may revise it. Revision
of this list is not permission to start.

## Not open

| Contract | Owns | Depends on before it can be useful |
| --- | --- | --- |
| Subscription & Infrastructure Ledger (software) | Persist the register that `SUBSCRIPTION-LEDGER.md` already keeps by hand | None. The markdown register exists now. The software ledger waits for its own contract |
| Factory Budget Controller | Daily, monthly, provider, generation, and prospect budgets; selective behavior at the threshold | Cost classification rules. Should exist before any contract that spends on generation |
| Fishing Fleet advertiser discovery | Inexpensive discovery of established local and regional businesses | Budget policy if the discovery path spends money. Public-source research otherwise |
| Opportunity Intelligence Engine | Light research, score, priority, production-investment decision | Advertiser discovery. Separate from decision-maker confidence |
| Decision-Maker & Contact Intelligence | Buying role, evidence, confidence, freshness, contact fallback, provider abstraction | A discovered and scored business. Enrichment spend only after the ledger review |
| Alpha Source Media Library | Qualified reusable clips and metadata | None for the catalog itself |
| Source Media Integrity Engine | Fingerprints and duplicate rules | Lands with the library. A library that counts duplicates is not acceptable |
| Media Supply & Productivity Control | Quota, replacement, Daily Operations, Mission Control fuel gauge, market coverage | Library + integrity. System evidence, not self-certification |
| Deterministic Video Composition Engine | .NET + FFmpeg composite, real QR, automated disclosure, mechanical QA | Approved source media and reusable components. Zero AI calls for QR and routine overlay |
| Prospect Demonstration Engine | One strong demonstration per eligible prospect; further spots only on engagement | Opportunity score, source media, composition, cache |
| Dynamic Prospect Sales Room | One application, many prospect pages, Interactive AI Sales Room | Verified prospect facts. Pricing calls Economics; it does not invent prices |
| Alpha Communication Router | Policy, eligibility, channel adapter | A contact tier and suppression rules. No send without eligibility |
| Unified Conversation Ledger | One relationship history across channels | Router, or at least a channel that writes here |
| Prospect Engagement Brain | Signal measurement and the decisions those signals unlock | Pages and outreach events to measure |
| AI Business Development | Approved conversation and qualification | Ledger, policy, and human escalation. Cannot set `WON` |
| Advertiser Demand Map | Prospective interest versus authorized budget, and remaining creator demand | Qualification signals |
| Demand-Driven Creator Discovery | Fishing priority from demand, not random recruiting | Demand map |
| Creator Prospect Presentation | Creator-facing demonstration | Creator discovery and the same cost rules as advertiser demos |
| Acquisition → Bliss Integration | Hand qualified advertisers and creators to existing Bliss matching | Both sides of the marketplace. Does not replace `DeterministicRuleEvaluator` |

Creative DNA, Wedding Planner conversation, and the six logical creative
responsibilities stay on the Wedding Planner phase sequence already
described in `docs/wedding-planner/`. Future creative contracts follow
the consolidation rule in `MASTER-ARCHITECTURE.md`: six logical
responsibilities are not six API calls. They are not opened by this
catalog.

## Suggested sequence

This is the cheapest order that respects the governing rule. It is not
a sprint plan.

1. Keep Bliss on its acceptance path. Do not start this catalog early
   to get ahead of it.
2. Keep the subscription register current before any paid dependency.
3. Put budget control in place before speculative generation.
4. Land source-media library, integrity, and productivity together.
5. Discover and score advertisers before researching decision-makers.
6. Verify people and contacts before personalizing to them.
7. Composite with software. Generate with AI only where generation is
   required. Reuse market intelligence and approved media first.
8. Present on one landing application, then route communication, then
   remember the conversation, then measure engagement.
9. Qualify, record demand, fish for creators against that demand, and
   only then hand the pair to Bliss.
10. Humans negotiate, authorize, and close.

## Explicitly out of scope until a named contract

Fishing Fleet crawlers, contact-enrichment purchases, generative video
for routine compositing, per-prospect websites, a second CRM, a second
workflow engine, a second messaging suite, personal ChatGPT as a
worker, and any path that lets AI approve, price, contract, or move
money.
