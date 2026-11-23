// Offline safety regression. Never run Railway, GPG decryption or real WSL.
import assert from 'node:assert/strict';
import {readFileSync,mkdtempSync,writeFileSync,rmSync} from 'node:fs';
import {join} from 'node:path';
import {tmpdir} from 'node:os';
import {spawnSync} from 'node:child_process';
const path='scripts/task3b-wsl-local-verify-current.ps1';
const source=readFileSync(path,'utf8');
const keyCheck=readFileSync('scripts/task3b-gpg-key-presence-preflight.ps1','utf8');
for(const guard of ['TASK3B_GPG_RECOVERY_PREFLIGHT=', 'gpgRecipientFingerprint',
  '--list-secret-keys', 'Get-FileHash -LiteralPath $archive',
  'MATCHING_PRIVATE_KEY_NOT_VISIBLE_TO_GPG',
  'MATCHING_PRIVATE_KEY_LISTED_DECRYPTION_UNVERIFIED',
  'TASK4_CREATOR_RESTORE=HELD']) {
  assert.ok(keyCheck.includes(guard),'missing local-only GPG preflight guard '+guard);
}
assert.doesNotMatch(keyCheck,/--decrypt|--import|--export-secret|Invoke-WebRequest|Invoke-RestMethod|pg_restore|railway\.cmd|CREATE\s+DATABASE|Start-Transcript/i);
assert.doesNotMatch(keyCheck,/Write-(?:Output|Host)\s+\$lines|Write-(?:Output|Host)\s+\$fingerprint/);

