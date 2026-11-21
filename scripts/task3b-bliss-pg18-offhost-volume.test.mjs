// ALPHA Task3B: execute ONLY the extracted off-host shell preflight against
// fabricated local mount metadata. No Railway, DigitalOcean, credential or API call.
import assert from 'node:assert/strict';
import {readFileSync, writeFileSync, chmodSync, existsSync, mkdirSync, rmSync, mkdtempSync, symlinkSync, statSync} from 'node:fs';
import {join} from 'node:path';
import {tmpdir} from 'node:os';
import {spawnSync} from 'node:child_process';
const source=readFileSync('scripts/task3b-bliss-pg18-offhost.ps1','utf8');
const match=source.match(/\$remoteCommand\s*=\s*@'\r?\n([\s\S]*?)\r?\n'@/);
assert.ok(match,'expected explicit isolated shell preflight');
assert.match(source,/Get-Item -LiteralPath \$archivePath/);
assert.match(source,/Math\]::Ceiling\(\$archiveBytes \/ 1024\.0\) \+ 102400/);
assert.match(source,/NONROOT_ATTACHED_VOLUME_OR_SPACE_NOT_VERIFIED/);
assert.match(source,/StrictHostKeyChecking=yes/g);
assert.ok(source.indexOf('$remoteCommand = @')<source.indexOf('& $scp -o BatchMode=yes'),
    'volume check must precede any encrypted upload');
const tmp=mkdtempSync(join(tmpdir(),'alpha-task3b-offhost-'));
const disk=join(tmp,'attached-existing');
const bin=join(tmp,'fake-tools');
mkdirSync(disk);
mkdirSync(bin);
const stub=(name,s)=>{const p=join(bin,name);writeFileSync(p,s);chmodSync(p,0o700);};
stub('findmnt',[
'#!/bin/sh',
'case "$5" in',
'  TARGET) printf "%s\\n" "$FAKE_MOUNT_TARGET" ;;',
'  SOURCE) printf "%s\\n" "$FAKE_MOUNT_SOURCE" ;;',
'  FSTYPE) printf "%s\\n" "$FAKE_MOUNT_TYPE" ;;',
'  *) exit 8 ;;',
'esac',''].join('\n'));
stub('df',[
'#!/bin/sh',
'printf "Filesystem 1024-blocks Used Available Capacity Mounted on\\n"',
'printf "/dev/fake 1000000 1 %s 1%% /mnt/test\\n" "$FAKE_FREE_KIB"',
''].join('\n'));
const path=join(disk,'alpha-bliss-pg18','private');
const filename='bliss-pg18-offlinefixture.gpg';
const script=match[1].replaceAll('__REMOTE_DIRECTORY__',path)
  .replaceAll('__REMOTE_ARCHIVE__',join(path,filename))
  .replaceAll('__REQUIRED_KIB__','102500');
const env=(override={})=>({...process.env,PATH:bin+':'+process.env.PATH,
  FAKE_MOUNT_TARGET:disk,FAKE_MOUNT_SOURCE:'/dev/mock-block-volume',
  FAKE_MOUNT_TYPE:'ext4',FAKE_FREE_KIB:'500000',...override});
const run=(e={})=>spawnSync('sh',['-c',script],{encoding:'utf8',timeout:10000,env:env(e)});
try {
  let result=run({FAKE_MOUNT_TARGET:'/'});
  assert.notEqual(result.status,0,'root filesystem must not be accepted');
  assert.equal(existsSync(path),false,'root disk must have no directory writes');
  result=run({FAKE_MOUNT_SOURCE:'tmpfs'});
  assert.notEqual(result.status,0,'non-disk mount must be rejected');
  assert.equal(existsSync(path),false);
  result=run({FAKE_MOUNT_TYPE:'tmpfs'});
  assert.notEqual(result.status,0,'tmpfs must be rejected');
  result=run({FAKE_FREE_KIB:'100'});
  assert.notEqual(result.status,0,'insufficient capacity must be rejected');
  assert.equal(existsSync(path),false);
  result=run();
  assert.equal(result.status,0,result.stderr);
  assert.equal(result.stdout.trim(),'TASK3B_MOUNTED_VOLUME_READY');
  assert.equal(statSync(path).mode & 0o777,0o700);
  writeFileSync(join(path,filename),'synthetic-untrusted-fixture-only');
  result=run();
  assert.notEqual(result.status,0,'existing destination archive must never be overwritten');
  assert.equal(readFileSync(join(path,filename),'utf8'),'synthetic-untrusted-fixture-only');
  const link=join(disk,'linked-path');
  symlinkSync(path,link,'dir');
  const symlinkScript=match[1].replaceAll('__REMOTE_DIRECTORY__',link+'/new')
      .replaceAll('__REMOTE_ARCHIVE__',link+'/new/'+filename)
      .replaceAll('__REQUIRED_KIB__','102500');
  result=spawnSync('sh',['-c',symlinkScript],{encoding:'utf8',timeout:10000,env:env()});
  assert.notEqual(result.status,0,'symlink destination must be rejected');
  assert.equal(existsSync(join(path,'new')),false);
  console.log('TASK3B_OFFHOST_EXISTING_NONROOT_VOLUME_CAPACITY_SYMLINK_REFUSAL_PASS (offline / $0)');
} finally {
  rmSync(tmp,{recursive:true,force:true});
}
