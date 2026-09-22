# Wedding Planner Phase 8 — Engineering Contract

Status: authorized for implementation by the user's request to proceed to
Phase 8.

## Objective

Add the **Bliss handshake / campaign-readiness** planning integration as
Wedding Planner **deterministic** software:

1. an authenticated **operator/admin** commits a campaign-readiness
   handshake for an authorized workspace that already has a **current
   clean** Phase 7 QA report (`Status = ACCEPTED` only — never
   `ACCEPTED_WITH_EXCEPTION`) pointed by
   `CurrentAcceptedQaReviewReportVersionId`, whose acceptance decision
   is a clean `ACCEPT` (never `WAIVE_AND_ACCEPT`), and whose pinned
   Phase 6 creative package remains the current approved package with
   valid APPROVE / variant / exactly-one asset integrity;
2. the human commit body selects exact Bliss inventory targets
   (`blissMatchId`, `campaignId`, `contentItemId`, `adInventorySlotId`)
   plus rationale and acknowledgements;
3. the server runs authoritative deterministic
   `campaign-readiness-rules.v1` (**PASS / BLOCK only — no WARN**) and
   **all** listed codes must PASS before any durable write;
4. on success, **one** transaction atomically inserts:
   - existing Bliss `CampaignPlacement` with status `PLANNED`,
   - existing Bliss `CampaignPlacementRun` with status `COMPLETED` /
     outcome `PLANNED`,
   - immutable `WeddingPlannerCampaignReadinessHandshakeVersion` with
     status `CAMPAIGN_READY`,
   - immutable `WeddingPlannerCampaignReadinessDecision` with
     `MARK_CAMPAIGN_READY` (created atomically with the version),
   - workspace pointer `CurrentCampaignReadinessHandshakeVersionId`
     set to that version;
5. revoke is an explicit human `REVOKE_CAMPAIGN_READY` decision that
   mutates **only** the handshake row status to `REVOKED` and clears the
   pointer when it was current — **never** deletes or changes the
   planned placement/run.

PostgreSQL remains permanent memory. Phase 8 adds **exactly 0 NEW** AI
roles, profiles, calls, runs, prompts, or providers. Campaign-ready
state lives **only** in the new handshake document/status + workspace
pointer. Phase 6 creative packages and Phase 7 QA reports are **never**
mutated to `CAMPAIGN_READY`. Blueprint formula locked verbatim:

> campaign-ready creative + approved Bliss match + compatible inventory
> → planned placement

### Inconsistencies resolved (design corrections)

| Prior / ambiguous design | Locked Phase 8 correction |
| --- | --- |
| Blueprint “campaign-ready creative” vs creative/QA state machines that forbid marking packages/reports campaign-ready | **Campaign-ready is handshake-owned.** Creative package and QA report statuses are never mutated to `CAMPAIGN_READY`. Handshake + `CurrentCampaignReadinessHandshakeVersionId` are the sole campaign-ready state. |
| Phase 7 left open whether `ACCEPTED_WITH_EXCEPTION` could be campaign-ready | **Fail-closed reject.** Only clean `ACCEPTED` + clean `ACCEPT` decision. `ACCEPTED_WITH_EXCEPTION` / waive path is **ineligible** with **no** Phase 8 waiver. |
| Temptation to gate or clear `AdInventorySlot.IsAvailable` on planned bind | **Preserve Bliss Phase 7 semantics exactly.** Do **not** gate on `IsAvailable`. Do **not** mutate `IsAvailable`. Planned placement discloses **no reservation**. |
| Temptation to invent format/slot mapping or recompute Bliss matching | Forbidden. Reuse exact Bliss Phase 7 compatibility checks only. Matching scores/status history unchanged. |
| Whether Phase 8 should call `CampaignPlacementService.BindAsync` separately (two transactions) | **Refactor** into a shared transaction-aware bind **core**. Phase 8 owns **one** outer transaction; core participates without starting a nested conflicting commit. Public `BindAsync` behavior unchanged. |
| Whether pointer clear / revoke should rewrite historical handshake rows | Historical handshake **document** immutable. Status mutates **only** on explicit revoke (`CAMPAIGN_READY` → `REVOKED`). Pointer clear alone leaves historical row/status unchanged. |
| Whether new QA ACCEPT / WAIVE or creative APPROVE should auto-create a handshake | **Never.** They **clear** the current handshake pointer (when set). No auto handshake. Exception QA remains ineligible even if pointer briefly pointed at a waived report after clear rules. |
| Whether advertisers/reviewers/AI can mark campaign ready | **Operator/admin only** for commit and revoke. AI cannot. Advertisers/reviewers cannot create/revoke. |
| Whether Phase 8 needs an agent-run API | **No.** Zero AI; no agent-run endpoints for Phase 8. |

Blueprint Phase 8 starting allocation (“0 new logical AI; deterministic
integration”) is hereby locked for V1 as above.

## Assumptions

1. Phases 1–7 remain accepted and unchanged in behavior except for
   additive handshake tables/pointers, shared placement-core refactor
   (public Bliss bind behavior preserved), routes, UI surfaces, tests,
   audit action names, and the Phase 6 creative `APPROVE` / Phase 7 QA
   pointer-move hooks that **clear**
   `CurrentCampaignReadinessHandshakeVersionId` as specified below.
