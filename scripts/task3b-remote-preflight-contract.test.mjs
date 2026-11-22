// Offline only: synthetic Bash fixtures and metadata, no Railway connection.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, chmodSync, rmSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';

const preflight=readFileSync('scripts/task3b-bliss-pg18-remote-preflight.ps1','utf8');
const capture=readFileSync('scripts/task3b-bliss-pg18-capture.ps1','utf8');
const extract=s=>{
  const m=s.match(/\$remote = @'\r?\n([\s\S]*?)\r?\n'@/);
  assert.ok(m, 'reviewed remote bash source required');
  return m[1];
};
const script=extract(preflight),original=extract(capture);
assert.ok(preflight.includes("Write-Output ('TASK3B_REMOTE_PREFLIGHT=' + $stage)"));
for (const [code,stage] of [
  [31,'SCOPE_MISMATCH'],[32,'DATABASE_TOOL_MISSING'],
  [33,'DATABASE_ENV_MISSING'],[34,'DATABASE_CONNECTION_OR_QUERY_REFUSED'],
  [35,'DATABASE_IDENTITY_MISMATCH'],[36,'PG_DUMP_SCHEMA_PREFLIGHT_REFUSED']
]) {
  assert.ok(script.includes(stage+') stage_exit='+code), 'reviewed remote exit code '+code);
  assert.ok(preflight.includes(code+" = '"+stage+"'"), 'PowerShell maps fixed exit '+code);
}
assert.ok(preflight.includes('$exitStage.ContainsKey([int]$exitCode)'),
  'fallback from missing stdout to trusted exit code');

assert.ok(preflight.includes("-s 'Postgres' -e production -- bash -c $runner 2>&1"));
assert.ok(preflight.includes('REMOTE_SHELL_OR_OUTPUT_UNVERIFIED'));
assert.doesNotMatch(preflight,/Write-(?:Output|Host)\s+\$raw|Start-Transcript|Get-ChildItem Env:/);
assert.doesNotMatch(script,/--data-only|--format=custom/);
assert.match(script,/pg_dump --dbname=bliss --schema-only --no-owner --no-acl --file=\/dev\/null/);
for(const name of ['SCOPE_MISMATCH','DATABASE_TOOL_MISSING','DATABASE_ENV_MISSING',
  'DATABASE_CONNECTION_OR_QUERY_REFUSED','DATABASE_IDENTITY_MISMATCH','PG_DUMP_EXPORT_REFUSED'])
  assert.ok(original.includes("refuse '"+name+"'"), 'missing capture class '+name);
assert.match(capture,/TASK3B_CAPTURE_REMOTE_STAGE=TRANSPORT_OR_UNCLASSIFIED/);
assert.match(capture,/--format=custom --no-owner --no-acl 2>\/dev\/null \| base64 -w0/);
const folder=mkdtempSync(join(tmpdir(),'task3b-preflight-'));
function fake(name,body){
  const target=join(folder,name);
  writeFileSync(target,'#!/bin/sh\n'+body+'\n');
  chmodSync(target,0o700);
}
fake('psql',"printf '%s\\n' \"$FAKE_DBIDENTITY\"");
fake('pg_dump',"case \" $* \" in *' --schema-only '*' --file=/dev/null '*) exit 0;; *) exit 13;; esac");
function run(changes={}){
  const env={
    ...process.env, PATH:folder+':'+process.env.PATH,
    RAILWAY_PROJECT_ID:'fb33c1b7-03fb-42c8-89b1-081951a738fd',
    RAILWAY_ENVIRONMENT_ID:'b67a9836-36bf-4083-93f7-00c867b13f39',
    RAILWAY_SERVICE_ID:'6aa78407-1632-48c9-8ea4-0289a7d36a5b',
    PGHOST:'127.0.0.1',PGPORT:'5432',PGUSER:'synthetic',
    PGPASSWORD:'synthetic-only',FAKE_DBIDENTITY:'bliss|18|99',
    ...changes
  };
  const r=spawnSync('bash',['-c',script],{env,encoding:'utf8',timeout:10000,maxBuffer:4096});
  assert.equal(r.error,undefined);
  assert.doesNotMatch(r.stdout,/synthetic-only|127\.0\.0\.1|password|PGPASSWORD|PGUSER/);
  return r;
}
try{
  let r=run();
  assert.equal(r.status,0,r.stderr);
  assert.equal(r.stdout.trim(),'TASK3B_REMOTE_PREFLIGHT=PASS_SCOPE_DATABASE_AND_DUMP_TOOLS');
  r=run({RAILWAY_SERVICE_ID:'not-the-approved-service'});
  assert.equal(r.status,31);
  assert.equal(r.stdout.trim(),'TASK3B_REMOTE_PREFLIGHT=SCOPE_MISMATCH');
  r=run({PGPASSWORD:''});
  assert.equal(r.status,33);
  assert.equal(r.stdout.trim(),'TASK3B_REMOTE_PREFLIGHT=DATABASE_ENV_MISSING');
  r=run({FAKE_DBIDENTITY:'bliss|17|99'});
  assert.equal(r.status,35);
  assert.equal(r.stdout.trim(),'TASK3B_REMOTE_PREFLIGHT=DATABASE_IDENTITY_MISMATCH');
  fake('psql','exit 9');
  r=run();
  assert.equal(r.status,34);
  assert.equal(r.stdout.trim(),'TASK3B_REMOTE_PREFLIGHT=DATABASE_CONNECTION_OR_QUERY_REFUSED');
  fake('psql',"printf '%s\\n' \"$FAKE_DBIDENTITY\"");
  fake('pg_dump','exit 13');
  r=run();
  assert.equal(r.status,36);
  assert.equal(r.stdout.trim(),'TASK3B_REMOTE_PREFLIGHT=PG_DUMP_SCHEMA_PREFLIGHT_REFUSED');
  console.log('TASK3B_REMOTE_EXIT2_CLASSIFICATION=PASS_SYNTHETIC_ONLY');
}finally{rmSync(folder,{recursive:true,force:true});}
