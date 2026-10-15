# Phase 5 — Creative approval and Bliss handoff

The user explicitly authorized Phase 5 while Phase 4 live-provider validation
remains deferred. This phase supports manually submitted candidates and requires
no paid calls. It does not claim Phase 4's outstanding check succeeded.

New candidate-bound decision history supplements the existing generic creative
approval proof. Generic workspace/title approvals alone cannot authorize a
candidate. Reuse CreativeApproval's human approve/withhold decision semantics;
bind each decision to an exact candidate, assessment, stage, and actor.

Stages: ADVERTISER approval, ALPHA_QA approval, and INVENTORY_FIT approval for one
specific slot. In authenticated deployments, advertiser decisions require the
advertiser role and matching advertiser claim. QA/fit require reviewer/admin
authority plus workspace access; an advertiser cannot give themselves QA by
submitting an actor label. Advertiser and QA approvers must have different
server-resolved subjects. Development with authentication disabled supports
fictional human workflow proofs; it does not establish hosted actor separation.

An APPROVE decision requires the latest candidate and latest PASS instructor
assessment with all four gates passed and no critical defect. It binds the
assessment ID. Latest confirmed Brand DNA must still match the brief. Inventory
fit additionally requires available image-overlay inventory linked to an approved
match for this advertiser, a nondegenerate PNG, and explicit human confirmation
of dimensions, format, and creator-safe areas. Pixel dimensions are recorded;
the application does not invent missing slot geometry or automatically resize.
WITHHOLD can append a later revocation without erasing earlier decisions.
Stage/slot versions and retry keys distinguish stale and conflicting inputs.

An explicit staff planning request identifies the candidate, exact latest stage
approval IDs, approved Bliss match, draft campaign, available slot, and accepted
Economics quote line. Quote version must be current and accepted with stored
approval and advertiser acceptance evidence. The selected line must have quantity
one and reference a recommendation for this exact match, opportunity, creator,
content, and slot. Monetary values are read from Economics only. No prices are
accepted as request fields and no pricing formulas are implemented here.

The existing CampaignPlacementService validates and prepares placement intent.
Placement/run, creative handoff lineage, and audit commit together. The handoff
records asset hash, reference/DNA provenance, assessment and approval IDs,
inventory fit, match/rule evidence, and quote/version/line/amount snapshot.
One quote line can be handed off once. Exact retries replay; changed inputs
conflict; concurrent attempts cannot create duplicate handoffs/placements.
Planned history is immutable and survives subsequent revocations; those prevent
new planning, and any future delivery must revalidate authority. Slots are not
reserved, dates are not scheduled, campaigns remain DRAFT, delivery NOT_SENT.

Workspace API: scoped candidate approval histories and writes, planning options,
handoff histories and staff-only writes. Staff can explicitly create a DRAFT
campaign for an advertiser-owned approved match using a stable request UUID;
identical intent replays and changed intent conflicts. Public `/campaign-handoff.html` provides
the explicit human actions, displays gate failures, and displays quote amounts
from Economics. User selection is never authorization.

Required checks: positive authorized end-to-end handoff, unreviewed/stale/revoked
creative refusal, distinct approval authority, unavailable/incompatible inventory,
wrong-owner/wrong-match/unaccepted/revised quote refusal, cross-tenant and CSRF
negative tests, immutable history, atomic concurrent planning, database migrations,
browser flow, and full regression. Legacy matching/economics calculations remain
unchanged. Production deployment, AI activation, and external sending are excluded.
