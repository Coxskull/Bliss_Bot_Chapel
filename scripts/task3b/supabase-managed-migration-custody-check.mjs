// ALPHA Task #3B: deterministic OFFLINE check of migration-source custody.
// No Supabase/Railway connection, no credentials, no DDL, no customer records.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createHash } from "node:crypto";
const manifest=JSON.parse(readFileSync(new URL("./supabase-migration-custody-20261116.json",import.meta.url),"utf8"));
assert.equal(manifest.schema_version,1);
assert.equal(manifest.supabase_parent_project_ref,"bkutbglzivfdnoerigyb");
assert.equal(manifest.migrations.length,7);
assert.equal(manifest.expected_managed_replay_status.startsWith("UNRECONCILED"),true);
const expectedVersionOrder=["20261112042025","20261112054014","20261112070921","20261112071252",
 "20261112072944","20261112074018","20261112100346"];
const versions=manifest.migrations.map(x=>x.version);
assert.deepEqual(versions,expectedVersionOrder,"Do not silently add, remove or renumber historical production migrations");
let patched=0;
for(const item of manifest.migrations){
 assert.match(item.historic_md5,/^[a-f0-9]{32}$/);
 assert.match(item.source,/^scripts\/(?:task21|task22)\/[\w.-]+\.sql$/);
 // Read source relative to repository root, NEVER fetch a remote migration.
 const sql=readFileSync(new URL("../../"+item.source,import.meta.url),"utf8");
 const currentMd5=createHash("md5").update(sql,"utf8").digest("hex");
 if(item.source_relation==="unchanged") {
  assert.equal(currentMd5,item.historic_md5,
   "Previously identical Supabase migration SQL drifted unexpectedly: "+item.version);
 }else{
  assert.equal(item.source_relation,"changed_review_only");
  assert.notEqual(currentMd5,item.historic_md5,
   "Reviewed source repair missing; historical SQL has NOT been updated remotely: "+item.version);
  patched++;
 }
 assert.doesNotMatch(sql,/\b(?:INSERT\s+INTO|UPDATE|DELETE\s+FROM)\s+(?:auth\.users|supabase_migrations\.schema_migrations)\b/i,
   "Historical source cannot impersonate staff or rewrite migration history: "+item.version);
}
assert.equal(patched,4);
const source=path=>readFileSync(new URL("../../"+path,import.meta.url),"utf8");
const privacy=source("scripts/task21/production-privacy-form-private-queue-20261112.sql");
const auth=source("scripts/task21/production-locked-single-operator-20261112.sql");
const outbox=source("scripts/task21/production-real-incident-alert-outbox-20261112.sql");
const retention=source("scripts/task21/production-mailcase-retention-cascade-fix-20261112.sql");
const crm=source("scripts/task22/production-crm-optout-local-transport-hold-20261112.sql");
assert.match(privacy,/manual_operator_grants WHERE enabled=true\)>1/);
assert.match(auth,/JOIN auth\.users u ON u\.id=g\.user_id/);
assert.match(auth,/g\.enabled=true/);
assert.match(outbox,/public\.alpha_support_manual_operator_authorized\(g\.user_id\)/);
assert.match(outbox,/claim_next_production_incident\(\)[\s\S]*?WHERE q\.state='QUEUED'/);
assert.match(retention,/PERFORM cron\.schedule\(/);
assert.match(retention,/EXISTS \(SELECT 1 FROM alpha_support_private\.manual_mail_cases\)/);
assert.match(crm,/EXISTS\(SELECT 1 FROM public\.alpha_ghl_delivery\)/);
assert.match(crm,/WHERE state='DELIVERED'\)<>5/);
assert.match(crm,/r\.marketing_opt_in IS TRUE/);
console.log("TASK3B_MANAGED_MIGRATION_CUSTODY_PASS "+manifest.migrations.length+" historical versions, "+patched+" source-only reviewed revisions");
console.log("TASK3B_SUPABASE_REMOTE_REPLAY_UNRECONCILED: do not claim managed branch migrations, hosted API/EF acceptance, or production release");
console.log("TASK3B_RAILWAY_POSTGRES_TARGET_SEPARATE: app's actual protected connection URL not inspected; no test writes");
