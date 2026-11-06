import test from 'node:test';
import assert from 'node:assert/strict';
import { generateKeyPairSync } from 'node:crypto';
import { mkdtemp, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { SCOPE, publicKeyFingerprint, signTask, verifyReceipt } from './pilot.mjs';

const script = fileURLToPath(new URL('./pilot.mjs', import.meta.url));
const createFixture = async () => {
  const dir = await mkdtemp(join(tmpdir(), 'alpha-cli-'));
  const e = generateKeyPairSync('ed25519'), w = generateKeyPairSync('ed25519');
  const now = Date.now();
  const task = {id:'ALPHA-PILOT-SIGNEDCLI01', type:'SHA256_EVIDENCE', scope:SCOPE, payload:{text:'synthetic staging data'},issuedAt:new Date(now).toISOString(),expiresAt:new Date(now+120000).toISOString()};
  const paths = {task: join(dir,'signed-task.json'), executive:join(dir,'executive-public.pem'), worker:join(dir,'worker-private.pem'), journal:join(dir,'journal')};
  await writeFile(paths.task, JSON.stringify(signTask(task,e.privateKey)), {mode:0o600});
  await writeFile(paths.executive, e.publicKey.export({type:'spki',format:'pem'}), {mode:0o600});
  await writeFile(paths.worker, w.privateKey.export({type:'pkcs8',format:'pem'}), {mode:0o600});
  const env={...process.env,ALPHA_PILOT_ENABLED:'NO',ALPHA_EXEC_PUBLIC_KEY_FILE:paths.executive,ALPHA_EXEC_PIN_SHA256:publicKeyFingerprint(e.publicKey),ALPHA_WORKER_PRIVATE_KEY_FILE:paths.worker,ALPHA_PILOT_JOURNAL:paths.journal};
  return {paths,env,task,worker:w};
};
const invoke = ({paths,env}) => spawnSync(process.execPath,[script,'process-file',paths.task],{env,encoding:'utf8',timeout:10000});

test('real CLI is disabled by default', async () => {const f=await createFixture(); const r=invoke(f);assert.notEqual(r.status,0);assert.match(r.stderr,/CAPTAIN_STOP_ACTIVE/);});
test('real CLI requires independently provided pin', async () => {const f=await createFixture();f.env.ALPHA_PILOT_ENABLED='YES';delete f.env.ALPHA_EXEC_PIN_SHA256;const r=invoke(f);assert.notEqual(r.status,0);assert.match(r.stderr,/MISSING_ALPHA_EXEC_PIN_SHA256/);});
test('real CLI rejects incorrect fingerprint', async () => {const f=await createFixture();f.env.ALPHA_PILOT_ENABLED='YES';f.env.ALPHA_EXEC_PIN_SHA256='0'.repeat(64);const r=invoke(f);assert.notEqual(r.status,0);assert.match(r.stderr,/CAPTAIN_PIN_MISMATCH/);});
test('real CLI produces signed receipt with pinned verification key', async () => {const f=await createFixture();f.env.ALPHA_PILOT_ENABLED='YES';const r=invoke(f);assert.equal(r.status,0,r.stderr);const receipt=JSON.parse(r.stdout);assert.equal(verifyReceipt(receipt,f.worker.publicKey,f.task),true);});
test('real CLI refuses replay after successful processing', async () => {const f=await createFixture();f.env.ALPHA_PILOT_ENABLED='YES';assert.equal(invoke(f).status,0);const replay=invoke(f);assert.notEqual(replay.status,0);assert.match(replay.stderr,/REPLAY_REJECTED/);});