2. A workspace may commit a handshake only when
   `CurrentAcceptedQaReviewReportVersionId` is non-null **and** that
   report `Status` is exactly `ACCEPTED` (not
   `ACCEPTED_WITH_EXCEPTION`) **and** the latest human decision on that
   report is exactly `ACCEPT` (not a waive resolution) **and**
   `CurrentApprovedCreativePackageVersionId` is non-null and equals the
   QA report’s pinned approved creative package **and** package /
   decision / variant / asset / provenance pins remain valid under
   `campaign-readiness-rules.v1`.
3. Phase 6 creative package `DocumentJson`, contributions, assets, and
   APPROVE decision remain immutable provenance. Phase 7 QA report
   `DocumentJson`, contributions, decisions, case opening payloads, and
   resolutions remain immutable provenance (case status alone may have
   advanced `OPEN` → `RESOLVED`). Phase 8 never re-invokes Phase 2–7
   profiles, never rewrites creative/QA rows to campaign-ready, and
   never re-runs matching.
4. Campaign-readiness is **deterministic planning integration only**. It
   is not research, claim, legal, accessibility, compliance,
   measurement, delivery, publication, or payment approval.
5. Bliss matching, review evaluation, Alpha Auto, and n8n authority
   remain out of scope and untouched except for the shared placement
   bind core refactor and Phase 8’s atomic use of that core.
6. Phase 9 measurement/learning is out of scope. Phase 8 does not
   auto-advance to Phase 9. Placement remains `PLANNED` only — not
   activation.

## Workforce allocation

| Item | Phase 8 allocation |
| --- | --- |
| New logical AI roles | **exactly 0** |
| Executable AI profiles / calls / runs | **exactly 0** |
| New prompt packs | **exactly 0** |
| New AI providers | **exactly 0** |
| Deterministic rules engine | `campaign-readiness-rules.v1` — **not** an AI worker/run |
| Human authority | operator/admin alone `MARK_CAMPAIGN_READY` / `REVOKE_CAMPAIGN_READY` |
| Agent runs on success | **0** `WeddingPlannerAgentRun` rows |
| Durable handshake evidence | immutable handshake version + MARK decision + planned placement/run |
| Phase 2–7 roles / runs | remain pinned provenance; **0** re-runs |

Do **not** create fake agent runs, AI profiles, or prompt packs for
Phase 8. Deterministic rules execution and human commit/revoke are
**not** AI workers and do **not** create agent-run rows.

Phases 2–7 remain as allocated. Phase 8 does not extend their tools into
measurement, activation, legal certification, matching writes beyond the
existing placement bind semantics, Alpha Auto, or n8n.

## Authority

- Advertisers act only inside the `advertiser_id`-bound workspace.
- Operators/admins may act across advertisers; viewers cannot write.
- **Operator/admin only** may **commit** (`POST` handshake →
  `MARK_CAMPAIGN_READY`) and **revoke**
  (`POST .../decisions` with `REVOKE_CAMPAIGN_READY`).
- Advertisers **cannot** create or revoke handshakes.
- Reviewers **cannot** create or revoke handshakes.
- Advertisers may **read** own workspace eligibility + own handshakes /
  decisions.
- Reviewers may **read** handshakes/decisions/eligibility across
  advertisers **only** under the dedicated Phase 8 handshake **read**
  policy below (explicit handshake read — not a widening of generic WP
  write).
- Viewers have no Phase 8 workspace read or write authority in V1.
- Do **not** widen generic WeddingPlanner `CanWrite` to grant
  advertisers or reviewers commit/revoke authority.
- AI cannot mark campaign ready, revoke, propose waivers, or create
  placement/handshake rows.
- Cross-tenant resource lookup returns **404**, consistent with
  Phases 1–7.
- Anonymous protected reads/writes return **401** when OIDC is enabled.
- Authenticated identities without commit/revoke authority return
  **403** on those writes.
- Advertiser reading another advertiser’s handshake → **404**.
- Reviewer/operator/admin missing resource → **404**; wrong role on
  write → **403**.

### Dedicated handshake policy (normative)

| Action | Advertiser | Reviewer | Operator | Admin | Viewer |
| --- | --- | --- | --- | --- | --- |
| Read eligibility (own / assigned) | yes (own) | yes (explicit handshake read) | yes | yes | no |
| List/get handshakes + decisions | yes (own) | yes (explicit handshake read) | yes | yes | no |
| Commit handshake (`MARK_CAMPAIGN_READY`) | **no** | **no** | yes | yes | no |
| Revoke (`REVOKE_CAMPAIGN_READY`) | **no** | **no** | yes | yes | no |

Reviewer read authority is limited to Phase 8 campaign-readiness routes
under this dedicated policy. It does **not** widen generic Wedding
Planner write authority or permit reviewer writes to Phase 1–7
resources.

## Campaign-ready state ownership (normative)

Phase 8 campaign-ready state lives **ONLY** in:

1. `WeddingPlannerCampaignReadinessHandshakeVersion` (immutable document;
   status `CAMPAIGN_READY` | `REVOKED`); and
2. workspace pointer `CurrentCampaignReadinessHandshakeVersionId`.

**Forbidden mutations for campaign-ready:**

