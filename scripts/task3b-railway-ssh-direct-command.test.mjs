// Regression for Task3B Railway CLI's DIRECT remote command behavior.
// Uses synthetic PGDMP fixture and LOCAL bash only; no Railway, customer data,
// credentials, actual database, Docker or paid services.
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtempSync,writeFileSync,chmodSync,rmSync,readFileSync} from 'node:fs';
import {join} from 'node:path';
import {tmpdir} from 'node:os';

const ps=readFileSync('scripts/task3b-bliss-pg18-capture.ps1','utf8');
const match=ps.match(/\$remote = @'\r?\n([\s\S]*?)\r?\n'@/);
assert.ok(match,'fixed owner-reviewed shell source must exist');
assert.match(ps,/ToBase64String\(\[Text\.Encoding\]::UTF8\.GetBytes\(\$remote\)\)/);
assert.match(ps,/\$runner = 'set -o pipefail; printf %s ' \+ \$remoteBase64 \+ ' \| base64 -d \| bash'/);
assert.match(ps,/-- bash -c \$runner 2>&1/);
assert.doesNotMatch(ps,/\$remote \|\s*&\s*\$railway\.Source ssh/,
  'PowerShell cannot pipe command input over this Railway CLI version');
assert.match(ps,/--format=custom --no-owner --no-acl \| base64 -w0/);
assert.match(ps,/Get-FileHash -LiteralPath \$archivePath -Algorithm SHA256/);
assert.doesNotMatch(ps, /Write-(Output|Host)\s+\$raw/,
  'never show confidential encoded backup in console');
assert.match(ps,/^\$dataLines = @\(\$lines \| Where-Object/m);
const temp=mkdtempSync(join(tmpdir(),'alpha-task3b-direct-'));
const shell=(name,script)=>{
  const path=join(temp,name);writeFileSync(path,script);chmodSync(path,0o700);
};
shell('psql', [
  '#!/bin/sh',
  'printf "%s\\n" "$FAKE_DBIDENTITY"',
  ''
].join('\n'));
shell('pg_dump', [
  '#!/bin/sh',
  'printf PGDMP',
  'head -c 150 /dev/zero | tr "\\000" x',
  ''
].join('\n'));
const script=match[1];
const b64=Buffer.from(script,'utf8').toString('base64');
const command='set -o pipefail; printf %s '+b64+' | base64 -d | bash';
const baseEnv={
  ...process.env,
  PATH:temp+':'+process.env.PATH,
  FAKE_DBIDENTITY:'bliss|18|2',
  RAILWAY_PROJECT_ID:'fb33c1b7-03fb-42c8-89b1-081951a738fd',
  RAILWAY_ENVIRONMENT_ID:'b67a9836-36bf-4083-93f7-00c867b13f39',
  RAILWAY_SERVICE_ID:'6aa78407-1632-48c9-8ea4-0289a7d36a5b',
  PGUSER:'fixture',PGPASSWORD:'fixture',PGHOST:'127.0.0.1',PGPORT:'5432'
};
function run(env=baseEnv){
  return spawnSync('bash',['-c',command],{env,encoding:'utf8',timeout:10000,maxBuffer:100000});
}
try {
  let r=run();
  assert.equal(r.status,0,'expected direct synthetic backup success: '+r.stderr);
  const expected=/^TASK3B_SOURCE=bliss\|18\|2\nTASK3B_DUMP_BEGIN\n([A-Za-z0-9+/=]+)\nTASK3B_DUMP_END\n$/;
  const m=expected.exec(r.stdout);
  assert.ok(m,'precise framing and base64 encoded synthetic fixture expected');
  const data=Buffer.from(m[1],'base64');
  assert.equal(data.length,155);
  assert.equal(data.subarray(0,5).toString(),'PGDMP');

  r=run({...baseEnv,RAILWAY_PROJECT_ID:'another-project'});
  assert.notEqual(r.status,0,'unapproved project refused before dumping');
  assert.doesNotMatch(r.stdout,/TASK3B_DUMP_BEGIN/);

  r=run({...baseEnv,FAKE_DBIDENTITY:'bliss|17|2'});
  assert.notEqual(r.status,0,'PostgreSQL 17 must not be accepted');
  assert.doesNotMatch(r.stdout,/TASK3B_DUMP_BEGIN/);

  r=run({...baseEnv,RAILWAY_SERVICE_ID:'wrong-service'});
  assert.notEqual(r.status,0,'wrong database service refused');
  assert.doesNotMatch(r.stdout,/TASK3B_DUMP_BEGIN/);
  console.log('TASK3B_DIRECT_RAILWAY_SSH_SHELL_COMMAND_AND_STRICT_PGDMP_RECEIPT_PASS (offline / $0)');
} finally {
  rmSync(temp,{recursive:true,force:true});
}
