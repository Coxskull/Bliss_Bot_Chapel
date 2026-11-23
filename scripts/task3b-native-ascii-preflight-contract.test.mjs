// Offline safety & transport regression for the Task4 native PowerShell no-DB preflight.
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const source=readFileSync(new URL('./task3b-wsl-ascii-armor-native-preflight.ps1',import.meta.url),'utf8');
const receiver=readFileSync(new URL('./task3b-wsl-ascii-armor-receiver.py',import.meta.url),'utf8');
for (const guard of [
  '$OutputEncoding=$utf8','[Console]::InputEncoding=$utf8',
  'Get-FileHash -LiteralPath $archive','gpgRecipientFingerprint',
  'StandardOutput.BaseStream.Read','Write-Output ([Convert]::ToBase64String(',
  'base64 -d | python3 - 3<&3','--decrypt --output -',
  'PASS_BYTE_IDENTICAL','SHA256_MISMATCH','TASK4_CREATOR_RESTORE=HELD',
  'PASS_ARCHIVE_FORMAT','SIZE_LIMIT_REFUSED','[int64]$script:maxBytes=64*1024*1024',
  'PASS_UTF8_BOM_STRIPPED_IN_STREAM','PGDMP_AT_ZERO'
]) assert.ok(source.includes(guard),'missing preflight safeguard '+guard);
assert.doesNotMatch(source,/StandardInput\.BaseStream\.Write|\$wproc|--import|--export-secret|createdb|dropdb|\bpsql\b|pg_restore|\brailway\.cmd\b|Start-Transcript|Set-Content|Out-File|WriteAllBytes|Invoke-WebRequest|Invoke-RestMethod/i);
assert.doesNotMatch(source,/Write-(?:Output|Host)\s+\$(?:archive|buffer|header|m|gpg|helper|output|raw)/i);
assert.ok(source.includes('if ($wslExit -eq 0 -and $pgStatus -ceq'));
// Emulate the new small-frame Base64-over-CRLF transport under Bash/Python.
// This checks LF/CRLF handling and SHA-256 with only synthetic data.
const original=Buffer.concat([Buffer.from('PGDMP','ascii'),Buffer.alloc(8192,0x8f)]);
let carry=Buffer.alloc(0);const frames=[];
for(let i=0;i<original.length;i+=97){
  const next=Buffer.concat([carry,original.subarray(i,i+97)]);
  const bulk=next.length-next.length%3;
  if(bulk) frames.push(next.subarray(0,bulk).toString('base64'));
  carry=next.subarray(bulk);
}
if(carry.length) frames.push(carry.toString('base64'));
const helper64=Buffer.from(receiver,'utf8').toString('base64');
const cmd='exec 3<&0; set -o pipefail; printf %s '+helper64+
  ' | base64 -d | python3 - --probe-only 3<&3';
const result=spawnSync('bash',['-lc',cmd],{
  input:frames.join('\r\n')+'\r\n',encoding:'utf8',timeout:10000,maxBuffer:4096
});
assert.equal(result.status,0,'synthetic no-DB native ASCII route: '+result.stderr);
assert.match(result.stdout,/TASK3B_ARMOR_WSL_PG18=PROBE_ONLY/);
assert.ok(result.stdout.includes('TASK3B_ARMOR_WSL_SHA256='+createHash('sha256').update(original).digest('hex')));
console.log('TASK3B_NATIVE_ASCII_OFFLINE=PASS');
