-- Bliss Bot Chapel Phase 1 database verification
-- Run against the Phase 1 PostgreSQL database after applying Phase1Foundation.

-- All public tables
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY table_name;

-- Foreign-key verification
SELECT
    tc.table_name,
    kcu.column_name,
    ccu.table_name AS referenced_table,
    ccu.column_name AS referenced_column
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
    ON tc.constraint_name = kcu.constraint_name
JOIN information_schema.constraint_column_usage ccu
    ON tc.constraint_name = ccu.constraint_name
WHERE tc.constraint_type = 'FOREIGN KEY'
ORDER BY tc.table_name;

-- Unique constraints (PKs appear as PRIMARY KEY, not UNIQUE)
SELECT
    table_name,
    constraint_name
FROM information_schema.table_constraints
WHERE constraint_type = 'UNIQUE'
ORDER BY table_name;

-- Same unique-constraint check limited to the application schema
SELECT
    table_name,
    constraint_name
FROM information_schema.table_constraints
WHERE constraint_type = 'UNIQUE'
  AND table_schema = 'public'
ORDER BY table_name;

-- Cardinality proofs: one Creator, many ContentItems
SELECT c."Id" AS creator_id, COUNT(ci."Id") AS content_item_count
FROM "Creators" c
LEFT JOIN "ContentItems" ci ON ci."CreatorId" = c."Id"
GROUP BY c."Id"
HAVING COUNT(ci."Id") > 1;

-- Cardinality proofs: one ContentItem, many AdInventorySlots
SELECT ci."Id" AS content_item_id, COUNT(s."Id") AS slot_count
FROM "ContentItems" ci
LEFT JOIN "AdInventorySlots" s ON s."ContentItemId" = ci."Id"
GROUP BY ci."Id"
HAVING COUNT(s."Id") > 1;

-- Cardinality proofs: one Creator, many BlissMatches
SELECT c."Id" AS creator_id, COUNT(m."Id") AS match_count
FROM "Creators" c
LEFT JOIN "BlissMatches" m ON m."CreatorId" = c."Id"
GROUP BY c."Id"
HAVING COUNT(m."Id") > 1;

-- Cardinality proofs: one ContentItem, many CampaignPlacements
SELECT ci."Id" AS content_item_id, COUNT(p."Id") AS placement_count
FROM "ContentItems" ci
LEFT JOIN "CampaignPlacements" p ON p."ContentItemId" = ci."Id"
GROUP BY ci."Id"
HAVING COUNT(p."Id") > 1;

-- Confirm BlissMatch.CreatorId is indexed but not unique
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'BlissMatches'
ORDER BY indexname;

-- Confirm CampaignPlacement.ContentItemId is indexed but not unique
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'CampaignPlacements'
ORDER BY indexname;

-- Confirm FemalePercentage is nullable
SELECT column_name, is_nullable, data_type
FROM information_schema.columns
WHERE table_name = 'Creators'
  AND column_name IN ('FemalePercentage', 'MalePercentage');