- Never set creative package status / DocumentJson / decision to
  `CAMPAIGN_READY`.
- Never set QA report status / DocumentJson / decision to
  `CAMPAIGN_READY`.
- Never invent a creative or QA “campaign ready” boolean column.

Clean current QA `ACCEPTED` only. `ACCEPTED_WITH_EXCEPTION` is rejected
fail-closed with **no** Phase 8 waiver path.

## Inputs (human commit body)

`POST .../campaign-readiness-handshakes` body:

| Field | Required | Rules |
| --- | --- | --- |
| `blissMatchId` | yes | exact existing `BlissMatch` Guid |
| `campaignId` | yes | exact existing `Campaign` Guid |
| `contentItemId` | yes | exact existing `ContentItem` Guid |
| `adInventorySlotId` | yes | exact existing `AdInventorySlot` Guid |
| `rationale` | yes | non-empty trimmed string; max length **2000** |
| `disclaimerAcknowledged` | yes | must be **`true`** |
| `syntheticMarkerAcknowledged` | conditional | must be **`true`** when any pinned upstream artifact carries a synthetic Development marker; otherwise may be null/false and is not a commit prerequisite |
| `SourceSystem` | yes | Phase 1–7 idempotency pattern; max **64**; normalized upper |
| `IdempotencyKey` | yes | unique with `SourceSystem` for the handshake; max length such that derived keys `{key}:PLACEMENT` and `{key}:MARK` fit within **128** characters (reject **400** if derived keys would exceed) |

### Forbidden commit fields

The request body **must reject** (**400**) any of the following if
present (including aliases / nested objects):

- client QA report / package / decision / variant / asset overrides
- provenance / hash / score overrides
- opportunity / placement / run / readiness / status overrides
- AI / provider configuration
- URLs, base64, markup, HTML/CSS/SVG/script
- legal / measurement / activation / reservation / payment flags
- `IsAvailable` overrides
- match score / evaluation overrides
- handshake status overrides (`CAMPAIGN_READY` / `REVOKED` client-set)
- decision type overrides other than the server-owned MARK path

Server binds, without client override, all pins listed in **Server pins**
below.

If `CurrentCampaignReadinessHandshakeVersionId` is non-null → **400**
(`revoke/clear first`). New commit is rejected while the current
handshake pointer is set. Idempotent replay of an **existing** successful
commit (same `SourceSystem` + `IdempotencyKey`) returns the existing
handshake/placement/run/decision with **no** second writes even if the
pointer was later cleared or the handshake later revoked — replay is
identity-stable on source/key, not a re-commit of campaign-ready state.

## Server pins (authoritative)

On commit evaluation the server pins and validates:

| Pin | Source of truth |
| --- | --- |
| Current clean QA report | workspace `CurrentAcceptedQaReviewReportVersionId` → report must exist, same workspace/advertiser, `Status = ACCEPTED` |
| Exact clean ACCEPT decision | latest human `WeddingPlannerQaReviewDecision` for that report with `Decision = ACCEPT` (not waive); must exist |
| Current approved creative package | workspace `CurrentApprovedCreativePackageVersionId` must equal the QA report’s pinned approved creative package id; package `Status = APPROVED` |
| Package DocumentJson SHA | recompute/store lowercase hex SHA-256; must match QA pin and package integrity |
| Latest creative APPROVE decision | latest `APPROVE` for that package; `SelectedVariantId` present |
| Selected variant + exactly one asset | variant present in package; exactly one linked PNG `CreativeAsset`; hash/meta/PNG revalidation against stored bytes (Phase 6 validator; no provider regenerate/fetch) |
| Concept / DNA / color / research lineage | from creative package pins (package pins win over possibly drifted workspace pointers) |
| Selected Bliss graph | human-selected match/campaign/content/slot + Bliss Phase 7 compatibility rules below |
| Rules output | full `campaign-readiness-rules.v1` finding set; overall must be PASS |

## Bliss exact compatibility (preserve Phase 7)

Phase 8 reuses the **same** compatibility semantics as Bliss
`CampaignPlacementService` / Phase 7 placement contract:

| Check | Rule |
| --- | --- |
| Match status | `BlissMatch.Status = APPROVED` |
| Opportunity status | match opportunity `Status = ACTIVE` |
| Advertiser scope | `opportunity → AdvertiserProgram.AdvertiserId` **must equal** workspace `AdvertiserId` |
| Campaign status | `Campaign.Status = DRAFT` |
| Campaign opportunity | `Campaign.AdvertiserOpportunityId` is **null** or **equal** to match opportunity; when null, shared placement core sets it to the match opportunity (existing Phase 7 behavior) |
| Content creator | `ContentItem.CreatorId == BlissMatch.CreatorId` |
| Slot content | `AdInventorySlot.ContentItemId == ContentItem.Id` |

### Explicit Phase 7 semantics preserved

- Do **NOT** gate on `AdInventorySlot.IsAvailable`.
- Do **NOT** mutate `IsAvailable` (true or false) on commit, revoke, or
  pointer clear.
- Do **NOT** invent format/slot-type mapping beyond the explicit slot id
  bind.
- Do **NOT** recompute Bliss matching scores or change match status /
  evaluation / review history.
- Allow existing **multi-bind** semantics: `ContentItemId`,
  `AdInventorySlotId`, and `BlissMatchId` remain non-unique across
  placements.
