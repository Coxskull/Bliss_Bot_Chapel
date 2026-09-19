# PHASE-1-DATABASE-PROOF

## Primary keys

All domain tables use `uuid` primary keys.

## Nullability

- `Creators.FemalePercentage` and `MalePercentage` are nullable `numeric`.
- UNKNOWN is stored as SQL NULL, not `0`.
- `DataProvenances.ConfidenceLevel` defaults to `'UNKNOWN'`.

## Indexes

Non-unique indexes exist on all specified foreign keys, including:

- `BlissMatches.CreatorId` (NOT UNIQUE)
- `CampaignPlacements.ContentItemId` (NOT UNIQUE)

External lookup indexes exist for profile, content, program, opportunity, and network account identifiers.

## Delete behavior

Major foreign keys use `ON DELETE RESTRICT` so historical match, placement, and access rows are not cascade-deleted.

## Uniqueness

The generated `phase1.sql` contains no `UNIQUE` constraints besides primary keys. There is no unique constraint that would limit a Creator to one BlissMatch or a ContentItem to one CampaignPlacement.

## Cardinality proofs (SQL)

See `docs/sql/phase1-verification.sql` for:

- one Creator → many ContentItems
- one ContentItem → many AdInventorySlots
- one Creator → many BlissMatches
- one ContentItem → many CampaignPlacements

Automated proofs live in `Bliss.Tests`.

## Seed data

`Phase1DataSeeder` creates fictional CRT-TEST-001 data:

- 1 primary creator (Test Creator, PH, podcast, 100k audience, 70/30)
- 2 content items
- 4 slots on content 1 (`PRE_ROLL`, `MID_ROLL`, `LOWER_THIRD`, `POST_ROLL`)
- 1 advertiser, 1 program, 3 opportunities
- 3 BlissMatches for the same creator
- NetworkAccess = APPROVED and ProgramAccess = UNKNOWN
- provenance for FemalePercentage
- a second creator with null demographics
