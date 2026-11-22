// Offline synthetic-only Railway stdout framing acceptance for Task3B.
// Never executes Railway, SQL, GPG, a real backup, or customer-data queries.
import assert from 'node:assert/strict';
import { readFileSync, mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { join } from 'node:path';
import { tmpdir } from 'node:os';

const capture=readFileSync('scripts/task3b-bliss-pg18-capture.ps1','utf8');
const start=capture.indexOf('$lines = @($raw | ForEach-Object');
const end=capture.indexOf('\n$publicTables = [int]$m.Groups[1].Value',start);
assert.ok(start>0&&end>start&&end-start<3600,'bounded receipt parsing block');
const parser=capture.slice(start,end);
assert.match(capture,/-- bash -c \$runner 2>\$null/);
assert.match(parser,/TASK3B_CAPTURE_RECEIPT_STAGE/);
assert.match(parser,/^(\$m = \[regex\]::Match\(\$joined, '\\A)/m);
assert.doesNotMatch(capture,/Write-(?:Output|Host)\s+\$(?:raw|lines|joined|b64|plain)/);
const data=Buffer.alloc(180,88); data.write('PGDMP',0,'ascii');
const b64=data.toString('base64');
const envelope=[
 'TASK3B_SOURCE=bliss|18|99',
 'TASK3B_DUMP_BEGIN', b64, 'TASK3B_DUMP_END'
];
const notices=[
 'Using SSH key from file C:\\Users\\Synthetic\\.ssh\\id_ed25519.pub: synthetic',
 'warning: Config as Code (railway.json / railway.toml) is deprecated. Prefer Infrastructure as Code (.railway/railway.ts).',
 '- Run railway config migrate or see https://docs.railway.com/infrastructure-as-code#migrating-from-config-as-code',
 'Existing files keep working until 2026-12-01.',
 'Connection to ssh.railway.com closed.'
];
const dir=mkdtempSync(join(tmpdir(),'task3b-railway-notices-'));
function run(lines){
 const privateLines=lines.map(x=>"'"+x.replaceAll("'","''")+"'").join(",\r\n");
 const source=[
  "$ErrorActionPreference = 'Stop'",
  '$raw = @('+privateLines+')',
  parser,
  "if (!$m.Success) { exit 4 }",
  "if ($m.Groups[1].Value -cne '99') { exit 5 }",
  "if ($m.Groups[2].Value -cne '"+b64+"') { exit 6 }",
  "Write-Output 'TASK3B_FIXTURE_RECEIPT=PASS'"
 ].join('\r\n')+'\r\n';
 const file=join(dir,'check.ps1');
 writeFileSync(file,source);
 const result=spawnSync('pwsh',['-NoProfile','-NonInteractive','-File',file],{encoding:'utf8',maxBuffer:8192,timeout:10000});
 assert.ok(!result.error,'pwsh fixture must start');
 assert.doesNotMatch(result.stdout,b64);
 return result;
}
try{
 let r=run(envelope);
 assert.equal(r.status,0,r.stderr);
 assert.equal(r.stdout.trim(),'TASK3B_FIXTURE_RECEIPT=PASS');
 r=run([notices[0],notices[1],notices[2],notices[3],...envelope,notices[4]]);
 assert.equal(r.status,0,r.stderr);
 assert.equal(r.stdout.trim(),'TASK3B_FIXTURE_RECEIPT=PASS');
 r=run([...envelope,'UNEXPECTED_REMOTE_TEXT']);
 assert.notEqual(r.status,0);
 assert.match(r.stdout,/TASK3B_CAPTURE_RECEIPT_STAGE=EXTRA_OUTPUT_OR_DUMP_FRAMING/);
 r=run([notices[0],...envelope,...envelope]);
 assert.notEqual(r.status,0,'duplicate dump envelope must fail');
 r=run([envelope[0],envelope[1],b64.slice(0,-5),envelope[3]]);
 assert.notEqual(r.status,0,'incomplete encoding must fail');
 r=run([envelope[0],envelope[1],b64+'?',envelope[3]]);
 assert.notEqual(r.status,0,'unexpected byte in base64 must fail');
 console.log('TASK3B_RAILWAY_BACKUP_NOTICE_FRAMING=PASS_SYNTHETIC_ONLY');
}finally{rmSync(dir,{recursive:true,force:true});}