- Disclose **no reservation**: UI and document must state that slot
  availability is not reserved and planned placement is not activation.

### `IsAvailable` contradiction resolved

Any design impulse to treat `IsAvailable=false` as blocking
campaign-readiness, or to flip `IsAvailable` when creating a planned
placement, is **rejected**. Bliss Phase 7 already locked planning-without
-reservation. Phase 8 inherits that lock. Eligibility preview may
**display** `IsAvailable` as metadata for operator awareness but must
**not** filter candidates solely by it and must label that display as
non-authoritative for reservation.

## Shared placement bind core (refactor)

Refactor `CampaignPlacementService` so that:

1. existing public `BindAsync(CampaignPlacementCommand, …)` behavior
   remains **unchanged** for Bliss `/api/campaign-placements` callers
   (normalize, idempotent replay, validation, transaction when relational,
   insert `CampaignPlacement` `PLANNED` + `CampaignPlacementRun`
   `COMPLETED`/`PLANNED`, optional campaign opportunity assignment,
   return same result shape);
2. validation + insert logic moves into an internal
   **transaction-aware bind core** that:
   - accepts an optional ambient EF transaction / “caller owns
     transaction” flag;
   - when called from existing `BindAsync`, preserves today’s begin /
     commit behavior;
   - when called from Phase 8, **does not** begin/commit its own
     transaction and instead participates in the Phase 8 outer
     transaction;
3. Phase 8 owns **one** transaction and atomically performs:
   - shared core bind (placement + run),
   - handshake version insert (`CAMPAIGN_READY`),
   - `MARK_CAMPAIGN_READY` decision insert,
   - workspace pointer set,
   - audit events;
4. **No partial success.** Any failure after begin rolls back all of
   placement, run, handshake, decision, and pointer.

### Derived placement / decision keys

Within max lengths (`SourceSystem` 64, `IdempotencyKey` 128):

| Record | Idempotency key |
| --- | --- |
| Handshake version | human `IdempotencyKey` |
| `MARK_CAMPAIGN_READY` decision | `{handshakeKey}:MARK` |
| `CampaignPlacementRun` | `{handshakeKey}:PLACEMENT` |

Reject commit (**400**) if derived keys would exceed 128 characters.

### Idempotent replay

Exact replay of the same handshake `(SourceSystem, IdempotencyKey)`:

- returns existing handshake version, MARK decision, placement id, run
  id;
- writes **nothing**;
- placement/run/handshake/decision counts unchanged;
- `isReplay=true` (or equivalent DTO flag).

Replay does **not** re-set a cleared pointer and does **not** un-revoke a
`REVOKED` handshake. It is a durable identity lookup only.

## Deterministic rules — `campaign-readiness-rules.v1`

Rules run **before** any durable insert. Findings are durable on the
handshake document. Severity model: **PASS | BLOCK only**. **No WARN.**
Overall = BLOCK if any code is BLOCK; else PASS. Commit requires overall
**PASS** (every listed code PASS).

### Exact codes (locked) — all must PASS to commit

| Code | Severity when failing | What it asserts |
| --- | --- | --- |
| `CR_CURRENT_QA_POINTER` | **BLOCK** | Workspace `CurrentAcceptedQaReviewReportVersionId` is non-null and references an existing same-workspace report |
| `CR_QA_CLEAN_ACCEPTED` | **BLOCK** | That report `Status` is exactly `ACCEPTED` (not `ACCEPTED_WITH_EXCEPTION`, not other statuses) |
| `CR_QA_ACCEPT_DECISION` | **BLOCK** | Latest decision for that report is exactly `ACCEPT` (clean accept; waive resolution path fails) |
| `CR_PACKAGE_CURRENT_APPROVED` | **BLOCK** | Workspace current-approved creative package is non-null, equals QA package pin, package exists and is `APPROVED` |
| `CR_PACKAGE_DOCUMENT_SHA` | **BLOCK** | Recomputed package DocumentJson SHA-256 matches QA/package pins |
| `CR_CREATIVE_DECISION_VARIANT` | **BLOCK** | Latest creative `APPROVE` decision exists; `SelectedVariantId` matches pins and exists in package |
| `CR_ASSET_INTEGRITY` | **BLOCK** | Exactly one selected PNG asset; stored bytes revalidate (Phase 6 PNG validator); hash/size/dims match pins/package refs — no provider regenerate/fetch |
| `CR_PROVENANCE_CHAIN` | **BLOCK** | Selected concept + Brand DNA / Color Profile / Research Report lineage from creative package pins is present and consistent |
| `CR_MATCH_APPROVED` | **BLOCK** | Selected match exists and `Status = APPROVED` |
| `CR_OPPORTUNITY_ACTIVE` | **BLOCK** | Match opportunity exists and `Status = ACTIVE` |
| `CR_ADVERTISER_SCOPE` | **BLOCK** | Opportunity → program `AdvertiserId` equals workspace `AdvertiserId` |
| `CR_CAMPAIGN_DRAFT` | **BLOCK** | Selected campaign exists and `Status = DRAFT` |
| `CR_CAMPAIGN_OPPORTUNITY` | **BLOCK** | Campaign opportunity is null or equals match opportunity |
| `CR_CONTENT_CREATOR` | **BLOCK** | Content exists and `CreatorId` equals match `CreatorId` |
| `CR_SLOT_CONTENT` | **BLOCK** | Slot exists and `ContentItemId` equals selected content |
| `CR_SYNTHETIC_ENVIRONMENT` | **BLOCK** | Synthetic upstream artifacts: Development may PASS only when `syntheticMarkerAcknowledged=true`; non-Development host **rejects** synthetic upstream (BLOCK). Non-synthetic path PASS without ack requirement |

