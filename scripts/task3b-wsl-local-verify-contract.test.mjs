// Offline safety regression. Never run Railway, GPG decryption or real WSL.
import assert from 'node:assert/strict';
import {readFileSync,mkdtempSync,writeFileSync,rmSync} from 'node:fs';
import {join} from 'node:path';
import {tmpdir} from 'node:os';
import {spawnSync} from 'node:child_process';
const path='scripts/task3b-wsl-local-verify-current.ps1';
const source=readFileSync(path,'utf8');
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
  'PREVIOUS_TEMP_DB_PRESENT','UNEXPECTED_HELPER_EXIT','GPG_DECRYPT_REFUSED',
  'PIPE_STATUS_UNAVAILABLE',
  'PG_RESTORE_REFUSED','TABLE_COUNT_QUERY_REFUSED','TABLE_COUNT_MISMATCH',
  'EF_HISTORY_QUERY_REFUSED','EF_HISTORY_MISSING','TEMP_DB_REMOVE_REFUSED',
  'TEMP_DB_CLEANUP_UNVERIFIED','UNKNOWN'
];
assert.ok(helper.includes('TASK4_WSL_PG18_STAGE=%s'));
assert.ok(helper.includes('TASK4_WSL_PG18_RESTORE=REFUSED_OR_NOT_VERIFIED'));
assert.ok(helper.includes('pipe_status=('),'check each command exit status');
assert.ok(helper.includes('fail GPG_DECRYPT_REFUSED'));
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
