# Wedding Planner Phase 3 — Engineering Contract

Status: authorized for implementation by the request to proceed to Phase 3.

## Objective

Add **Alpha Color Intelligence** as deterministic Wedding Planner software:

1. an authenticated human supplies explicit seed colors for an authorized
   workspace that already has an approved Brand DNA version;
2. a pure, versioned algorithm emits an immutable `color-profile.v1` palette
   with `aci.hsl.v1` roles and WCAG contrast evidence; and
3. only a human `APPROVE` or `REJECT` decision may change current-approved
   pointer metadata.

PostgreSQL remains permanent memory. Phase 3 adds **0** new logical AI roles
and **0** new executable workers. It must not call
`IWeddingPlannerAiProvider`, must not create `WeddingPlannerAgentRun` rows,
and must not invent colors from Brand DNA text.

## Assumptions

1. Phase 1 foundation and Phase 2 Conversation + Brand DNA remain accepted and
   unchanged in behavior except for additive color-profile tables/pointers,
   routes, UI surfaces, tests, and audit action names.
2. A workspace may compute a color profile only when
   `CurrentApprovedBrandDnaVersionId` is non-null. That Brand DNA id is stored
   as provenance. DocumentJson/Summary are never parsed for hues, hex values,
   or palette guidance.
3. Seed colors are human-authored. Omitted optional seeds are derived only by
   the deterministic geometry below, never by an AI provider.
4. WCAG relative-luminance arithmetic on this profile is **evidence**, not a
   legal, regulatory, or accessibility certification. Passing AA/AAA flags in
   evidence does not certify a live UI.
5. HSL offsets used to derive secondary, accent, and neutrals are **geometry**,
   not psychological, cultural, or brand-strategy recommendations.
6. Bliss matching, review, placement, Alpha Auto, and n8n authority remain
   out of scope and untouched.

## Workforce allocation

| Item | Phase 3 allocation |
| --- | --- |
| New logical AI roles | 0 |
| New executable workers | 0 |
| Provider calls | none; `IWeddingPlannerAiProvider` must not be invoked |
| Agent runs | none; no `WeddingPlannerAgentRun` insert |
| Deterministic service | `aci.hsl.v1` Color Intelligence inside Wedding Planner application code |
| Human authority | humans supply seeds and alone approve/reject profiles |

Phase 2 Concierge and Brand DNA Interpreter roles remain as allocated. Phase 3
does not extend their tools, prompts, or outputs into Color Intelligence.

## Authority

- Advertisers act only inside the `advertiser_id`-bound workspace.
- Operators/admins may act across advertisers; viewers cannot write.
- Only an authenticated advertiser or chapel operator/admin may compute a
  profile or record an `APPROVE` / `REJECT` decision.
- Cross-tenant resource lookup returns **404**, consistent with Phases 1–2.
- Anonymous writes/reads of protected endpoints return **401** when OIDC is
  enabled.
- Authenticated identities without write authority (e.g. viewer) return
  **403** on compute and decision writes.

## Inputs

Compute request fields (all hex inputs accepted as `#RGB` or `#RRGGBB`, case
insensitive):

| Field | Required | Default / derivation |
| --- | --- | --- |
| `PrimaryHex` | yes | — |
| `SecondaryHex` | no | derived from primary via `aci.hsl.v1` |
| `AccentHex` | no | derived from primary via `aci.hsl.v1` |
| `BackgroundHex` | no | `#FFFFFF` |
| `SurfaceHex` | no | canonical background |
| `Notes` | no | omitted / null; human free text, not algorithm input beyond storage |
| `SourceSystem` | yes | same idempotency pattern as Phase 1–2 |
| `IdempotencyKey` | yes | unique with `SourceSystem` for compute |

Canonicalization before hash and persistence:

1. trim whitespace;
2. require leading `#`;
3. expand `#RGB` → `#RRGGBB` by duplicating each nibble;
4. reject any other form (named colors, `rgb()`, `#RRGGBBAA`, empty);
5. uppercase to `#RRGGBB`.

Canonical input JSON for hashing is a stable, sorted-key object of the
canonical seed hexes plus whether each optional field was supplied vs
defaulted/derived, the approved Brand DNA version id, algorithm version, and
schema version. Notes are excluded from the hash. Persist both the canonical
JSON string and its lowercase hex SHA-256 digest on the profile version.

## Algorithm `aci.hsl.v1`

### Color space helpers

Work in sRGB 8-bit channels `R,G,B ∈ {0…255}` and normalized `r,g,b = C/255`.