Every listed code **always appears** in the findings array with severity
`PASS` or `BLOCK`. Empty findings array is forbidden. Rules **cannot**
certify: semantic truth, visual safety, legal/compliance/accessibility
clearance, measurement fitness, delivery readiness, or payment
authorization. They must not invent such certifications in finding
messages.

Rules findings shape (illustrative):

```json
{
  "schemaVersion": "campaign-readiness-rules.v1",
  "overallSeverity": "PASS",
  "findings": [
    {
      "code": "CR_CURRENT_QA_POINTER",
      "severity": "PASS",
      "message": "Current accepted QA pointer is present and scoped to the workspace."
    }
  ]
}
```

### Disclaimer merge/validate

Handshake `DocumentJson.disclaimer` must equal the Phase 8 locked
disclaimer string **verbatim**. Merge/validate fails closed (**400** /
transaction abort) if mismatched, truncated, or altered.

## Synthetic environment policy

Upstream synthetic markers recognized for Phase 8 (any one ⇒ synthetic
path):

- `SYNTHETIC DEVELOPMENT CREATIVE PACKAGE` (Phase 6)
- `SYNTHETIC DEVELOPMENT QA REVIEW` (Phase 7)

| Host environment | Rule |
| --- | --- |
| Development | Commit of synthetic upstream **allowed only** when `syntheticMarkerAcknowledged=true`; otherwise `CR_SYNTHETIC_ENVIRONMENT` BLOCK |
| Non-Development (Production / equivalent remote-required) | Synthetic upstream artifacts → `CR_SYNTHETIC_ENVIRONMENT` BLOCK (fail closed); acknowledgement cannot override |

When the committed handshake is on the synthetic path, the handshake
document **must** include the conspicuous marker string exactly:

`SYNTHETIC DEVELOPMENT CAMPAIGN READINESS`

Non-synthetic handshakes must not claim this marker.

## Exact disclaimer (locked)

The following string must appear verbatim in
`campaign-readiness-handshake.v1` `DocumentJson.disclaimer` and in
commit UI surfaces that require `disclaimerAcknowledged=true`:

> This campaign-readiness handshake is a deterministic planning integration only. It marks Wedding Planner campaign-ready state for a clean Phase 7 ACCEPTED QA report bound to an APPROVED Bliss match and compatible creator inventory, and records a PLANNED campaign placement. It is not research, claim, legal, accessibility, compliance, measurement, delivery, publication, or payment approval. It does not mutate Phase 6 creative packages or Phase 7 QA reports. It does not recompute Bliss matching scores. Acceptance-with-exception QA is not campaign-ready. AI cannot mark campaign ready. Advertisers and reviewers cannot create this handshake. Slot availability is not reserved. Planned placement is not activation.

## Durable records

### `WeddingPlannerCampaignReadinessHandshakeVersion`

Immutable document (payload fields never updated after insert):

- advertiser/workspace scope; monotonic `VersionNumber` per workspace
- `SchemaVersion` = `campaign-readiness-handshake.v1`
- `DocumentJson`, `Summary`
- `Status`: `CAMPAIGN_READY` | `REVOKED`
- pins: clean QA report id + SHA/meta as needed, clean ACCEPT decision
  id, creative package id + DocumentJson SHA, creative APPROVE decision
  id, selected variant id, selected asset id + hash/meta, concept /
  DNA / color / research lineage ids (+ version numbers), selected
  `BlissMatchId`, `CampaignId`, `ContentItemId`, `AdInventorySlotId`,
  resulting `CampaignPlacementId`, `CampaignPlacementRunId`
- `RulesFindingsJson` (`campaign-readiness-rules.v1`)
- rationale snapshot; `DisclaimerAcknowledged`;
  `SyntheticMarkerAcknowledged` (as applicable); synthetic marker when
  applicable
- source/idempotency: unique `(SourceSystem, IdempotencyKey)`
- created-at / actor metadata (operator/admin)

**Status mutation rule:** document payload immutable; `Status` mutates
**only** on explicit `REVOKE_CAMPAIGN_READY` from `CAMPAIGN_READY` →
`REVOKED`. No other status transitions. No delete.

### `WeddingPlannerCampaignReadinessDecision`

Immutable human decision rows:

| Decision | When |
| --- | --- |
| `MARK_CAMPAIGN_READY` | Created **atomically** with the handshake version on successful commit |
| `REVOKE_CAMPAIGN_READY` | Later explicit revoke POST |

Fields:

- advertiser/workspace/handshake scope
- `Decision` ∈ `MARK_CAMPAIGN_READY` | `REVOKE_CAMPAIGN_READY`
- required non-empty rationale (revoke also max **2000**)
- actor, source/idempotency, timestamp
- Unique `(SourceSystem, IdempotencyKey)`
- MARK uses `{handshakeKey}:MARK`; revoke uses its own client-supplied
  source/key under the same uniqueness rule

