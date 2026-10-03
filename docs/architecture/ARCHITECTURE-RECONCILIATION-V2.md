# Architecture reconciliation — v2

**Status:** engineering assessment. It challenges
`MASTER-ARCHITECTURE-V2.md`. It does not authorize the unbuilt phases.

The program remains a future engineering program. Reconciliation is
recorded. The v2 amendments are not verified as a completed platform.

## Challenge

The blueprint is right about the commercial voyage and right to warn
against a service per idea. Several sections still describe a second
copy of a machine Alpha already has, or a scale target that would be
unsafe to treat as a design requirement today.

### Already solved, so do not rebuild

| Capability | Where it already lives |
| --- | --- |
| Creator and advertiser matching | Bliss Phases 1–20. `DeterministicRuleEvaluator` remains the matching authority. |
| Hosted fail-closed posture | Bliss Phase 20. That evidence does not declare the hosted deployment finished. |
| Markets, audience snapshots, inventory benchmarks, deterministic rate ranges | Economics Phases 1–4. |
| Quote versions and negotiation history | Economics Phase 5. |
| Compensation illustrations | Economics Phase 6. |
| Public-research provenance | Economics Phase 7. The workflow may stage research. .NET still validates it. |
| Planner asks Economics instead of inventing a price | Economics Phase 8. |
| Append-only performance history | Economics Phase 9. Automatic repricing, settlement, and payout are not authorized. Phase 9 is pending owner acceptance. |
| Advertiser workspace, session, message, and audit | Wedding Planner Phase 1. Zero AI workers. |
| Operator identity, roles, and audit visibility | Existing Bliss API and operations console. |
| Source slice, duplicate rule, quota, and fuel | Verified demonstration library. |
| Software overlay, QR, disclosure, and unsent preview | Verified demonstration path. |
| Score before one cold demonstration | Verified discovery screen. |
| Public evidence before a personal name | Verified decision-maker screen. |
| Provider-neutral sign-in | Existing OIDC. Development remains the anonymous local path. |

Alpha Auto is a separate product. The AI Development Fleet Mission 002
is not this program and stays stopped on its own security finding.

### Partial, so extend

- The demonstration is one application with prospect data, but it still
  flattens a permanent MP4 per concept. That was the correct cheap proof.
  It is not yet a recipe runtime.
- The conversation is one Ask Alpha surface with deterministic answers.
  It is not a queued multi-tenant conversation platform.
- Economics can recommend and record negotiation history. Ask Alpha reads
  an accepted quote amount and states no number when that result is absent.
- The operations console can show Bliss, Wedding Planner, and Economics,
  and can pause one acquisition lane without stopping the others.
- Prospect rows, clip metadata, and factory batches are rows in the
  Bliss PostgreSQL database. The video and QR files stay on disk.

### Genuinely missing

- A preserved business that is legitimate and still not worth a
  demonstration.
- Green, yellow, and red progression on the prospect. Phase 21 can
  pause one lane. It does not advance a prospect, and green still does
  not send. This is not a scale claim.
- Acquisition events. Phase 11 records a factory batch: deterministic checks, asset counts, zero model calls, and no invented price.
- Hosted scale. Phase 23 measured 100, 1,000, and 10,000 repeated
  in-memory checks. That measurement is not a count of stored
  prospects and it does not claim the 15-minute factory target.

### Conflicts to resolve before building past them

1. **Big net versus the score-100 gate.** Resolved in Phase 8. A named
   business with a public source URL is stored when the road scores
   below 100. The demonstration, the decision-maker record, and any
   send stay withheld until the road scores 100. A missing name or a
   missing public URL is still rejected and not stored.
2. **Permanent composite versus just-in-time overlay.** Phase 10 stores
   recipe `overlay-1` and plays it on the approved source slice. QA
   matches the prospect-page QR and the not-sponsored disclosure. The
   permanent MP4 is still written. Retirement waits until the player is
   the only served picture.
3. **Ask for the sale versus authority.** Positive advocacy can be added
   without removing the disclosure or the unverified-person rule.
   A binding price, a reservation, and a won state wait for an accepted
   Economics result. Phase 15 may record a draft only between the
   creator floor and the envelope high already stored on the approved
   line. Words and system state have to agree, so Ask Alpha cannot
   announce a package the database did not accept.
