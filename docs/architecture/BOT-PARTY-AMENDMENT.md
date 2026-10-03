# High-velocity acquisition and Bot Party amendment

**Status:** addition to the existing Alpha and Bliss architecture.
This document does not remove or weaken a safeguard already built.
It does not authorize crawlers, live send, a second Mission Control
product, or the unstarted phases named below.

Volume is a core business requirement. Alpha is a high-volume
marketplace. Safeguards exist so legitimate scale does not become
suppressed activity, an opt-out violation, duplicate outreach, a
deceptive claim, an invalid contact road, a prohibited channel, an
unsafe operation, or an unauthorized financial action. They must not
create arbitrary scarcity for legitimate activity.

## What stays in force

- Suppression, opt-out, and an invalid road still stop the prohibited
  action.
- Transmission stays `NOT_SENT` until a later phase explicitly
  authorizes a send. Green is progress to the next authorized action.
  Green is not a send.
- A public road is not permission to send.
- Decision-maker confidence stays unverified unless public evidence
  supports a named state. Names are not invented.
- Economics remains the only price authority. An operational spend
  ceiling is not an Economics price and is not a prospect price.
- `DeterministicRuleEvaluator` remains the only matching authority.
- Wedding Planner stays asleep until an accepted Economics result and
  an advertiser already on file.
- One PostgreSQL database remains the long-term system of record.
- One operations console remains the place to see and pause a lane.

## Operating rules this amendment adds

Cast the net wide. A legitimate prospect is preserved, queued, and
processed. It is not discarded because a downstream lane is slower
than discovery. Discovery capacity does not have to match outreach
capacity. Flow is regulated. Legitimate volume is not artificially
restricted.

Green moves automatically to the next authorized action: discovery to
verification, verification to road finding, a road to policy, eligibility
to an approved queue, an authorized outreach action to that action, a
response to the right route, and an accepted Economics result to the
eligible commercial step. Routine green progression does not wait for
a manual approval.

Yellow preserves the record and tries another legitimate road, a
recheck, or another bot. A person is the escalation when automation
cannot resolve the exception.

Red stops the prohibited action and keeps the prospect, the status,
the reason, and the evidence. Red does not erase the business.

Independent lanes may move at the same time. A problem in one lane
does not stop every healthy lane. Mission Control, on the existing
operations console, sets tempo: full, reduced, or stopped for the
affected lane.

Busy is not the measure. Productive marketplace results are the
measure. Counts of discovered advertisers, creators, roads, eligible
prospects, inventory, and revenue are future measurements. This
amendment does not invent those counts.

The Fishing Fleet and the Creator Fleet both remain required. Bliss
Chapel stays the matching middle. Creative density inside a podcast
stays disciplined: balanced pairs of two, four, or six advertisements,
subject to creator approval, content suitability, viewer experience,
and Alpha QA. A two-over-four stack is not the default. Rotation
periods remain a future configuration and do not require one
advertiser for every theoretical slot.

## What Phase 21 implements

Phase 21 records tempo for six lanes on `/operations#/tempo`:
discovery, verification, road finding, policy, outreach, and creator
discovery. Each lane starts at full tempo with no spend ceiling. An
operator may record a ceiling. A partial ceiling is refused and no
number is invented. Stopping one lane leaves the others at their own
tempo. The prospect record is preserved. Every notice says green does
not send and delivery remains `NOT_SENT`. The audit is append-only.

Phase 21 does not discover businesses, crawl, send, buy enrichment,
change a creative layout, or create inventory rotations.

## Amendment phases

| Phase | Capability | Status |
| --- | --- | --- |
| 24 | Green, yellow, and red progression on the prospect | Verified. Green is not a send |
| 25 | Flow control: preserve and queue legitimate volume | Verified. None are discarded |
| 26 | Independent fleet lanes beyond a pause control | Verified. A broken lane does not stop the ocean |
| 27 | Marketplace balance signals, without invented counts | Verified. Stored rows only |
| 28 | Balanced creative inventory: pairs of 2, 4, or 6 | Verified. The stack is refused |
| 29 | Rotation abundance under creator approval | Next. Not started |

Phase 22 counts production events and stages an untrusted excerpt
without changing the prospect. Phase 23 measures the in-memory rungs
and does not claim stored scale. Phase 24 records green, yellow, and
red on the prospect. Green is not a send. Phase 25 keeps excess
legitimate prospects queued. Phase 26 reads the Fishing Fleet and the
Creator Fleet from the stored lane tempos. A broken lane does not stop
the ocean. Phase 27 reads advertiser pressure and creator pressure
from stored rows. A missing count stays unrecorded. Revenue and
inventory stay unrecorded. Phase 28 accepts a balanced pair of 2, 4,
or 6 when the creator approves that density. A two-over-four stack is
refused. Stored slots stay as stored rows. The next authorized build
after Phase 28 is Phase 29.

## What Phase 26 implements

Phase 26 reads the six stored lane tempos as two fleets on
`/operations#/tempo`. The Fishing Fleet is discovery, verification,
road finding, policy, and outreach. The Creator Fleet is creator
discovery. Bliss Chapel stays the matching middle. A stopped lane is
broken for that lane. The other lanes and the other fleet keep their
own tempo. The reading does not append an audit, crawl, send, or
invent a lane. Green does not send. Delivery remains `NOT_SENT`.

Phase 26 does not build a second console, a crawler, or marketplace
balance counts.

## What Phase 27 implements

Phase 27 reads stored advertiser names and stored creator names on
`/acquisition/balance.html`. The counts are the named stored rows. A
missing count stays unrecorded and is not treated as zero. A blank
stored name is skipped. Revenue is not recorded. Inventory is not
recorded. The reading claims no market census. It does not write a
row, crawl, send, or invent a price. Green does not send. Delivery
remains `NOT_SENT`.

Phase 27 does not choose a creative pair or a rotation.

## What Phase 28 implements

Phase 28 reads a proposed pair on `/acquisition/inventory.html`. A
balanced pair is 2, 4, or 6. Creator approval accepts that density or
withholds it. A two-over-four stack is refused even when approval is
present. The stored slot rows stay unchanged and are not treated as a
balanced pair. Rotation is not configured. The reading does not assign
an advertiser to a slot, crawl, send, or invent a price. Green does
not send. Delivery remains `NOT_SENT`.

Phase 28 does not require one advertiser for every theoretical slot.
