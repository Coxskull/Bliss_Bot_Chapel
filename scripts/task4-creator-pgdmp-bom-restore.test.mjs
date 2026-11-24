// Synthetic-only regression for temporary PG18 restore's exact BOM normalizer.
// Never starts GPG, pg_restore, a database, or contacts an external service.
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
const sh=readFileSync(new URL('./task4-creator-wsl-pg18-restore.sh',import.meta.url),'utf8');
const syntax=spawnSync('bash',['-n'],{input:sh,encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(syntax.status,0,'Bash parser must accept pipeline syntax: '+syntax.stderr);
const m=sh.match(/python3 -c '\n([\s\S]*?)\n' 2>\/dev\/null \|/);
assert.ok(m,'no-filesystem Python prefix filter must remain embedded in the pipe');
const py=m[1];
function run(buf){return spawnSync('python3',['-c',py],{input:buf,timeout:10000,maxBuffer:1048576});}
const valid=Buffer.concat([Buffer.from('PGDMP','ascii'),Buffer.alloc(65537,0x8f)]);
const bare=run(valid);
assert.equal(bare.status,0,'Unprefixed PGDMP must be preserved');
assert.deepEqual(bare.stdout,valid);
const prefixed=run(Buffer.concat([Buffer.from([0xef,0xbb,0xbf]),valid]));
assert.equal(prefixed.status,0,'Exact UTF-8 BOM PGDMP must be normalized');
assert.deepEqual(prefixed.stdout,valid);
for(const bad of [
  Buffer.from('PGDM','ascii'),
  Buffer.from('WRONGHEADER','ascii'),
  Buffer.from([0xef,0xbb,0xbf,80,71,68,77,88,0]),
  Buffer.from([0,0,0,80,71,68,77,80,0]),
]){
  const result=run(bad);
  assert.notEqual(result.status,0,'Missing or untrusted PGDMP signature must refuse');
  assert.equal(result.stdout.length,0,'Rejected input must never reach pg_restore');
}
for(const guard of [
  'TASK4_WSL_PG18_RESTORE=PASS',
  'TASK4_WSL_PG18_EXIT_CLEANUP=ABSENT_VERIFIED',
  'PREVIOUS_TEMP_DB_PRESENT',
  'trap cleanup EXIT',
  'fail PREFIX_NORMALIZATION_REFUSED',
  '[[ "${#pipe_status[@]}" -eq 3 ]]',
  '"$archive" </dev/null 2>/dev/null |',
  'runuser -u postgres -- /usr/lib/postgresql/18/bin/pg_restore --exit-on-error --single-transaction',
]) assert.ok(sh.includes(guard),'Missing isolated-restore safety guard: '+guard);
assert.doesNotMatch(sh,/--ignore-mdc-error|--skip-signature|--export-secret|--import|--no-verify/i);
console.log('TASK4_PGDMP_EXACT_BOM_SYNTHETIC=PASS');