// Owner-only WSL/Gpg4win interop probe: no database, no plaintext file.
const interopPS=readFileSync('scripts/task3b-wsl-gpg-stream-preflight.ps1','utf8');
const interopSH=readFileSync('scripts/task3b-wsl-gpg-stream-preflight.sh','utf8');
for(const guard of ['Get-FileHash -LiteralPath $archive','gpgRecipientFingerprint',
  'sourceProjectId','sourceEnvironmentId','sourceServiceId',
  'TASK3B_WSL_GPG_STREAM=PASS','TASK4_CREATOR_RESTORE=HELD',
  'set -o pipefail; printf %s ','base64 -d | bash -s -- ']) {
  assert.ok(interopPS.includes(guard),'missing WSL-GPG guard '+guard);
}
assert.ok(interopSH.includes('--decrypt --output - "$archive" </dev/null >/dev/null 2>/dev/null'));
assert.doesNotMatch(interopPS,/pg_restore|createdb|dropdb|railway\.cmd|Invoke-WebRequest|Start-Transcript|--import|--export-secret/i);
assert.doesNotMatch(interopSH,/pg_restore|createdb|dropdb|psql|railway|--import|--export-secret/i);
assert.doesNotMatch(interopPS,/Write-(?:Output|Host)\s+\$(?:raw|archive|gpg|source)/);
// The current task uses one bounded GPG->PG18 archive TOC check, no DB writes.
const formatPS=readFileSync('scripts/task3b-wsl-pg18-format-preflight.ps1','utf8');
const formatSH=readFileSync('scripts/task3b-wsl-pg18-format-preflight.sh','utf8');
for(const guard of [
  'Get-FileHash -LiteralPath $archive','sourceProjectId',
  'sourceEnvironmentId','sourceServiceId','gpgRecipientFingerprint',
  'TASK3B_WSL_PG_ARCHIVE=PASS_AUTHENTICATED_FORMAT',
  'TASK4_CREATOR_RESTORE=HELD','base64 -d | bash -s -- '
]) assert.ok(formatPS.includes(guard),'missing no-DB PG archive format guard '+guard);
for(const guard of [
  '/usr/lib/postgresql/18/bin/pg_restore --list',
  'cat >/dev/null 2>/dev/null','BOTH_PROCESSES_REFUSED',
  'PG_FORMAT_ONLY_REFUSED','GPG_ONLY_REFUSED',
  'pipeline=( "${PIPESTATUS[@]}" )','--decrypt --output - "$archive" </dev/null'
]) assert.ok(formatSH.includes(guard),'missing archive pipeline guard '+guard);
assert.doesNotMatch(formatPS,/createdb|dropdb|psql|railway\.cmd|Invoke-WebRequest|Start-Transcript|--import|--export-secret/i);
assert.doesNotMatch(formatSH,/createdb|dropdb|psql|--dbname|--exit-on-error|--single-transaction|--import|--export-secret/i);
assert.doesNotMatch(formatPS,/Write-(?:Host|Output)\s+\$(?:raw|archive|gpg|source)/);
assert.match(formatPS,/\^TASK3B_WSL_PG_ARCHIVE=\(\[A-Z_\]\+\)\$/);
const formatEarly=spawnSync('bash',['scripts/task3b-wsl-pg18-format-preflight.sh'],{encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(formatEarly.status,2);
assert.match(formatEarly.stdout,/TASK3B_WSL_PG_ARCHIVE=PREFLIGHT_REFUSED/);
const formatSyntax=spawnSync('pwsh',['-NoProfile','-NonInteractive','-Command',
  "$tokens=$null;$errors=$null;[System.Management.Automation.Language.Parser]::ParseFile('scripts/task3b-wsl-pg18-format-preflight.ps1',[ref]$tokens,[ref]$errors)>$null;if($errors.Count -gt 0){exit 71}"
],{encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(formatSyntax.status,0,'PowerShell parse failure: '+formatSyntax.stderr);
const drainSynthetic=spawnSync('bash',['-c',
  'set -euo pipefail; set +e; head -c 131072 /dev/zero | ( head -c 4 >/dev/null; cat >/dev/null ); pipe=( "${PIPESTATUS[@]}" ); [[ ${pipe[0]} == 0 && ${pipe[1]} == 0 ]]'
],{encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(drainSynthetic.status,0,'synthetic early reader must drain producer');

// Nov22 GPG output accepted but missing PGDMP; only fixed in-memory envelope classifications.
const envelope=readFileSync('scripts/task3b-native-envelope-preflight.ps1','utf8');
for (const guard of [
  'Get-FileHash -LiteralPath $archive','sourceProjectId','sourceEnvironmentId',
  'sourceServiceId','gpgRecipientFingerprint','StandardOutput.BaseStream.Read',
  'PGDMP_AT_START','PGDMP_AFTER_SHORT_PREFIX','PGDMP_AFTER_LONG_PREFIX',
  'GZIP_NOT_PGDMP','ZIP_NOT_PGDMP','UTF8_BOM_NO_PGDMP',
  'UNKNOWN_NOT_PGDMP','TASK4_CREATOR_RESTORE=HELD','SIZE_LIMIT_REFUSED'
]) assert.ok(envelope.includes(guard),'missing envelope guard '+guard);
assert.doesNotMatch(envelope,/pg_restore|createdb|dropdb|psql|railway\.cmd|Invoke-WebRequest|Start-Transcript|--import|--export-secret|Set-Content|Out-File|WriteAllBytes/i);
assert.doesNotMatch(envelope,/Write-(?:Host|Output)\s+\$(?:first|buf|archive|gpg|m|plain|total)/i);

// Classify PGDMP prefix using only approved markers and a numeric byte offset.
// Do not normalize/strip prefix until the owner provides the byte category.
for (const marker of ['TASK3B_NATIVE_PREFIX_KIND=', 'TASK3B_NATIVE_PGDMP_OFFSET=',
  'UTF8_BOM','CRLF','ZERO_PADDING','ASCII_TEXT_UNTRUSTED',
  'OTHER_BINARY_UNTRUSTED', 'ClassifyPrefix $first $offset']) {
  assert.ok(envelope.includes(marker),'missing prefix-classifier safety marker '+marker);
}
const classifierSource=envelope.match(/function ClassifyPrefix\([\s\S]*?\n\}/);
assert.ok(classifierSource,'reviewed PowerShell prefix classifier body');
const cases=[
  ['UTF8_BOM',[239,187,191],3],
  ['CRLF',[13,10],2],
  ['ZERO_PADDING',[0,0,0],3],
  ['ASCII_WHITESPACE',[32,9,32],3],
  ['ASCII_TEXT_UNTRUSTED',[65,66],2],
  ['OTHER_BINARY_UNTRUSTED',[0,255],2]
];
for (const [expected,bytes,length] of cases) {
  const c=classifierSource[0]+'\n'+'$b=[byte[]]@('+bytes.join(',')+');'+
    'if ((ClassifyPrefix $b '+length+') -cne "'+expected+'") { exit 84 }';
  const test=spawnSync('pwsh',['-NoProfile','-NonInteractive','-Command',c],
    {encoding:'utf8',timeout:10000,maxBuffer:4096});
  assert.equal(test.status,0,'PowerShell prefix category '+expected+': '+test.stderr);
}

const envParse=spawnSync('pwsh',['-NoProfile','-NonInteractive','-Command',
  "$t=$null;$e=$null;[System.Management.Automation.Language.Parser]::ParseFile('scripts/task3b-native-envelope-preflight.ps1',[ref]$t,[ref]$e)>$null;if($e.Count -gt 0){exit 83}"
],{encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(envParse.status,0,'envelope PowerShell parse failure: '+envParse.stderr);
// Native byte-stream bridge: bypass Windows PowerShell 5.1 text conversion.
// Keep plaintext in process pipes only; WSL pg_restore may list TOC, never DB.
const nativeBridge=readFileSync('scripts/task3b-native-binary-wsl-pg18-preflight.ps1','utf8');
for(const guard of [
  'Get-FileHash -LiteralPath $archive','sourceProjectId','sourceEnvironmentId',
  'sourceServiceId','gpgRecipientFingerprint',
  'StandardOutput.BaseStream.Read','StandardInput.BaseStream.Write',
  'exec 3<&0;', 'bash -s -- 3<&3', 'pg_restore --list <&3',
  'cat <&3 >/dev/null', 'TASK3B_NATIVE_PGDMP=',
  'TASK3B_NATIVE_WSL_PG18=', 'TASK4_CREATOR_RESTORE=HELD',
  "PGDMP", 'PASS_ARCHIVE_FORMAT',
  'WSL_HEADER_NOT_PGDMP', 'dd bs=5 count=1 iflag=fullblock <&3'
]) assert.ok(nativeBridge.includes(guard),'missing native binary guard '+guard);
assert.doesNotMatch(nativeBridge,/pg_restore --dbname|createdb|dropdb|psql|railway\.cmd|Start-Transcript|--export-secret|--import|Set-Content|Out-File|WriteAllBytes/i);
assert.doesNotMatch(nativeBridge,/Write-(?:Output|Host)\s+\$(?:magic|buffer|output|archive|gpg|wsl)/i);

// Task #4: accepted owner evidence has exactly EF BB BF + PGDMP.
// Keep ciphertext unmodified; only remove those 3 bytes in volatile stream.
for (const guard of [
  'GetPgDumpHeaderKind', 'EXACT_UTF8_BOM_PGDMP', 'PGDMP_AT_ZERO',
  'PASS_UTF8_BOM_STRIPPED_IN_STREAM',
  'StandardInput.BaseStream.Write($header,$headerStart,8-$headerStart)',
  "if ($kind -ceq 'UNRECOGNIZED')", '$headerStart=3',
]) assert.ok(nativeBridge.includes(guard),'BOM stream guard missing: '+guard);
assert.doesNotMatch(nativeBridge,/Remove-Item|WriteAllBytes|Set-Content|Out-File|--ignore-mdc-error|--skip-signature/i);
const exactHeaderBody=nativeBridge.match(/function GetPgDumpHeaderKind\([\s\S]*?\n\}/)?.[0];
assert.ok(exactHeaderBody, 'exact BOM verification function required');
const casesBOM=[
  ['PGDMP_AT_ZERO',[80,71,68,77,80,4,1,0],8],
  ['EXACT_UTF8_BOM_PGDMP',[239,187,191,80,71,68,77,80],8],
  ['UNRECOGNIZED',[239,187,191,80,71,68,88,80],8],
  ['UNRECOGNIZED',[239,187,191,80,71,68,77],7],
  ['UNRECOGNIZED',[32,32,32,80,71,68,77,80],8],
  ['UNRECOGNIZED',[239,187,191,80,71,68,77,80],4],
];
for(const [expected, bytes, count] of casesBOM){
  const textBlock=exactHeaderBody+'\n$b=[byte[]]@('+bytes.join(',')+
    ');if ((GetPgDumpHeaderKind $b '+count+') -cne "'+expected+'"){exit 84}';
  const test=spawnSync('pwsh',['-NoProfile','-NonInteractive','-Command',textBlock],
    {encoding:'utf8',timeout:10000,maxBuffer:4096});
  assert.equal(test.status,0,'BOM header contract '+expected+': '+test.stderr);
}
// Synthetic byte preservation: only 3-byte BOM is omitted, no text recoding.
const rawBOM=Buffer.concat([Buffer.from([239,187,191]),Buffer.from('PGDMP','ascii'),Buffer.alloc(65536,0x8f)]);
const afterBOM=Buffer.concat([rawBOM.subarray(3,8),rawBOM.subarray(8)]);
assert.equal(afterBOM.subarray(0,5).toString('ascii'),'PGDMP');
assert.deepEqual(afterBOM,rawBOM.subarray(3));


// Actual pg_restore --list error category is kept private in the Bash process.
// The PowerShell owner readback receives ONLY allowlisted status marker names.
const pgHelper=nativeBridge.match(/\$helper=@'\n([\s\S]*?)\n'@/)?.[1];
assert.ok(pgHelper,'embedded no-DB WSL helper is present');
assert.ok(pgHelper.includes('2>&1 1>/dev/null'),'stderr captured in process only, TOC discarded');
assert.ok(pgHelper.includes('cat <&3 >/dev/null 2>/dev/null'),'unconsumed authenticated source must drain');
assert.ok(pgHelper.includes('TASK3B_NATIVE_WSL_MARKER=%s'),'only fixed classification printed');
assert.doesNotMatch(pgHelper,/printf[^\n]*\$pg_err|echo[^\n]*\$pg_err|mktemp|pg_restore\s+(?:--dbname|--create)|createdb|dropdb|psql/i);
const pgHelperSyntax=spawnSync('bash',['-n'],{input:pgHelper,encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(pgHelperSyntax.status,0,'embedded WSL Bash parse failed: '+pgHelperSyntax.stderr);
const startClass=pgHelper.indexOf('category=PG_FORMAT_UNCLASSIFIED');
const endClass=pgHelper.indexOf("printf 'TASK3B_NATIVE_WSL_MARKER=%s");
assert.ok(startClass>=0&&endClass>startClass,'safe pg_restore classifier block');
const classBody=pgHelper.slice(startClass,endClass);
const classes=[
  ['did not find magic string','PG_FORMAT_MAGIC_OR_SHORT'],
  ['input file is too short','PG_FORMAT_MAGIC_OR_SHORT'],
  ['unsupported version (1.99) in file header','PG_FORMAT_VERSION_UNSUPPORTED'],
  ['input file appears to be a text format dump','PG_FORMAT_NONCUSTOM'],
  ['could not read from input file: end of file','PG_FORMAT_READ_OR_TRUNCATED'],
  ['out of memory','PG_FORMAT_RESOURCE_REFUSED'],
  ['permission denied','PG_FORMAT_INPUT_IO'],
  ['unsupported compression algorithm','PG_FORMAT_COMPRESSION_UNSUPPORTED'],
  ['invalid archive','PG_FORMAT_STRUCTURE_REFUSED'],
  ['unexpected-unknown-code','PG_FORMAT_UNCLASSIFIED']
];
for(const [msg,expected] of classes) {
  const body='set -eu\npg_status=1\ndrain_status=0\npg_err='+
    JSON.stringify(msg)+'\n'+classBody+
    '\n[[ "$category" == '+JSON.stringify(expected)+' ]]';
  const run=spawnSync('bash',['-c',body],{encoding:'utf8',timeout:10000,maxBuffer:4096});
  assert.equal(run.status,0,'no-DB pg_restore failure category '+expected+': '+run.stderr);
}
const drainClass=spawnSync('bash',['-c','set -eu\npg_status=0\ndrain_status=2\npg_err=""\n'+classBody+
  '\n[[ "$category" == "PG_FORMAT_DRAIN_REFUSED" ]]'],{encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(drainClass.status,0,'no-DB source-stream drain classification');
for(const name of ['PG_FORMAT_MAGIC_OR_SHORT','PG_FORMAT_VERSION_UNSUPPORTED',
'PG_FORMAT_NONCUSTOM','PG_FORMAT_READ_OR_TRUNCATED','PG_FORMAT_RESOURCE_REFUSED',
'PG_FORMAT_INPUT_IO','PG_FORMAT_COMPRESSION_UNSUPPORTED','PG_FORMAT_STRUCTURE_REFUSED',
'PG_FORMAT_UNCLASSIFIED','PG_FORMAT_DRAIN_REFUSED']) {
  assert.ok(nativeBridge.includes("'"+name+"'"),'client does not recognize allowed PG status '+name);
}

const nativeSyntax=spawnSync('pwsh',['-NoProfile','-NonInteractive','-Command',
  "$t=$null;$e=$null;[System.Management.Automation.Language.Parser]::ParseFile('scripts/task3b-native-binary-wsl-pg18-preflight.ps1',[ref]$t,[ref]$e)>$null;if($e.Count){exit 81}"
],{encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(nativeSyntax.status,0,'native binary source PowerShell parse: '+nativeSyntax.stderr);
// Ensure Bash script bytes and GPG-like binary data have separate file descriptors.
// The 0x8f test bytes deliberately cannot survive text-based transcode safely.
const fd3Helper='set -euo pipefail\nfirst="$(head -c 5 <&3)"\ncat <&3 >/dev/null\n[[ "$first" == "PGDMP" ]] && printf "BINARY_FD3=PASS\\n"\n';
const fd3B64=Buffer.from(fd3Helper,'utf8').toString('base64');
const fd3Raw=Buffer.concat([Buffer.from('PGDMP','ascii'),Buffer.alloc(196608,0x8f)]);
const fd3=spawnSync('bash',['-lc',
  'exec 3<&0; set -o pipefail; printf %s '+fd3B64+' | base64 -d | bash -s -- 3<&3'
],{input:fd3Raw,encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(fd3.status,0,'FD3 binary safety repro: '+fd3.stderr);
assert.match(fd3.stdout,/BINARY_FD3=PASS/);

// Task4 no-database PGDMP binary stream signature safety contracts.
const magicPS=readFileSync('scripts/task3b-wsl-pgdmp-header-preflight.ps1','utf8');
const magicSH=readFileSync('scripts/task3b-wsl-pgdmp-header-preflight.sh','utf8');
for(const guard of ['sourceProjectId','sourceEnvironmentId','sourceServiceId',
  'Get-FileHash -LiteralPath $archive','gpgRecipientFingerprint',
  'PASS_PGDMP_AND_AUTHENTICATION','NOT_PGDMP',
  'TASK4_CREATOR_RESTORE=HELD','base64 -d | bash -s -- ']) {
  assert.ok(magicPS.includes(guard),'missing PGDMP preflight guard '+guard);
}
for(const guard of ['head -c 5','cat >/dev/null 2>/dev/null',
  '[[ "$header" == "PGDMP" ]]','pipe=( "${PIPESTATUS[@]}" )',
  'PASS_PGDMP_AND_AUTHENTICATION','NOT_PGDMP','</dev/null']) {
  assert.ok(magicSH.includes(guard),'missing binary signature guard '+guard);
}
assert.ok(!/pg_restore|createdb|dropdb|psql|railway\.cmd|Start-Transcript|--import|--export-secret/i.test(magicPS));
assert.ok(!/pg_restore|createdb|dropdb|psql|--import|--export-secret/i.test(magicSH));
const magicParse=spawnSync('pwsh',['-NoProfile','-NonInteractive','-Command',
  "$t=$null;$e=$null;[System.Management.Automation.Language.Parser]::ParseFile('scripts/task3b-wsl-pgdmp-header-preflight.ps1',[ref]$t,[ref]$e)>$null;if($e.Count){exit 80}"
],{encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(magicParse.status,0,'PowerShell PGDMP source syntax');
const magicBash=spawnSync('bash',['-n','scripts/task3b-wsl-pgdmp-header-preflight.sh'],
  {encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(magicBash.status,0,'Bash PGDMP source syntax');
const magicEarly=spawnSync('bash',['scripts/task3b-wsl-pgdmp-header-preflight.sh'],
  {encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(magicEarly.status,2);
assert.match(magicEarly.stdout,/TASK3B_WSL_PGDMP=REFUSED_OR_INTEROP_UNVERIFIED/);
const interopEarly=spawnSync('bash',['scripts/task3b-wsl-gpg-stream-preflight.sh'],{encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(interopEarly.status,2);
assert.match(interopEarly.stdout,/TASK3B_WSL_GPG_STREAM=REFUSED_OR_INTEROP_UNVERIFIED/);


for(const guard of [
  'NOT_VERIFIED','offhostSha256Verified','isolatedRestoreVerified',
  'sourcePostgresMajor','sourceProjectId','sourceEnvironmentId',
  'sourceServiceId','sourcePublicTableCount','archiveSha256',
  '2026-11-21T07:07:25Z','Get-FileHash -LiteralPath $archive',
  'task4-creator-wsl-pg18-restore.sh','TASK4_WSL_PG18_RESTORE=PASS',
  'PRODUCTION_DB_UNCHANGED','localOnly=$true',
  'offhostVerifiedByThisScript=$false','productionMigrationAuthorized=$false',
  'TASK4_CREATOR_SCHEMA=HELD',
  '.Replace("`r`n", "`n").Replace("`r", "")'
]) assert.ok(source.includes(guard),'missing guard '+guard);
assert.doesNotMatch(source,/&\s*\$railway|&\s*\$docker|cryptsetup|mkfs|migrate\s+apply|CREATE\s+ROLE/i);
assert.doesNotMatch(source,/Write-(?:Output|Host)\s+\$raw|Start-Transcript/i);
const tmp=mkdtempSync(join(tmpdir(),'task3b-local-'));
const file=join(tmp,'bliss-pg18-20261122T055153Z-b34126b0.pending.json');
const archive=join(tmp,'bliss-pg18-20261122T055153Z-b34126b0.gpg');
const base={
 schema:'alpha-bliss-pg18-recovery/v1',
 sourceProjectId:'fb33c1b7-03fb-42c8-89b1-081951a738fd',
 sourceEnvironmentId:'b67a9836-36bf-4083-93f7-00c867b13f39',
 sourceServiceId:'6aa78407-1632-48c9-8ea4-0289a7d36a5b',
 sourceDatabase:'bliss',sourcePostgresMajor:18,
 sourcePublicTableCount:99,capturedAtUtc:'2026-11-22T05:51:54Z',
 archiveFileName:'bliss-pg18-20261122T055153Z-b34126b0.gpg',
 archiveSha256:'0'.repeat(64),encryption:'GPG_RECIPIENT',
 gpgRecipientFingerprint:'A'.repeat(40),offhostProvider:'NOT_VERIFIED',
 offhostSha256Verified:false,isolatedRestoreVerified:false
};
function run(data){
 writeFileSync(file,JSON.stringify(data));
 const r=spawnSync('pwsh',['-NoProfile','-NonInteractive','-File',path,'-ManifestPath',file],
  {encoding:'utf8',timeout:10000,maxBuffer:4096});
 assert.equal(r.error,undefined);
 assert.notEqual(r.status,0);
 assert.doesNotMatch(r.stdout,/PASS_TEMPORARY_PG18_RESTORE/);
 return r.stderr+r.stdout;
}
try {
 writeFileSync(archive,'synthetic-no-real-backup');
 assert.match(run({...base,sourceProjectId:'bad'}),/REFUSED_TARGET_OR_CUSTODY_FIELDS/);
 assert.match(run({...base,offhostProvider:'DigitalOcean'}),/REFUSED_TARGET_OR_CUSTODY_FIELDS/);
 assert.match(run({...base,capturedAtUtc:'2026-11-21T05:07:19Z'}),/REFUSED_HISTORIC_OR_FUTURE_ARCHIVE/);
 assert.match(run({...base,archiveFileName:'bliss-pg18-20261121T050719Z-f21cde9d.gpg'}),/REFUSED_ARCHIVE_MANIFEST_PAIR_MISMATCH/);
 assert.match(run(base),/REFUSED_CIPHERTEXT_SHA256/);
 
// Stage diagnosis must remain non-secret, offline and fail-closed.
const helper=readFileSync('scripts/task4-creator-wsl-pg18-restore.sh','utf8');
const allowed=[
  'ARGUMENTS','NOT_ROOT','COUNT_INVALID','TOOLS_MISSING','PG18_BINARY_MISSING',
  'PG18_VERSION_MISMATCH','PG18_CLUSTER_NOT_READY','PG18_SOCKET_MISSING',
  'PG18_QUERY_REFUSED','PG18_MAJOR_MISMATCH','ARCHIVE_PATH_INVALID',
  'GPG_EXE_MISSING','TEMP_DB_NAME_INVALID','TEMP_DB_CHECK_REFUSED',
  'TEMP_DB_EXISTS','TEMP_DB_CREATE_REFUSED',
  'PREVIOUS_TEMP_DB_PRESENT','UNEXPECTED_HELPER_EXIT','GPG_DECRYPT_REFUSED','BOTH_PIPE_COMMANDS_REFUSED',
  'PIPE_STATUS_UNAVAILABLE',
  'PG_RESTORE_REFUSED','TABLE_COUNT_QUERY_REFUSED','TABLE_COUNT_MISMATCH',
  'EF_HISTORY_QUERY_REFUSED','EF_HISTORY_MISSING','TEMP_DB_REMOVE_REFUSED',
  'TEMP_DB_CLEANUP_UNVERIFIED','UNKNOWN'
];
assert.ok(helper.includes('TASK4_WSL_PG18_STAGE=%s'));
assert.ok(helper.includes('TASK4_WSL_PG18_RESTORE=REFUSED_OR_NOT_VERIFIED'));
assert.ok(helper.includes('pipe_status=('),'check each command exit status');
assert.ok(helper.includes('"$archive" </dev/null 2>/dev/null |'),
  'Windows GPG must not read the Bash script source from stdin');
assert.ok(source.includes('base64 -d | bash -s -- '),
  'use previously observed, quote-free Windows PowerShell to WSL transport');
assert.doesNotMatch(source,/exec bash -c "\$code"/,
  'do not require embedded shell quotes to survive Windows PowerShell 5.1');
assert.ok(helper.includes('"$archive" </dev/null 2>/dev/null |'),
  'Windows GPG must never inherit stdin carrying Bash source');
// Offline repro: a stdin-reading child can eat the rest of a bash -s script;
// with stdin explicitly closed, the script reaches its following instruction.
const scriptUnsafe='printf "TASK3B_PIPE=START\\n";\ncat >/dev/null\nprintf "TASK3B_PIPE=AFTER\\n"';
const scriptSafe='printf "TASK3B_PIPE=START\\n";\ncat </dev/null >/dev/null\nprintf "TASK3B_PIPE=AFTER\\n"';
const wrongTransport=spawnSync('bash',['-s'],{input:scriptUnsafe+'\n',encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(wrongTransport.status,0);
assert.doesNotMatch(wrongTransport.stdout,/TASK3B_PIPE=AFTER/);
const safeTransport=spawnSync('bash',['-s'],{input:scriptSafe+'\n',encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(safeTransport.status,0);
assert.match(safeTransport.stdout,/TASK3B_PIPE=AFTER/);
// Quote-free base64 command uses only base64 alphabet and decimal count;
// no local file paths, decrypted data or secrets participate in the test.
const testB64=Buffer.from(scriptSafe,'utf8').toString('base64');
const entry='set -o pipefail; printf %s '+testB64+' | base64 -d | bash -s -- QUJD 99 REVG';
const transport=spawnSync('bash',['-lc',entry],{encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(transport.status,0);
assert.match(transport.stdout,/TASK3B_PIPE=AFTER/);

assert.ok(helper.includes('fail GPG_DECRYPT_REFUSED'));
assert.ok(helper.includes('fail BOTH_PIPE_COMMANDS_REFUSED'));
assert.ok(source.includes("'BOTH_PIPE_COMMANDS_REFUSED'"));
assert.ok(helper.includes('fail PG_RESTORE_REFUSED'));
assert.ok(helper.includes('trap cleanup EXIT'));
assert.ok(helper.includes('trap - EXIT'));
assert.ok(helper.includes('TASK4_WSL_PG18_EXIT_CLEANUP=ABSENT_VERIFIED'));
assert.ok(helper.includes('TASK4_WSL_PG18_EXIT_CLEANUP=NOT_VERIFIED'));
assert.ok(helper.includes('PREVIOUS_TEMP_DB_PRESENT'));
assert.ok(helper.includes('UNEXPECTED_HELPER_EXIT'));
assert.ok(helper.includes('checkpoint PIPELINE_STARTED'));
assert.ok(helper.includes('checkpoint PIPELINE_RETURNED'));
assert.ok(helper.includes('stage_reported=1'));
assert.ok(helper.includes('[[ "${#pipe_status[@]}" -eq 2 ]] || fail PIPE_STATUS_UNAVAILABLE'));
const stageBlock=source.slice(source.indexOf('  if ($stages.Count -eq 1)'),
  source.indexOf('  $cleanups=@('));
assert.match(stageBlock,/}\s*else\s*\{\s*Write-Output 'TASK3B_WSL_LOCAL_STAGE=TRANSPORT_OR_UNCLASSIFIED'\s*}\s*# Independently show/);
assert.match(stageBlock,/TASK3B_WSL_LOCAL_LAST_CHECKPOINT=/);
const syntheticPipe=spawnSync('bash',['-c',
  'set -euo pipefail; set +e; printf x | cat >/dev/null; a=( "${PIPESTATUS[@]}" ); set -e; [[ "${#a[@]}" -eq 2 ]]'],
  {encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(syntheticPipe.status,0,'synthetic Bash PIPESTATUS length sanity');

assert.ok(helper.includes("SELECT count(*) FROM pg_database WHERE datname ~"));
assert.ok(source.includes('TASK3B_WSL_LOCAL_TEMP_DB=ABSENT_VERIFIED'));
assert.ok(source.includes('TASK3B_WSL_LOCAL_TEMP_DB=NOT_VERIFIED'));
assert.ok(source.includes('TASK4_WSL_PG18_EXIT_CLEANUP=(ABSENT_VERIFIED|NOT_VERIFIED)'));

const checkpoints=['PG18_READY','ARCHIVE_PATH_ACCEPTED','GPG_BINARY_READY','TEMP_DB_CREATED',
  'PIPELINE_STARTED','PIPELINE_RETURNED','PIPELINE_COMPLETE','TABLE_COUNT_VERIFIED','EF_HISTORY_VERIFIED','TEMP_DB_REMOVED'];
assert.ok(helper.includes('TASK4_WSL_PG18_CHECKPOINT=%s'),'checkpoint emits fixed vocabulary only');
assert.ok(source.includes('TASK3B_WSL_LOCAL_LAST_CHECKPOINT=NONE'));
assert.ok(source.includes('TASK3B_WSL_LOCAL_LAST_CHECKPOINT='));
for(const check of checkpoints) {
  assert.ok(helper.includes('checkpoint '+check),'helper checkpoint '+check);
  assert.ok(source.includes(check),'wrapper checkpoint '+check);
}
const checkpointRx=source.match(/\$line -cmatch '(\^TASK4_WSL_PG18_CHECKPOINT=[^']+)'/);
assert.ok(checkpointRx,'wrapper checkpoint regex present');
const checkpointProbe=spawnSync('pwsh',['-NoProfile','-NonInteractive','-Command',
  [
    '$rx='+"'"+checkpointRx[1]+"'",
    "if ('TASK4_WSL_PG18_CHECKPOINT=TEMP_DB_CREATED' -cnotmatch $rx) { exit 71 }",
    "if ('TASK4_WSL_PG18_CHECKPOINT=UNKNOWN' -cmatch $rx) { exit 72 }",
    "if ('PRIVATE_KEY' -cmatch $rx) { exit 73 }",
    "Write-Output 'TASK3B_SAFE_CHECKPOINT_FILTER=PASS_SYNTHETIC_ONLY'"
  ].join('; ')
],{encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(checkpointProbe.error,undefined);
assert.equal(checkpointProbe.status,0,'checkpoint parser must allow only fixed markers: '+checkpointProbe.stderr);
assert.match(checkpointProbe.stdout,/TASK3B_SAFE_CHECKPOINT_FILTER=PASS_SYNTHETIC_ONLY/);
assert.ok(helper.includes('[[ "$left" == "0" ]]'));
assert.ok(source.includes("Write-Output ('TASK3B_WSL_LOCAL_STAGE=' + $stages[0])"));
assert.ok(source.includes('TASK3B_WSL_LOCAL_STAGE=TRANSPORT_OR_UNCLASSIFIED'));
assert.doesNotMatch(source,/Write-(?:Output|Host)\s+\$(?:raw|line|archive|gpg|source)/);
for(const stage of allowed) assert.ok(source.includes("'"+stage+"'"),'missing stage '+stage);
// With no arguments, the helper must refuse before any DB or GPG operation.
const early=spawnSync('bash',['scripts/task4-creator-wsl-pg18-restore.sh'],
  {encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(early.status,2);
assert.match(early.stdout,/TASK4_WSL_PG18_STAGE=ARGUMENTS/);
assert.doesNotMatch(early.stdout,/pg_restore:|gpg:|password|\/mnt\/|\.gpg/);
console.log('TASK3B_WSL_RESTORE_STAGE_DIAGNOSTIC=PASS_SYNTHETIC_ONLY');

// Regression: the real Windows wrapper used [A-Z_]+ and silently discarded
// its PG18_* failure stages, replacing them with TRANSPORT_OR_UNCLASSIFIED.
// Extract and execute the exact reviewed PowerShell regex (not a duplicate).
const match=source.match(/\$line -cmatch '([^']+)' -and/);
assert.ok(match,'safe WSL stage regex must be present');
const stageRegex=match[1];
const safeRegex=stageRegex.replaceAll("'","''");
const pwshProbe=[
  "$rx='"+safeRegex+"'",
  "if ('TASK4_WSL_PG18_STAGE=PG18_CLUSTER_NOT_READY' -cnotmatch $rx) { exit 31 }",
  "if ('TASK4_WSL_PG18_STAGE=PG18_MAJOR_MISMATCH' -cnotmatch $rx) { exit 32 }",
  "if ('TASK4_WSL_PG18_STAGE=GPG_DECRYPT_REFUSED' -cnotmatch $rx) { exit 33 }",
  "if ('TASK4_WSL_PG18_STAGE=BOTH_PIPE_COMMANDS_REFUSED' -cnotmatch $rx) { exit 37 }",
  "if ('TASK4_WSL_PG18_STAGE=TEMP_DB_CREATE_REFUSED' -cnotmatch $rx) { exit 34 }",
  "if ('TASK4_WSL_PG18_STAGE=PG18_MAJOR_MISMATCH extra' -cmatch $rx) { exit 35 }",
  "if ('private: error details' -cmatch $rx) { exit 36 }",
  "Write-Output 'TASK3B_WSL_DIGIT_STAGE_FILTER=PASS_SYNTHETIC_ONLY'"
].join("; ");
const probe=spawnSync('pwsh',['-NoProfile','-NonInteractive','-Command',pwshProbe],
  {encoding:'utf8',timeout:10000,maxBuffer:4096});
assert.equal(probe.error,undefined);
assert.equal(probe.status,0,'actual wrapper stage regex should recognize PG18_* safely: '+probe.stderr);
assert.match(probe.stdout,/TASK3B_WSL_DIGIT_STAGE_FILTER=PASS_SYNTHETIC_ONLY/);
assert.ok(source.includes("'PG18_CLUSTER_NOT_READY'"),'stage must remain allowlisted');
assert.ok(source.includes("'PG18_MAJOR_MISMATCH'"),'stage must remain allowlisted');

console.log('TASK3B_CURRENT_WSL_LOCAL_SAFETY=PASS_OFFLINE_ONLY');
} finally {rmSync(tmp,{recursive:true,force:true});}