Decisions never mutate handshake `DocumentJson`. Revoke mutates handshake
`Status` only.

### Workspace pointer

Add nullable `CurrentCampaignReadinessHandshakeVersionId` on
`WeddingPlannerWorkspace`.

| Event | Pointer behavior | Historical handshake row |
| --- | --- | --- |
| Successful commit (`MARK_CAMPAIGN_READY`) | set to new version | new row `CAMPAIGN_READY` |
| `REVOKE_CAMPAIGN_READY` on current | clear pointer | status → `REVOKED` |
| `REVOKE_CAMPAIGN_READY` on non-current `CAMPAIGN_READY` row | pointer unchanged | that row status → `REVOKED` |
| New clean QA `ACCEPT` (pointer move) | **clear** handshake pointer if set | historical handshake status/document **unchanged** |
| QA `WAIVE_AND_ACCEPT` (pointer move to exception report) | **clear** handshake pointer if set | historical handshake unchanged; exception report remains **ineligible** for future commit |
| Later Phase 6 creative `APPROVE` | clear QA pointer **and** clear handshake pointer if set | historical QA + handshake rows unchanged |

**No auto handshake** on any pointer event.

New commit while `CurrentCampaignReadinessHandshakeVersionId` is
non-null → **400** (revoke or wait for clear first).

Revoke is allowed for **any** handshake row currently in
`CAMPAIGN_READY` (current or historical). Revoke **never** deletes or
changes `CampaignPlacement` / `CampaignPlacementRun` rows (they remain
`PLANNED` / `COMPLETED`+`PLANNED`).

## Canonical document — `campaign-readiness-handshake.v1`

Required conceptual contents (normative; field names may be camelCase in
JSON):

- `schemaVersion`: `campaign-readiness-handshake.v1`
- `disclaimer`: exact locked string
- optional `marker`: `SYNTHETIC DEVELOPMENT CAMPAIGN READINESS` when
  synthetic
- pins object: QA report id, QA ACCEPT decision id, creative package id,
  package DocumentJson SHA-256, creative APPROVE decision id, selected
  variant id, selected asset id + sha256/meta, concept/DNA/color/
  research lineage ids + version numbers, blissMatchId, campaignId,
  contentItemId, adInventorySlotId
- `rules`: embedded `campaign-readiness-rules.v1` output (all codes)
- snapshots: bounded metadata snapshots of QA/package/match/campaign/
  content/slot needed for audit (ids, statuses, names/labels as
  appropriate) — **no external URLs**, no bytes, no base64, no markup
- `campaignPlacementId`, `campaignPlacementRunId`
- `rationale`, acknowledgement booleans
- explicit planning disclosures: no reservation; not activation; not
  legal/measurement/payment approval

Merge/validate must reject unknown forbidden keys (URLs, base64, markup,
legal clearance claims, AI provider blocks, client status overrides).

## Eligibility preview (read-only)

`GET /api/wedding-planner/workspaces/{id}/campaign-readiness/eligibility`

- Scoped to the workspace advertiser (cross-tenant → **404**).
- Read-only: creates **no** records, runs **no** AI, writes **no** audit
  beyond optional bounded access audit if the platform already audits
  reads (not required).
- Returns:
  - clean QA state: whether current QA pointer is present; report id/
    status; whether clean `ACCEPTED` vs exception/other; whether clean
    ACCEPT decision present; package/variant/asset readiness summary;
    synthetic upstream flags; current handshake pointer if any;
  - candidate approved matches / DRAFT campaigns / creator content /
    slots metadata compatible under the Bliss checks above;
  - **omit external URLs**;
  - structural candidate grouping (e.g. group slots under content under
    match/creator; campaigns listed with opportunity compatibility
    flags);
  - may include `IsAvailable` as **informational** metadata with explicit
    non-reservation labeling; must not imply reservation or filter-only
    semantics.
- Does not create handshake, placement, run, or decision rows.

## API

Phase 1–7 routes remain unchanged in public behavior. Add under
`/api/wedding-planner`:

- `GET /workspaces/{id}/campaign-readiness/eligibility`
- `GET /workspaces/{id}/campaign-readiness-handshakes`
- `GET /campaign-readiness-handshakes/{id}`
- `GET /campaign-readiness-handshakes/{id}/decisions`
- `POST /workspaces/{id}/campaign-readiness-handshakes`
- `POST /campaign-readiness-handshakes/{id}/decisions` (**REVOKE only**)

**No** Phase 8 agent-run API. **No** PUT/PATCH/DELETE for handshake
document payloads. **No** endpoint that mutates creative packages or QA
reports to campaign-ready. **No** endpoint that reserves slots or
activates campaigns.

### Commit body example

```json
{
  "blissMatchId": "11111111-1111-1111-1111-111111111111",
  "campaignId": "22222222-2222-2222-2222-222222222222",
  "contentItemId": "33333333-3333-3333-3333-333333333333",
  "adInventorySlotId": "44444444-4444-4444-4444-444444444444",
  "rationale": "Clean ACCEPTED QA bound to approved match and compatible draft campaign inventory for planning.",
  "disclaimerAcknowledged": true,
  "syntheticMarkerAcknowledged": true,
  "sourceSystem": "OPERATIONS",
  "idempotencyKey": "cr-handshake-001"
}
```