**sRGB linearization** (exact WCAG 2 formula):

- if `c_srgb ≤ 0.04045` then `c_lin = c_srgb / 12.92`
- else `c_lin = ((c_srgb + 0.055) / 1.055) ^ 2.4`

**Relative luminance**:

`L = 0.2126·R_lin + 0.7152·G_lin + 0.0722·B_lin`

**Contrast ratio** between colors A and B:

`ratio = (L_lighter + 0.05) / (L_darker + 0.05)`

where `L_lighter = max(L_A, L_B)` and `L_darker = min(L_A, L_B)`.

**Stored rounding**

| Quantity | Storage |
| --- | --- |
| Hex | uppercase `#RRGGBB` |
| HSL `h` | degrees in `[0, 360)`, round half-away-from-zero to 1 decimal |
| HSL `s`,`l` | percent in `[0, 100]`, round half-away-from-zero to 1 decimal |
| Relative luminance | round half-away-from-zero to 6 decimal places |
| Contrast ratio | round half-away-from-zero to 2 decimal places |
| WCAG flags | computed from the **stored** 2-decimal ratio |

HSL conversions use the standard sRGB↔HSL geometry (no CIELAB, no OkLCh).
Hue arithmetic uses degrees modulo 360.

### Seed derivation

Let `P` be canonical primary HSL `(Hp, Sp, Lp)`.

- If `SecondaryHex` omitted: secondary = HSL `((Hp + 180) mod 360, Sp, Lp)` → hex.
- If `AccentHex` omitted: accent = HSL `((Hp + 30) mod 360, Sp, Lp)` → hex.
- Background defaults to `#FFFFFF` when omitted.
- Surface defaults to the canonical background when omitted.

### On-colors

For each of `primary`, `secondary`, `accent`, `background`, `surface`, choose
`on*` as `#000000` or `#FFFFFF` by **higher** contrast ratio against that role.
Ties (equal stored ratios) resolve to `#000000`.

### Neutrals

Build a deterministic neutral ladder from background luminance toward black,
independent of brand psychology:

| Role | Rule |
| --- | --- |
| `neutral50` | mix background toward `#000000` at 4% |
| `neutral100` | 8% |
| `neutral200` | 16% |
| `neutral400` | 32% |
| `neutral600` | 52% |
| `neutral800` | 72% |
| `neutral900` | 88% |

Mixing is channel-wise in sRGB 8-bit space:
`channel = round(bg + (0 - bg) * t)` with `t` as above, then re-canonicalize.
`round` is half-away-from-zero to integer `0…255`.

### Contrast evidence pairs (mandatory)

Emit one evidence row per pair:

| Foreground role | Background role |
| --- | --- |
| `onPrimary` | `primary` |
| `onSecondary` | `secondary` |
| `onAccent` | `accent` |
| `onBackground` | `background` |
| `onSurface` | `surface` |
| `neutral900` | `background` |
| `neutral800` | `surface` |
| `primary` | `background` |
| `secondary` | `background` |
| `accent` | `background` |

Each row stores: fg role, bg role, fg hex, bg hex, stored ratio, and boolean
flags for WCAG 2 AA/AAA normal text and large text using the stored ratio:

- AA normal ≥ 4.50; AA large ≥ 3.00
- AAA normal ≥ 7.00; AAA large ≥ 4.50

Evidence JSON must include a fixed disclaimer string stating that these flags
are arithmetic evidence only and are not accessibility certification.

## Output schema `color-profile.v1`

`DocumentJson` is an immutable JSON object:

```json
{
  "schemaVersion": "color-profile.v1",
  "algorithmVersion": "aci.hsl.v1",
  "provenance": {
    "approvedBrandDnaVersionId": "<guid>",
    "approvedBrandDnaVersionNumber": <int>
  },
  "seeds": {
    "primaryHex": "#RRGGBB",
    "secondaryHex": "#RRGGBB",
    "accentHex": "#RRGGBB",
    "backgroundHex": "#RRGGBB",
    "surfaceHex": "#RRGGBB",
    "secondaryDerived": true,
    "accentDerived": true,
    "backgroundDefaulted": true,
    "surfaceDefaulted": true,
    "notes": null
  },
  "palette": {
    "primary": { "hex": "#RRGGBB", "hsl": { "h": 0.0, "s": 0.0, "l": 0.0 }, "luminance": 0.0 },
    "secondary": { "hex": "#RRGGBB", "hsl": { "h": 0.0, "s": 0.0, "l": 0.0 }, "luminance": 0.0 },
    "accent": { "hex": "#RRGGBB", "hsl": { "h": 0.0, "s": 0.0, "l": 0.0 }, "luminance": 0.0 },
    "background": { "hex": "#RRGGBB", "hsl": { "h": 0.0, "s": 0.0, "l": 0.0 }, "luminance": 0.0 },
    "surface": { "hex": "#RRGGBB", "hsl": { "h": 0.0, "s": 0.0, "l": 0.0 }, "luminance": 0.0 },
    "onPrimary": { "hex": "#000000" },
    "onSecondary": { "hex": "#FFFFFF" },
    "onAccent": { "hex": "#000000" },
    "onBackground": { "hex": "#000000" },
    "onSurface": { "hex": "#000000" },
    "neutral50": { "hex": "#RRGGBB" },
    "neutral100": { "hex": "#RRGGBB" },
    "neutral200": { "hex": "#RRGGBB" },
    "neutral400": { "hex": "#RRGGBB" },
    "neutral600": { "hex": "#RRGGBB" },
    "neutral800": { "hex": "#RRGGBB" },
    "neutral900": { "hex": "#RRGGBB" }
  },
  "contrastEvidence": {
    "disclaimer": "WCAG contrast figures are deterministic arithmetic evidence only and are not a legal or accessibility certification of any rendered UI.",
    "pairs": [
      {
        "foregroundRole": "onPrimary",
        "backgroundRole": "primary",
        "foregroundHex": "#000000",
        "backgroundHex": "#RRGGBB",
        "ratio": 4.52,
        "aaNormal": true,
        "aaLarge": true,
        "aaaNormal": false,
        "aaaLarge": true
      }
    ]
  },
  "geometryDisclaimer": "HSL secondary/accent/neutral offsets are deterministic geometry, not psychological or brand-strategy recommendations."
}
```

`Summary` is a short server-built string naming schema/algorithm versions,
canonical primary hex, and whether secondary/accent were derived.

## Durable records

### `WeddingPlannerColorProfileVersion`

Immutable snapshot (no PUT/PATCH of payload fields after insert):

- advertiser/workspace scope
- monotonic `VersionNumber` per workspace
- `SchemaVersion` = `color-profile.v1`
- `AlgorithmVersion` = `aci.hsl.v1`
- `ApprovedBrandDnaVersionId` (required FK; provenance only)
- `DocumentJson`, `Summary`
- canonical `InputJson`, `InputSha256`
- `Status`: `PROPOSED` | `APPROVED` | `REJECTED` | `SUPERSEDED`
- `SourceSystem`, `IdempotencyKey`, `CreatedAt`
- actor metadata for the compute caller (`ActorType`, `ActorLabel`)

**No** agent-run FK. **No** provider, model, adapter, worker, token, or cost
fields. The version row itself is the execution receipt.

Unique constraints: `(WorkspaceId, VersionNumber)`;
`(SourceSystem, IdempotencyKey)`.

### `WeddingPlannerColorProfileDecision`

Immutable human decision:

- advertiser/workspace/profile version scope
- `Decision`: `APPROVE` | `REJECT`
- required non-empty `Rationale`
- actor type/label, source/idempotency, timestamp

Unique `(SourceSystem, IdempotencyKey)`.

### Workspace pointer

Add nullable `CurrentApprovedColorProfileVersionId` on
`WeddingPlannerWorkspace`. Approval sets it. Approving a later version sets
the prior current profile to `SUPERSEDED` without mutating `DocumentJson`,
`Summary`, `InputJson`, or `InputSha256`. Rejected versions never become
current. Only `PROPOSED` versions accept a first decision; subsequent decision
attempts on a non-`PROPOSED` version are **400**.

## Flow

### Compute

1. authorize workspace write access;
2. require current-approved Brand DNA; else **400**;
3. replay existing profile when `(SourceSystem, IdempotencyKey)` exists;
4. validate and canonicalize seeds;
5. run `aci.hsl.v1` → `color-profile.v1`;
6. insert next immutable `PROPOSED` version with input JSON/SHA-256 evidence;
7. append audit `COLOR_PROFILE_PROPOSED` (or replay audit on idempotent hit).

### Human decision

1. authorize write access to the profile's workspace;
2. replay decision by idempotency when present;
3. require non-empty rationale;
4. insert decision; on `APPROVE`, update status/pointer and supersede prior
   current; on `REJECT`, set `REJECTED` only;
5. audit approval, rejection, or replay.

## API

Phase 1–2 routes remain unchanged. Add under `/api/wedding-planner`:

- `POST /workspaces/{id}/color-profiles/compute`
- `GET /workspaces/{id}/color-profiles`
- `GET /color-profiles/{id}`
- `POST /color-profiles/{id}/decisions`

No PUT, PATCH, or DELETE for profile payloads. List responses include status,
version number, summary, algorithm/schema versions, provenance Brand DNA id,
and whether the row is the workspace current-approved profile.

## Failure and audit

| Condition | Behavior |
| --- | --- |
| Anonymous (OIDC on) | 401 |
| Viewer / no write authority | 403 on compute and decisions |
| Missing or cross-tenant id | 404 |
| Invalid hex, missing primary, empty rationale, illegal status transition, no approved Brand DNA | 400 |
| Idempotent replay | 200/201-equivalent existing row; no second version/decision; audit replay |

Audit actions (append-only `WeddingPlannerAuditEvents`): profile proposed,
approved, rejected, superseded (when a prior current is displaced), and
replay. Events carry actor, request id, workspace/advertiser, and profile
version id. Secrets are never stored.

## UI requirements

### Public (`frontend/public`)

- Show Color Intelligence only when an advertiser-capable session can access
  the workspace and a current-approved Brand DNA exists (or clearly disable
  compute with that prerequisite stated).
- Controls: seed inputs (primary required; secondary/accent/background/surface
  optional), optional notes, compute, list prior profiles, approve/reject with
  required rationale.
- Display palette swatches and contrast evidence pairs from the stored
  document; show the WCAG/geometry disclaimers verbatim from the document.
- Do not imply AI generation, certification, or matching changes.
- Preserve Phase 2 Concierge / Brand DNA surfaces; do not remove them.

### Operations (`frontend/operations`)

- List color profile versions and decisions for the selected workspace.
- Expose compute and approve/reject for operator/admin the same way Brand DNA
  decisions are exposed.
- Show algorithm/schema versions, input SHA-256, provenance Brand DNA id, and
  current-approved marker.

## Tests (exhaustive minimum)

**Algorithm unit tests**

- `#abc` → `#AABBCC`; mixed-case → uppercase `#RRGGBB`.
- Invalid hex forms rejected.
- Exact luminance/contrast fixtures for known pairs (e.g. black on white =
  21.00; white on white = 1.00).
- On-color selection picks the higher-contrast of black/white; tie → black.
- Omitted secondary = hue+180; omitted accent = hue+30; same S/L as primary.
- Background default `#FFFFFF`; surface default equals background.
- Neutral ladder matches channel-mix percentages.
- Identical canonical seeds produce identical DocumentJson and InputSha256.
- Notes change does not change InputSha256; notes still persist on the version.
- WCAG flags follow stored 2-decimal thresholds.
- Document includes both disclaimers.

**API / persistence**

- Compute requires approved Brand DNA; otherwise 400.
- Compute creates `PROPOSED` immutable version; version numbers increase.
- Idempotent source+key replay returns the same version; no duplicate row.
- GET list and GET by id return authorized rows.
- Approve with rationale sets pointer; second approve supersedes prior without
  payload mutation.
- Reject with rationale never sets pointer.
- Empty rationale 400; decision on non-`PROPOSED` 400.
- Advertiser A cannot compute/list/get/decide B's profiles (404).
- Anonymous 401; viewer 403 on writes.
- No `WeddingPlannerAgentRun` created; Color Intelligence path does not resolve
  or call `IWeddingPlannerAiProvider`.

**Architecture / regression**

- Existing Bliss matching, review, placement, and Wedding Planner Phase 1–2
  tests remain green.
- Architecture tests prove Phase 3 Color Intelligence sources do not reference
  deterministic match evaluation, Alpha Auto, or n8n authority, and do not call
  the AI provider interface.

## Explicit exclusions

- New AI roles, prompt packs, provider adapters, or agent runs
- Parsing Brand DNA (or conversation history) to invent colors
- Curator research, concepts/prototypes, image generation
- Mature creative-department roles, Chaperone/QA AI
- Bliss campaign handshake, measurement/learning
- GHL coupling, n8n authority, Alpha Auto
- Any change to deterministic Bliss scoring, review, or placement arithmetic
- Accessibility certification products, legal claims, or live-theme mutation of
  Bliss matching UIs from these profiles
- CIELAB/OkLCh/perceptual harmony engines beyond the stated HSL geometry

## Acceptance gate

Contract → implementation → automated tests → evidence → human review and
acceptance. Phase 3 does not auto-advance to Phase 4. Phase 2 behavior and all
Bliss matching boundaries remain preserved.
