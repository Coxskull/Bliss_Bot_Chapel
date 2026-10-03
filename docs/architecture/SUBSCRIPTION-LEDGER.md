# Alpha Subscription & Infrastructure Ledger

**Status:** authoritative register of record. Not a purchasing approval.
Not a new billing system.

The register exists so Alpha can answer:

- What are we already paying for?
- What capability does it provide?
- What do we actually need next?
- Can we eliminate a duplicate service?

Costs, account owners, plans, and renewal dates are **unrecorded** until
an operator writes them here from a billing source. This repository does
not contain invoices. Do not invent a price, plan, or owner.

No row in this register proposes a new paid subscription.

## Classification

| Class | Meaning |
| --- | --- |
| `FREE_SELF_HOSTED` | Ordinary software Alpha runs itself |
| `EXISTING_ALPHA_SUBSCRIPTION` | A subscription Alpha already pays for |
| `USAGE_BASED` | Metered use without a new recurring seat |
| `FREE_OR_DEVELOPMENT_TIER` | Free or development tier, with a known ceiling |
| `NEW_PAID_SUBSCRIPTION` | A new recurring charge. Empty until a capability-gap review is accepted |
| `DUPLICATIVE_REJECTED` | Rejected. Do not buy it to fill a gap Alpha or an existing service can fill |

## Capability-gap review

A `NEW_PAID_SUBSCRIPTION` row cannot be added until the proposal records:

| Field | Required answer |
| --- | --- |
| Required capability | What function is missing |
| Engineering Contract | Which accepted contract needs it |
| Provider | Who would be paid |
| Monthly subscription price | Recurring charge |
| Usage charges | Metered charges on top of the subscription |
| Alternatives considered | Including Alpha, .NET, PostgreSQL, FFmpeg, n8n orchestration, and any current subscription |
| Build or self-host alternative | What it would take to own the function |
| Why Alpha is insufficient | The specific gap, not a preference |
| Estimated monthly production cost | Subscription plus expected usage |
| Required date | When the gap blocks an accepted contract |
| Required or optional | Whether the contract can ship without it |

Order of consideration: existing Alpha infrastructure, then .NET /
PostgreSQL / FFmpeg / ordinary software, then an existing Alpha
subscription, then a free or usage-based gap-fill, and only then a new
recurring subscription.

Do not buy a prospecting suite, enrichment suite, personalized-video
suite, landing-page suite, chatbot suite, additional CRM, additional
workflow engine, additional video editor, or additional messaging system
because an interface or adapter exists.

## How to read a row

| Field | Rule |
| --- | --- |
| Service | Product or component name |
| Provider | Vendor, or `self-hosted` |
| Capability | The function Alpha actually uses it for |
| Account owner | Person or org account. `UNRECORDED` if not in a billing source |
| Current plan | Commercial plan. `UNRECORDED` if unknown. `n/a` if there is no plan |
| Monthly / annual cost | From an invoice or contract. Otherwise `UNRECORDED` |
| Usage-based charges | Metered fees. `none known` only when the repository shows no meter. Otherwise `UNRECORDED` |
| Renewal / billing cycle | Date or cycle. `UNRECORDED` if unknown |
| Engineering dependency | Repo path or bounded context. `none in this repo` if it is not coupled |
| Essential / optional | For the **current accepted** system, not for the long-term vision |
| Existing / proposed | `EXISTING`, `TEMPLATE_ONLY`, `REQUIRED_CONFIG`, `FUTURE`, or `REJECTED` |
| Alternatives | What was or should be considered before paying |
| Status | `OBSERVED`, `UNACTIVATED`, `REJECTED`, or `NOT_PROPOSED` |

## Register

### Bliss Chapel application

| Field | Value |
| --- | --- |
| Service | Bliss Bot Chapel (.NET 8 / ASP.NET Core) |
| Provider | self-hosted |
| Capability | Matching, operations API, Wedding Planner Phase 1, Economics Phases 1–9, production-posture gates |
| Account owner | n/a |
| Current plan | n/a |
| Monthly / annual cost | Compute cost `UNRECORDED` |
| Usage-based charges | none in application code |
| Renewal / billing cycle | n/a |
| Engineering dependency | `Bliss.Api`, `Bliss.Domain`, `Bliss.Infrastructure` |
| Essential / optional | Essential |
| Existing / proposed | `EXISTING` |
| Alternatives | Not applicable. This is Alpha's application |
| Status | `OBSERVED` |
| Class | `FREE_SELF_HOSTED` |

