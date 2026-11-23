// Offline synthetic regression for the Windows PowerShell 5.1 CRLF native pipeline.
// No WSL, GPG, protected archive, secrets, PostgreSQL, network, or disk writes.
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
const source=readFileSync(new URL('./task3b-wsl-native-pipeline-synthetic.ps1', import.meta.url),'utf8');
const match=source.match(/\$helper=@'\r?\n([\s\S]*?)\r?\n'@/);
assert.ok(match,'original synthetic FD3 helper must remain present');
const script64=Buffer.from(match[1].replace(/\r\n/g,'\n'),'utf8').toString('base64');
const cmd='exec 3<&0; set -o pipefail; printf %s '+script64+' | base64 -d | bash -s -- 3<&3';
function probe(input) {
  return spawnSync('bash',['-lc',cmd],{input,encoding:'utf8',timeout:10000,maxBuffer:2048});
}
for (const lineEnding of ['\n','\r\n']) {
  const r=probe('ALPHA_STDIN_PROBE'+lineEnding);
  assert.equal(r.status,0,'FD3 must accept synthetic probe with '+JSON.stringify(lineEnding)+': '+r.stderr);
  assert.equal(r.stdout.trim(),'TASK3B_SYNTHETIC_FD3=PASS');
}
for (const refused of ['ALPHA_WRONG_PROBE\r\n','ALPHA_STDIN_PROBE\r\r\n']) {
  const r=probe(refused);
  assert.notEqual(r.status,0,'invalid input must stay refused');
  assert.equal(r.stdout.trim(),'TASK3B_SYNTHETIC_FD3=REFUSED');
}
console.log('TASK3B_CRLF_SYNTHETIC_REGRESSION=PASS');
