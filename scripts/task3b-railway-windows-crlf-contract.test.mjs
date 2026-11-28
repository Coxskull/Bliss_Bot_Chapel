// Offline regression for Windows PowerShell CRLF -> Linux Bash source encoding.
// No Railway login, credentials, network, PG commands or customer records.
import assert from 'node:assert/strict';
import { readFileSync, mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

const files=[
  'scripts/task3b-bliss-pg18-capture.ps1',
  'scripts/task3b-bliss-pg18-remote-preflight.ps1'
];
for(const path of files){
  const source=readFileSync(path,'utf8');
  const normalized='$remote = $remote.Replace("`r`n", "`n").Replace("`r", "")';
  const position=source.indexOf(normalized);
  const encoded=source.indexOf('[Text.Encoding]::UTF8.GetBytes($remote)');
  assert.ok(position!==-1,'LF source normalization required in '+path);
  assert.ok(encoded>position,'normalize remote PowerShell here-string before base64 in '+path);
  assert.ok(source.includes("$remote = @'\nset -euo pipefail"),
    'keep fixed reviewed Bash source in '+path);
}
const synthetic="set -euo pipefail\nprintf 'TASK3B_LF_CHECK=PASS\\n'\n";
const crlf=synthetic.replaceAll('\n','\r\n');
const bad=spawnSync('bash',{input:crlf,encoding:'utf8',maxBuffer:4096});
assert.equal(bad.status,2,'Linux Bash must reject Windows CRLF set -o pipefail');
assert.doesNotMatch(bad.stdout,/TASK3B_LF_CHECK=PASS/);
const safe=spawnSync('bash',{input:synthetic,encoding:'utf8',maxBuffer:4096});
assert.equal(safe.status,0,'LF-normalized synthetic Bash should execute');
assert.equal(safe.stdout.trim(),'TASK3B_LF_CHECK=PASS');

// Validate EXACT PowerShell normalization syntax, not an unrelated JS fix.
// Offline only, using the same installed pwsh runtime as CI.
const dir=mkdtempSync(join(tmpdir(),'task3b-crlf-'));
try {
  const file=join(dir,'verify.ps1');
  const ps=[
    '$remote = "set -euo pipefail`r`nprintf '+"'TASK3B_LF_CHECK=PASS\\n'"+'`r`n"',
    '$remote = $remote.Replace("`r`n", "`n").Replace("`r", "")',
    '[Console]::Write([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($remote)))'
  ].join('\r\n')+'\r\n';
  writeFileSync(file,ps);
  const result=spawnSync('pwsh',['-NoProfile','-NonInteractive','-File',file],
    {encoding:'utf8',maxBuffer:4096,timeout:10000});
  assert.equal(result.status,0,'PowerShell normalization must succeed');
  const bytes=Buffer.from(result.stdout.trim(),'base64');
  assert.ok(bytes.length>0);
  assert.equal(bytes.includes(13),false,'no CR bytes may reach Linux Bash');
  const execution=spawnSync('bash',{input:bytes,encoding:'utf8',maxBuffer:4096});
  assert.equal(execution.status,0,'PowerShell-produced LF Bash must execute');
  assert.equal(execution.stdout.trim(),'TASK3B_LF_CHECK=PASS');
  console.log('TASK3B_WINDOWS_CRLF_REMOTE_BASH=PASS_OFFLINE_ONLY');
}finally{
  rmSync(dir,{recursive:true,force:true});
}