### PostgreSQL

| Field | Value |
| --- | --- |
| Service | PostgreSQL system of record |
| Provider | Supabase-compatible. Operators may use Supabase. The hosted plan is not in git |
| Capability | Authoritative business data |
| Account owner | `UNRECORDED` |
| Current plan | `UNRECORDED` |
| Monthly / annual cost | `UNRECORDED` |
| Usage-based charges | `UNRECORDED` |
| Renewal / billing cycle | `UNRECORDED` |
| Engineering dependency | `Bliss.Infrastructure` via EF Core and Npgsql |
| Essential / optional | Essential |
| Existing / proposed | `EXISTING` |
| Alternatives | Self-hosted PostgreSQL. Do not add a second database product for the same rows |
| Status | `OBSERVED` |
| Class | `EXISTING_ALPHA_SUBSCRIPTION` if the hosted Supabase plan is paid; otherwise record the actual class when the invoice is known. Do not guess |

### Identity provider

| Field | Value |
| --- | --- |
| Service | Provider-neutral OpenID Connect |
| Provider | Not selected in this repository |
| Capability | Operator and advertiser authentication outside Development |
| Account owner | `UNRECORDED` |
| Current plan | `UNRECORDED` |
| Monthly / annual cost | `UNRECORDED` |
| Usage-based charges | `UNRECORDED` |
| Renewal / billing cycle | `UNRECORDED` |
| Engineering dependency | `Bliss.Api` authentication configuration |
| Essential / optional | Essential outside Development |
| Existing / proposed | `REQUIRED_CONFIG` |
| Alternatives | Any OIDC issuer that can supply the Bliss role claims. Do not add a second identity product for the same users |
| Status | `OBSERVED` |
| Class | Record when a provider is chosen. No vendor is locked by this register |

### n8n

| Field | Value |
| --- | --- |
| Service | n8n |
| Provider | n8n |
| Capability | Inactive orchestration templates. Economics public-research staging only. Not business authority |
| Account owner | `UNRECORDED` |
| Current plan | `UNRECORDED` |
| Monthly / annual cost | `UNRECORDED` |
| Usage-based charges | Hosted executions are metered if a paid n8n plan is used. No production workflow is activated in this repo |
| Renewal / billing cycle | `UNRECORDED` |
| Engineering dependency | `n8n/workflows/` |
| Essential / optional | Optional until an accepted contract activates a workflow |
| Existing / proposed | `TEMPLATE_ONLY` |
| Alternatives | .NET for loops, calculations, QR, fingerprints, rendering, and state transitions |
| Status | `UNACTIVATED` |
| Class | `UNRECORDED` until a bill exists. Do not add a second workflow engine |

### GoHighLevel

| Field | Value |
| --- | --- |
| Service | GoHighLevel (GHL), named in the earlier Wedding Planner product note as an acquisition front door |
| Provider | GoHighLevel |
| Capability | Not coupled to this repository |
| Account owner | `UNRECORDED` |
| Current plan | `UNRECORDED` |
| Monthly / annual cost | `UNRECORDED` |
| Usage-based charges | `UNRECORDED` |
| Renewal / billing cycle | `UNRECORDED` |
| Engineering dependency | none in this repo |
| Essential / optional | Not essential to accepted Bliss behavior |
| Existing / proposed | Not an implementation in this repo. Do not treat the name as approved new spend |
| Alternatives | Future Alpha Communication Router and Unified Conversation Ledger, after their own contracts, before any additional CRM |
| Status | `OBSERVED` as a prior-document name only |
| Class | `UNRECORDED`. A second CRM is `DUPLICATIVE_REJECTED` unless a capability-gap review shows a missing function |

### Personal consumer chat subscriptions

