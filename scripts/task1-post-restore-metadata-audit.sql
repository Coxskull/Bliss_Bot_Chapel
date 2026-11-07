-- ALPHA Task #1: READ-ONLY metadata checks after an owner-approved REAL restore
-- Run only against the verified private, separate recovery DATABASE.
-- Never use on production. No customer rows or secrets returned.
BEGIN TRANSACTION READ ONLY;
SELECT current_database() AS restored_database,
       current_setting('server_version') AS postgres_version,
       pg_database_size(current_database())::bigint AS database_bytes;
SELECT n.nspname AS schema_name,
       COUNT(*) FILTER (WHERE c.relkind IN ('r','p')) AS tables,
       COUNT(*) FILTER (WHERE c.relkind IN ('r','p') AND c.relrowsecurity) AS rls_enabled_tables,
       COUNT(*) FILTER (WHERE c.relkind IN ('r','p') AND c.relforcerowsecurity) AS force_rls_tables
FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname <> 'information_schema'
GROUP BY n.nspname ORDER BY n.nspname;
SELECT connamespace::regnamespace::text AS schema_name,
       COUNT(*) FILTER (WHERE contype='f') AS foreign_keys,
       COUNT(*) FILTER (WHERE contype='p') AS primary_keys,
       COUNT(*) FILTER (WHERE contype='u') AS unique_constraints
FROM pg_constraint GROUP BY connamespace ORDER BY schema_name;
SELECT schemaname, COUNT(*) AS policies
FROM pg_policies GROUP BY schemaname ORDER BY schemaname;
SELECT n.nspname AS schema_name,
       COUNT(*) FILTER (WHERE c.relname ILIKE '%receipt%') AS receipt_tables,
       COUNT(*) FILTER (WHERE c.relname ILIKE '%sailor%') AS sailor_tables,
       COUNT(*) FILTER (WHERE c.relname ILIKE '%factory%') AS factory_tables
FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
WHERE c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%'
GROUP BY n.nspname ORDER BY n.nspname;
SELECT extname, extversion FROM pg_extension ORDER BY extname;
SELECT COUNT(*) AS untrusted_or_cross_system_foreign_servers FROM pg_foreign_server;
SELECT COUNT(*) AS event_triggers FROM pg_event_trigger;
SELECT COUNT(*) AS logical_replication_subscriptions FROM pg_subscription;
ROLLBACK;
-- Required separately: table-by-table exact counts and cryptographic content
-- fingerprints from source vs restored database, representative relationships,
-- role-level access-denied checks, triggers/side effects verified disabled,
-- auth and MFA tested later, physical snapshot recovery later.