### Revoke body example

```json
{
  "decision": "REVOKE_CAMPAIGN_READY",
  "rationale": "Planning intent withdrawn; placement remains historical PLANNED only.",
  "sourceSystem": "OPERATIONS",
  "idempotencyKey": "cr-revoke-001"
}
```

Revoke endpoint rejects any decision value other than
`REVOKE_CAMPAIGN_READY` (**400**). `MARK_CAMPAIGN_READY` is not accepted
on the decisions route — it exists only as the atomic companion of
`POST .../campaign-readiness-handshakes`.

## Commit saga (normative)

1. Authorize operator/admin; resolve workspace; cross-tenant → 404.
2. Reject if current handshake pointer non-null (unless exact idempotent
   replay of existing source/key).
3. Reject forbidden body fields; validate rationale ≤2000;
   `disclaimerAcknowledged=true`; idempotency key length for derived
   suffixes.
4. Load and pin current clean QA + ACCEPT decision + current approved
   package/SHA/APPROVE/variant/asset/provenance + selected Bliss graph.
5. Run `campaign-readiness-rules.v1`; persist findings into the to-be-
   written document; if any BLOCK → **400**, no writes.
6. Begin one DB transaction.
7. Call shared placement bind core (no nested commit) with derived
   `{key}:PLACEMENT` → insert `CampaignPlacement` `PLANNED` +
   `CampaignPlacementRun` `COMPLETED`/`PLANNED`; apply campaign
   opportunity null→match assignment when needed.
8. Insert handshake version `CAMPAIGN_READY` with canonical document
   (pins, rules, placement/run ids, rationale/ack, exact disclaimer,
   synthetic marker if applicable).
9. Insert `MARK_CAMPAIGN_READY` decision with `{key}:MARK`.
10. Set `CurrentCampaignReadinessHandshakeVersionId`.
11. Audit; commit transaction.
12. Return 201 (`isReplay=false`) or 200 on exact replay.

Any exception after step 6 → full rollback; **no partial**.

## Failure and audit

| Condition | Behavior |
| --- | --- |
| Anonymous (OIDC on) | 401 |
| Advertiser/reviewer/viewer on commit or revoke | 403 |
| Missing or cross-tenant id | 404 |
| Current handshake pointer set (non-replay) | 400 |
| Exception QA / waive decision / missing pins / any rules BLOCK / synthetic policy fail / disclaimer ack false / rationale empty/>2000 / forbidden overrides / derived key too long / illegal revoke state | 400 |
| Idempotent replay | existing handshake/decision/placement/run; no second rows; counts unchanged |
| Mid-transaction failure | full rollback; no placement, run, handshake, decision, or pointer mutation |

Audit actions (append-only): campaign-readiness eligibility read
(optional); handshake committed/replayed; rules findings recorded;
placement/run created via Phase 8 path; MARK decision recorded; pointer
set/cleared; revoke recorded; pointer cleared by QA ACCEPT/WAIVE or
creative APPROVE hooks. Events carry actor, request id,
workspace/advertiser, handshake/decision/placement/run ids. Secrets are
never stored. **Zero** AI token/cost fields for Phase 8.

## UI requirements

### Public (`frontend/public`)

- **Read-only** campaign-readiness surfaces when an advertiser-capable
  session can access the workspace.
- Show current clean QA / handshake pointer state and historical
  handshake list/detail **without** candidate selectors and **without**
  commit/revoke controls (hide or permanently disable).
- Display the Phase 8 disclaimer **verbatim**.
- Labels must state: **no reservation**, **no activation**, **no AI**.
- When synthetic handshake marker present, show
  `SYNTHETIC DEVELOPMENT CAMPAIGN READINESS` conspicuously.
- Do not imply legal clearance, measurement, delivery, payment, or
  matching recompute.
- Preserve Phase 1–7 surfaces.

### Operations (`frontend/operations`)

- Full eligibility preview with structural candidate grouping.
- Select exact match/campaign/content/slot ids; commit with rationale,
  exact disclaimer acknowledgement, and synthetic acknowledgement when
  required.
- List handshakes/decisions; revoke with rationale.
- Show rules PASS/BLOCK codes, pins, placement/run ids, pointer state,
  clean-vs-exception QA eligibility, synthetic markers.
- Labels: no reservation / no activation / no AI; operator/admin only
  for commit/revoke.
- Do not expose AI run panels for Phase 8 (there are none).

## Data model

- All new FKs use `ON DELETE RESTRICT`.
- Monotonic `VersionNumber` per workspace for handshake versions.
- Unique `(WorkspaceId, VersionNumber)`.
- Unique `(SourceSystem, IdempotencyKey)` on handshake versions.
- Unique `(SourceSystem, IdempotencyKey)` on decisions.
- Placement/run uniqueness remains existing Bliss
  `(SourceSystem, IdempotencyKey)` on `CampaignPlacementRun`.
- Workspace pointer FK to handshake version nullable, Restrict on delete
  semantics consistent with prior WP pointers.
- No destructive rewrite of Phase 1–7 history.

## Tests (exhaustive minimum)

**Rules blockers**