| Field | Value |
| --- | --- |
| Service | Mark's or Erwin's personal ChatGPT account, or any single consumer chat subscription used as a production worker |
| Provider | Consumer chat product |
| Capability | None for production Alpha |
| Account owner | personal |
| Current plan | consumer |
| Monthly / annual cost | Not an Alpha production dependency |
| Usage-based charges | n/a |
| Renewal / billing cycle | n/a |
| Engineering dependency | none |
| Essential / optional | Prohibited as a production dependency |
| Existing / proposed | `REJECTED` |
| Alternatives | A provider adapter behind budget policy, on the minimum provider set a later contract justifies |
| Status | `REJECTED` |
| Class | `DUPLICATIVE_REJECTED` |

### FFmpeg

| Field | Value |
| --- | --- |
| Service | FFmpeg or an equivalent deterministic renderer |
| Provider | self-hosted |
| Capability | Routine video compositing. Not implemented in this repo yet |
| Account owner | n/a |
| Current plan | n/a |
| Monthly / annual cost | No license fee for ordinary FFmpeg use |
| Usage-based charges | Compute only, `UNRECORDED` |
| Renewal / billing cycle | n/a |
| Engineering dependency | none until the Deterministic Video Composition contract |
| Essential / optional | Future |
| Existing / proposed | `FUTURE` |
| Alternatives | Generative video is rejected for ordinary compositing |
| Status | `NOT_PROPOSED` as a subscription |
| Class | `FREE_SELF_HOSTED` |

### QR generation

| Field | Value |
| --- | --- |
| Service | In-process QR generation |
| Provider | self-hosted library, chosen by the composition contract |
| Capability | Real QR codes. No AI call and no per-code subscription |
| Account owner | n/a |
| Current plan | n/a |
| Monthly / annual cost | No per-QR fee |
| Usage-based charges | none |
| Renewal / billing cycle | n/a |
| Engineering dependency | none until that contract |
| Essential / optional | Future |
| Existing / proposed | `FUTURE` |
| Alternatives | AI image generation and QR SaaS are rejected for routine codes |
| Status | `NOT_PROPOSED` as a subscription |
| Class | `FREE_SELF_HOSTED` |

### Contact enrichment

| Field | Value |
| --- | --- |
| Service | Commercial contact-enrichment database |
| Provider | none selected |
| Capability | Not purchased. The future provider interface is not permission to subscribe |
| Account owner | n/a |
| Current plan | none |
| Monthly / annual cost | none |
| Usage-based charges | none |
| Renewal / billing cycle | n/a |
| Engineering dependency | none |
| Essential / optional | Optional, and only after measured cost per verified decision-maker, contactability, response, qualified opportunity, and customer |
| Existing / proposed | `NOT_PROPOSED` |
| Alternatives | Public web and business research, then existing Alpha records |
| Status | `NOT_PROPOSED` |
| Class | Would be `NEW_PAID_SUBSCRIPTION` or `USAGE_BASED` only after a capability-gap review |

### AI model providers

| Field | Value |
| --- | --- |
| Service | Production model providers for creative, research, and conversation workers |
| Provider | none locked by this repo |
| Capability | Not called by accepted Bliss, Wedding Planner Phase 1, or Economics pricing authority |
| Account owner | `UNRECORDED` |
| Current plan | none in this repo |
| Monthly / annual cost | none recorded |
| Usage-based charges | none recorded |
| Renewal / billing cycle | n/a |
| Engineering dependency | Economics Phase 7 can call an environment-configured extraction endpoint. That workflow is inactive and is not pricing authority |
| Essential / optional | Not required for accepted behavior |
| Existing / proposed | `NOT_PROPOSED` as an additional subscription |
| Alternatives | Deterministic software for the services listed in the master architecture. One suitable provider before a second provider |
| Status | `NOT_PROPOSED` |
| Class | Add a row only when a contract names the provider. Prefer `USAGE_BASED` over a new seat. Do not add a second provider because an adapter exists |

## New paid subscriptions

None. This document does not approve one.

## What to record next

When a bill or contract is in hand, an operator records that amount on
`/operations#/ledger`. The software stores the supplied amount and does
not invent one. This file remains the classification source. PostgreSQL
holds the same known services. A missing cost stays unrecorded. A review
is not a purchase.
