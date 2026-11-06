import test from 'node:test';
import assert from 'node:assert/strict';
import { generateKeyPairSync } from 'node:crypto';
import { mkdtemp } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { SCOPE, signTask, executeEnvelope, digest } from './pilot.mjs';
import { BRIDGE_REPOSITORY, BRIDGE_ISSUE, EXECUTIVE_GITHUB_ACTOR, parseExecutiveComment, signAcknowledgment, verifyAcknowledgment, signExecutiveVerification, verifyExecutiveVerification } from './bridge.mjs';

const e = generateKeyPairSync('ed25519'), w = generateKeyPairSync('ed25519');
const now = Date.now();
const task = { id: 'ALPHA-PILOT-HANDOFF01', type: 'SHA256_EVIDENCE', scope: SCOPE, payload: { text: 'only synthetic local test data' }, issuedAt: new Date(now).toISOString(), expiresAt: new Date(now + 120000).toISOString() };
const env = signTask(task, e.privateKey);
const context = { repository: BRIDGE_REPOSITORY, issue: BRIDGE_ISSUE, actor: EXECUTIVE_GITHUB_ACTOR };
const parse = (ctx=context, body=JSON.stringify(env), pk=e.publicKey) => parseExecutiveComment(ctx, body, pk, now);

test('full offline signed TASK -> ACK -> RESULT -> VERIFIED roundtrip', async () => {
  const { task: got, envelope } = parse();
  assert.equal(digest(got), digest(task));
  assert.deepEqual(envelope, JSON.parse(JSON.stringify(env)));
  const ack = signAcknowledgment(got, w.privateKey, now);
  assert.equal(verifyAcknowledgment(ack, w.publicKey, got), true);
  const journalDir = await mkdtemp(join(tmpdir(), 'alpha-exchange-'));
  const result = await executeEnvelope(env, { executivePublicKey: e.publicKey, workerPrivateKey: w.privateKey, journalDir, enabled: true, now });
  const verified = signExecutiveVerification(result, w.publicKey, got, e.privateKey, now);
  assert.equal(verifyExecutiveVerification(verified, e.publicKey, result, got), true);
});
test('spoofed provider actor rejected even with valid signature', () => assert.throws(() => parse({ ...context, actor: 'anonymous' }), /UNTRUSTED_PROVIDER_PROVENANCE/));
test('wrong issue rejected', () => assert.throws(() => parse({ ...context, issue: 1 }), /UNTRUSTED_PROVIDER_PROVENANCE/));
test('wrong repository rejected', () => assert.throws(() => parse({ ...context, repository: 'untrusted/repo' }), /UNTRUSTED_PROVIDER_PROVENANCE/));
test('provider metadata cannot have unexpected override fields', () => assert.throws(() => parse({ ...context, actorInBody: EXECUTIVE_GITHUB_ACTOR }), /UNTRUSTED_PROVIDER_PROVENANCE/));
test('unparseable message rejected', () => assert.throws(() => parse(context, '# task'), /INVALID_ENVELOPE_JSON/));
test('oversized message rejected', () => assert.throws(() => parse(context, 'A'.repeat(16_385)), /OVERSIZED_COMMENT/));
test('un-pinned alternate executive key rejected', () => { const x = generateKeyPairSync('ed25519'); assert.throws(() => parse(context, JSON.stringify(signTask(task, x.privateKey))), /UNTRUSTED_SIGNATURE/); });
test('ack mutation rejected', () => { const a=signAcknowledgment(task,w.privateKey,now); a.ack.taskDigest='0'.repeat(64); assert.equal(verifyAcknowledgment(a,w.publicKey,task),false); });
test('ack from wrong worker rejected', () => { const x=generateKeyPairSync('ed25519'); assert.equal(verifyAcknowledgment(signAcknowledgment(task,x.privateKey,now),w.publicKey,task),false); });
test('cannot sign verification of forged worker result', () => { const result={actor:'ENGINEERING',receipt:{taskId:task.id,taskDigest:digest(task),resultSha256:'0'.repeat(64),status:'PASS',scope:SCOPE,completedAt:new Date(now).toISOString()},signature:'AAAA'}; assert.throws(() => signExecutiveVerification(result,w.publicKey,task,e.privateKey,now),/INVALID_WORKER_RESULT/); });
test('executive verification rejects substituted receipt', async () => {
  const journalDir=await mkdtemp(join(tmpdir(),'alpha-verify-'));
  const result=await executeEnvelope(env,{executivePublicKey:e.publicKey,workerPrivateKey:w.privateKey,journalDir,enabled:true,now});
  const verdict=signExecutiveVerification(result,w.publicKey,task,e.privateKey,now);
  const substituted={...result, signature:'AAAA'};
  assert.equal(verifyExecutiveVerification(verdict,e.publicKey,substituted,task),false);
});
test('ACK never bypasses Captain stop', async () => { const ack = signAcknowledgment(task,w.privateKey,now); assert.equal(verifyAcknowledgment(ack,w.publicKey,task),true); await assert.rejects(executeEnvelope(env,{executivePublicKey:e.publicKey,workerPrivateKey:w.privateKey,journalDir:await mkdtemp(join(tmpdir(),'alpha-stop-')),enabled:false,now}),/CAPTAIN_STOP_ACTIVE/); });