- Each of the 16 `CR_*` codes independently BLOCKs commit when its
  assertion fails; overall PASS only when all PASS.
- No WARN severity emitted by `campaign-readiness-rules.v1`.
- Disclaimer merge/validate exact-match enforced.

**Clean vs exception**

- Clean `ACCEPTED` + `ACCEPT` decision eligible.
- `ACCEPTED_WITH_EXCEPTION` / waive path rejected fail-closed; no Phase 8
  waiver.
- Missing QA pointer / non-ACCEPTED statuses rejected.

**Auth / tenant**

- Operator/admin commit + revoke succeed when eligible.
- Advertiser/reviewer/viewer commit/revoke → 403.
- Advertiser read own eligibility/handshakes; cross-tenant → 404.
- Reviewer may read under explicit handshake read policy.
- Anonymous → 401 when OIDC on.

**Shared core parity**

- Existing Bliss `BindAsync` public behavior unchanged (approved match,
  ACTIVE opportunity, DRAFT campaign, creator/slot checks, opportunity
  null assignment, multi-bind, replay).
- Phase 8 path uses the same compatibility checks and produces the same
  placement/run status/outcome shapes.

**Atomicity / replay**

- Forced failure after placement insert inside Phase 8 transaction →
  zero durable placement/run/handshake/decision/pointer changes.
- Exact replay → same ids; unchanged counts; no second writes.

**Pointer invalidation**

- New QA `ACCEPT` clears handshake pointer; historical handshake
  status/document unchanged.
- QA `WAIVE_AND_ACCEPT` clears handshake pointer; exception remains
  ineligible.
- Later Phase 6 creative `APPROVE` clears QA + handshake pointers;
  historical rows unchanged.
- New commit rejected while handshake pointer non-null; succeeds after
  revoke/clear.

**Revoke / placement stability**

- Revoke sets handshake `REVOKED`; clears pointer if current.
- Revoke leaves `CampaignPlacement` `PLANNED` and run
  `COMPLETED`/`PLANNED` unchanged (no delete).
- `IsAvailable` unchanged across commit, revoke, and pointer clears.
- Match status and scores unchanged across commit/revoke.
- Campaign opportunity assignment matches Phase 7 null→set semantics;
  conflicting opportunity rejected.

**Zero AI**

- Success path creates **0** new AI calls/runs/profiles/prompts/
  providers; architecture tests prove Phase 8 sources do not invoke
  `IWeddingPlannerAiProvider` or create Phase 8 agent runs.

**UI safety**

- Public UI has no candidate selectors and no commit/revoke controls.
- Operations shows exact disclaimer + no reservation / no activation /
  no AI labels.
- Synthetic marker conspicuous when applicable.

## Explicit exclusions

- Phase 9 measurement / learning
- Placement activation / scheduling / delivery / publication / payment
- Slot reservation or any `IsAvailable` gate/mutation
- Bliss matching score recompute or match status mutation beyond existing
  placement bind semantics
- Legal / compliance / accessibility certification products
- Alpha Auto / n8n / GHL authority or coupling
- Any new AI roles, profiles, calls, runs, prompts, or providers
- Mutating Phase 6 creative packages or Phase 7 QA reports to
  `CAMPAIGN_READY`
- Phase 8 waiver of `ACCEPTED_WITH_EXCEPTION`
- Auto-creating handshakes on QA ACCEPT/WAIVE or creative APPROVE
- Widening generic WeddingPlanner `CanWrite` for advertiser/reviewer
  commit/revoke
- Invented format/slot mapping beyond explicit id bind
- Agent-run API for Phase 8

## Known residual risks (accepted for Phase 8)

1. **Planning-without-reservation confusion:** Operators may treat
   `PLANNED` + `CAMPAIGN_READY` as inventory hold. Mitigation: locked
   disclaimer, UI labels, `IsAvailable` unchanged, eligibility
   non-reservation disclosure. Residual: organizational misread of
   planning state.
2. **Pointer vs history confusion:** Clearing handshake pointer on QA/
   creative supersession may look like revoke. Mitigation: historical
   handshake status/document remain; only explicit revoke flips status
   to `REVOKED`; UI must separate current pointer from history.
3. **Multi-bind overlap:** Multiple planned placements may reference the
   same slot/match. Mitigation: preserve Bliss multi-bind; disclose no
   exclusivity. Residual: over-booking at planning layer until a later
   activation phase.
4. **Clean-accept rubber stamp:** Phase 8 trusts Phase 7 clean ACCEPT as
   prerequisite without re-doing visual review. Mitigation: pins +
   asset revalidation + disclaimer. Residual: inherited Phase 7
   attestation limits.
5. **Shared-core regression risk:** Refactor of
   `CampaignPlacementService` could alter public bind behavior.
   Mitigation: parity tests for existing BindAsync cases required green
   before Phase 8 acceptance.

These residuals do not expand Phase 8 scope into activation,
reservation, measurement, legal certification, or AI.

## Acceptance gate

Contract → implementation → automated tests → evidence → human review and
acceptance. Phase 8 does not auto-advance to Phase 9. Phases 1–7 behavior
and Bliss matching boundaries remain preserved, with the shared placement
bind core refactor required to keep public `BindAsync` behavior
unchanged while enabling atomic Phase 8 commits.