4. **Do not self-gate versus required truth.** The not-sponsored
   disclosure, the unsent status, and the refusal to invent a person or
   a price stay. Those are integrity checks, not voluntary objections.
5. **Research and grooming versus production.** Phase 22 counts the
   production events already stored and stages an external excerpt as
   untrusted. The excerpt is not copied onto the prospect. A weekly
   note is not a deployment. Mission Control does not appear by
   creating another repository or another console.

### Duplicative, expensive, or overbuilt if taken literally

- A negotiation service beside Economics Phase 5.
- A commercial-memory database beside PostgreSQL.
- A route-graph application beside prospect fields and a delivery policy.
- Seven grooming models, a permanent research agent, and an evaluator on
  every routine message.
- Premium enrichment before public data is insufficient.
- A permanent MP4 and a generated design for every prospect.
- A 10,000-conversation platform before one eligible send exists.
- A new subscription for any of the above.

### Unsafe if started in the wrong order

- Sending because a public page or a WhatsApp number exists.
- Letting a model remember another prospect's budget, route, or price.
- Graduating a friendlier sales voice that can invent scarcity or skip
  the creator floor.
- Claiming the 15-minute or cost target before a measured batch.

### Scale

Prospect rows now live in the Bliss PostgreSQL database. That promotion
is not a measurement of 100, 1,000, or 10,000 prospects. Media files
stay on disk.

## Simplest architecture that keeps the capability

One ASP.NET application. One PostgreSQL database, the existing Bliss
database, holds the prospect rows. One demonstration page that resolves a versioned
recipe. One Ask Alpha endpoint with gears behind it. Economics is the
only price authority. Bliss remains the only matching authority.
Wedding Planner remains the campaign workspace and stays asleep until
the commercial state calls it. n8n may orchestrate a job and may not
own the record. No new subscription.

Spend deterministic software and reusable media before engagement.
Spend a stronger answer only after a person asks something the cheap
path cannot resolve. Spend campaign intelligence after a real
commercial commitment.

## Required changes when each phase starts

Schema, API, queue, isolation, and provider work belong to the phase
that needs them. Phase 8 records `PRESERVED` for a business with no
demonstration. Phase 9 records route rows and a suppression flag. A public road stays ineligible to send. Phase 10 records recipe `overlay-1` and a player. The permanent composite remains. Phase 12 appends observable events on the prospect record. An opinion and a delivery claim are refused. Phase 13 keeps one Ask Alpha voice that answers and then advances. Phase 13 needs conversation state that
cannot see another prospect. Phase 14 reads an accepted Economics quote and copies that amount. Phase 15 records a visitor proposal through QuoteService only when it sits inside the approved line's recommendation low and high. The new version is a draft. It is not approved and it is not accepted.
Phase 16 applies the preview policy and the preview adapter. A public road stays short of permission to send. Transmission stays NOT_SENT. Phase 17 adds `ProspectMemories`, `SourceClipMemories`, and `FactoryBatchMemories` on `BlissDbContext`. Phase 18 records a rematch notice by calling `DeterministicRuleEvaluator`. An approved alternate is not a win, and a missing opportunity keeps the advertiser. Phase 19 opens the existing Wedding Planner workspace only when Economics has accepted a result and an advertiser is already on file. The inherited message names that result and plans no campaign. Phase 20 runs the conversation laboratory against the production Ask Alpha replies. A failing scenario is the regression. The laboratory does not write a prospect. Phase 21 records lane tempo on the existing operations console. One paused lane does not stop the others. A ceiling is an operational limit, not an Economics price. Green does not send. Phase 22 reads those production events into a grooming note. An external excerpt can be staged only after a production event, and the reading does not change the prospect. Phase 23 measures the rungs 100, 1,000, and 10,000 as repeated in-memory checks. A passed check is not a claim of stored prospects or sends. The 15-minute factory target is not claimed. The next gap is green, yellow, and red progression on the prospect.

## Acceptance

A phase is verified only with the tests, regression, and walkthrough
that phase names. The program stays open until its own phases are
verified. This assessment is complete as a reconciliation. It is not
the factory, Ask Alpha at scale, negotiation, the laboratory, grooming,
Mission Control, or commercial memory.
