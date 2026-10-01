# Engineering Contract Rule

**Status:** binding on future acquisition and factory contracts. It does
not open any of those contracts.

Do not build the long-term architecture as one task. Every future
Engineering Contract is a separate document with its own
implementation, tests, evidence, and acceptance. Do not auto-advance.

Current Bliss Chapel work is outside this rule's build list. This rule
does not pause an accepted Bliss contract and does not start the next
Bliss phase.

## Required contents

Every future contract includes:

| Section | What it must answer |
| --- | --- |
| Objective | The one capability this contract adds |
| AI capability | Which steps require a model, and why software cannot do them |
| Deterministic capability | Which steps are ordinary software |
| Existing reusable assets | Cache rows, source media, components, and code that must be reused first |
| Required provider | The minimum provider set. Empty if none |
| Subscription impact | Class from the ledger, and whether a new row is required |
| Build-versus-buy review | The capability-gap sequence in `SUBSCRIPTION-LEDGER.md` |
| Estimated usage cost | Expected spend for the contract's volume |
| Maximum cost | The ceiling that stops or degrades the work |
| Inputs | What the contract reads |
| Outputs | What it persists or returns |
| Cache policy | What is stored, for how long, and when it is reverified |
| Failure handling | What happens when a provider, file, or fact is missing |
| Authorization | Who may call it, and which actions AI cannot take |
| Persistence | PostgreSQL tables or existing tables. n8n is not the system of record |
| Tests | Including the acceptance obligations in `MASTER-ARCHITECTURE.md` that this contract owns |
| Evidence | Commands, counts, and artifacts a reviewer can check |
| Acceptance criteria | Observable pass conditions |
| Out of scope | The next contracts, named so they are not built here |

Every contract also answers, in prose:

- Why does this need AI?
- Can software do it cheaper?
- Can we reuse something?
- Do we already pay for this capability?
- Do we actually need a new subscription?

If the honest answer to the AI question is no, the contract uses
software and records zero model calls for that step.

## Cost fields that cannot be skipped

- Operations that require AI.
- Operations that can use deterministic software.
- Information that can be cached.
- Assets that can be reused.
- Model or provider tier.
- Expected usage cost.
- Maximum permitted cost.
- Existing Alpha capability.
- Subscription impact.
- Fallback behavior.
- Budget-threshold behavior.

Classify each dependency as `FREE_SELF_HOSTED`,
`EXISTING_ALPHA_SUBSCRIPTION`, `USAGE_BASED`,
`FREE_OR_DEVELOPMENT_TIER`, `NEW_PAID_SUBSCRIPTION`, or
`DUPLICATIVE_REJECTED`.

A `NEW_PAID_SUBSCRIPTION` classification is incomplete without the
capability-gap review. An incomplete review means the contract is not
acceptable.

## Boundary rules

- .NET executes high-volume deterministic work and enforces authority.
- PostgreSQL remembers.
- n8n coordinates only where a contract says so. It does not own loops,
  quota math, QR, fingerprints, rendering, or state transitions that
  .NET can perform.
- AI cannot set `WON`, approve creative, activate a campaign, invent a
  price, sign a contract, move money, or publish.
- AI cannot fabricate a decision-maker, title, or contact.
- Personal consumer chat accounts are not a provider.
- Wedding Planner does not invent advertising prices. Economics remains
  the rate authority.
- Bliss matching remains the compatibility authority. A contract that
  needs a match calls it. It does not reimplement it.
- Alpha Auto stays a separate product.

## Acceptance shape

The contract names the tests it owns from the master architecture,
implements them, and files evidence the way `docs/bliss/` and
`docs/economics/` already do. A document that only restates the
architecture is not an accepted contract.
