// ALPHA Hybrid Pilot: staging-only, dependency-free, default-deny signed task runner.
import { createHash, generateKeyPairSync, sign, verify } from 'node:crypto';
import { mkdir, open, readFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { mkdtemp } from 'node:fs/promises';

export const SCOPE = 'ALPHA-HYBRID-PILOT-STAGING';
const TYPES = new Set(['SHA256_EVIDENCE']);
export const canonical = value => JSON.stringify(value, (_k, v) => v && typeof v === 'object' && !Array.isArray(v) ? Object.fromEntries(Object.keys(v).sort().map(k => [k, v[k]])) : v);
export const digest = value => createHash('sha256').update(typeof value === 'string' ? value : canonical(value)).digest('hex');
const keysExact = (o, required) => o && !Array.isArray(o) && typeof o === 'object' && Object.keys(o).sort().join(',') === required.slice().sort().join(',');
const parseDate = x => typeof x === 'string' && /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$/.test(x) ? Date.parse(x) : NaN;

export function signTask(task, privateKey) {
  return { actor: 'EXECUTIVE', task, signature: sign(null, Buffer.from(canonical(task)), privateKey).toString('base64') };
}

export function validateEnvelope(env, executivePublicKey, now = Date.now()) {
  if (!keysExact(env, ['actor', 'task', 'signature']) || env.actor !== 'EXECUTIVE') throw Error('BAD_ENVELOPE');
  const t = env.task;
  if (!keysExact(t, ['id', 'type', 'scope', 'payload', 'issuedAt', 'expiresAt'])) throw Error('BAD_TASK_SCHEMA');
  if (t.scope !== SCOPE || !TYPES.has(t.type) || !/^ALPHA-PILOT-[A-Za-z0-9_-]{8,64}$/.test(t.id)) throw Error('OUT_OF_SCOPE');
  if (!keysExact(t.payload, ['text']) || typeof t.payload.text !== 'string' || Buffer.byteLength(t.payload.text) > 500) throw Error('BAD_PAYLOAD');
  const issued = parseDate(t.issuedAt), expiry = parseDate(t.expiresAt);
  if (!Number.isFinite(issued) || !Number.isFinite(expiry) || issued > now + 30_000 || issued < now - 300_000 || expiry <= now || expiry > issued + 900_000) throw Error('EXPIRED_OR_INVALID_TTL');
  if (typeof env.signature !== 'string' || !/^[A-Za-z0-9+/]+={0,2}$/.test(env.signature)) throw Error('BAD_SIGNATURE_FORMAT');
  const signatureBytes = Buffer.from(env.signature, 'base64');
  if (signatureBytes.length !== 64 || !verify(null, Buffer.from(canonical(t)), executivePublicKey, signatureBytes)) throw Error('UNTRUSTED_SIGNATURE');
  return t;
}

export async function executeEnvelope(env, { executivePublicKey, workerPrivateKey, journalDir, enabled = false, now = Date.now() }) {
  if (!enabled) throw Error('CAPTAIN_STOP_ACTIVE');
  const task = validateEnvelope(env, executivePublicKey, now);
  if (typeof journalDir !== 'string' || !journalDir) throw Error('JOURNAL_REQUIRED');
  await mkdir(journalDir, { recursive: true, mode: 0o700 });
  const path = join(journalDir, `${task.id}.claimed`);
  // Atomic, fail-closed claim: all retries fail, even if an earlier execution failed mid-flight.
  let file;
  try { file = await open(path, 'wx', 0o600); }
  catch (err) { if (err.code === 'EEXIST') throw Error('REPLAY_REJECTED'); throw err; }
  try { await file.writeFile(digest(task)); } finally { await file.close(); }
  const receipt = { taskId: task.id, taskDigest: digest(task), resultSha256: digest(task.payload.text), status: 'PASS', scope: SCOPE, completedAt: new Date(now).toISOString() };
  return { actor: 'ENGINEERING', receipt, signature: sign(null, Buffer.from(canonical(receipt)), workerPrivateKey).toString('base64') };
}

export function verifyReceipt(signedReceipt, workerPublicKey, task) {
  if (!keysExact(signedReceipt, ['actor', 'receipt', 'signature']) || signedReceipt.actor !== 'ENGINEERING') return false;
  const r = signedReceipt.receipt;
  if (!keysExact(r, ['taskId','taskDigest','resultSha256','status','scope','completedAt'])) return false;
  if (r.scope !== SCOPE || r.status !== 'PASS' || r.taskId !== task.id || r.taskDigest !== digest(task) || r.resultSha256 !== digest(task.payload.text)) return false;
  try { return verify(null, Buffer.from(canonical(r)), workerPublicKey, Buffer.from(signedReceipt.signature, 'base64')); } catch { return false; }
}

export async function demo() {
  // DEMO-ONLY ephemeral keys: not the Captain's real signing identity.
  const executive = generateKeyPairSync('ed25519');
  const engineering = generateKeyPairSync('ed25519');
  const now = Date.now();
  const task = { id: 'ALPHA-PILOT-LOCALDEMO1', type: 'SHA256_EVIDENCE', scope: SCOPE, payload: { text: 'ALPHA hybrid pilot staging evidence' }, issuedAt: new Date(now).toISOString(), expiresAt: new Date(now + 120_000).toISOString() };
  const env = signTask(task, executive.privateKey);
  const journalDir = await mkdtemp(join(tmpdir(), 'alpha-hybrid-pilot-'));
  const receipt = await executeEnvelope(env, { executivePublicKey: executive.publicKey, workerPrivateKey: engineering.privateKey, journalDir, enabled: true, now });
  const verified = verifyReceipt(receipt, engineering.publicKey, task);
  let replayBlocked = false;
  try { await executeEnvelope(env, { executivePublicKey: executive.publicKey, workerPrivateKey: engineering.privateKey, journalDir, enabled: true, now }); } catch (e) { replayBlocked = e.message === 'REPLAY_REJECTED'; }
  return { localDemo: true, separateEphemeralKeys: true, taskId: task.id, taskDigest: digest(task), resultSha256: receipt.receipt.resultSha256, signedReceiptVerified: verified, replayBlocked, networkCalls: 0, externalWrites: 0, captainSigningKeyUsed: false, pass: verified && replayBlocked };
}

// Real mode requires externally pinned Executive public key and worker private key FILE.
// No remote side effects, GitHub access, model usage, or auto-scheduling included.
if (process.argv[1]?.endsWith('/pilot.mjs')) {
  const cmd = process.argv[2];
  try {
    if (cmd === 'demo') console.log(JSON.stringify(await demo(), null, 2));
    else if (cmd === 'process-file') {
      if (process.env.ALPHA_PILOT_ENABLED !== 'YES') throw Error('CAPTAIN_STOP_ACTIVE');
      for (const k of ['ALPHA_EXEC_PUBLIC_KEY_FILE', 'ALPHA_WORKER_PRIVATE_KEY_FILE', 'ALPHA_PILOT_JOURNAL']) if (!process.env[k]) throw Error(`MISSING_${k}`);
      if (!process.argv[3]) throw Error('MISSING_TASK_FILE');
      const env = JSON.parse(await readFile(process.argv[3], 'utf8'));
      const receipt = await executeEnvelope(env, { executivePublicKey: await readFile(process.env.ALPHA_EXEC_PUBLIC_KEY_FILE, 'utf8'), workerPrivateKey: await readFile(process.env.ALPHA_WORKER_PRIVATE_KEY_FILE, 'utf8'), journalDir: process.env.ALPHA_PILOT_JOURNAL, enabled: true });
      console.log(JSON.stringify(receipt));
    } else throw Error('USAGE: node pilot.mjs demo | process-file TASK.json');
  } catch (err) { console.error(err.message); process.exitCode = 1; }
}
