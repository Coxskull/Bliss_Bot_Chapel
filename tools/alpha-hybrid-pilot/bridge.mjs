// Offline bridge protocol. No network, no GitHub posting, no model calls.
import { sign, verify } from 'node:crypto';
import { canonical, digest, validateEnvelope, verifyReceipt, SCOPE } from './pilot.mjs';

export const BRIDGE_REPOSITORY = 'Alpha-Galaxy/alpha-shipping-yard-test';
export const BRIDGE_ISSUE = 3;
export const EXECUTIVE_GITHUB_ACTOR = 'alpha7auto';

const exact = (o, keys) => o && typeof o === 'object' && !Array.isArray(o) && Object.keys(o).sort().join(',') === keys.slice().sort().join(',');
const validSignature = x => typeof x === 'string' && /^[A-Za-z0-9+/]+={0,2}$/.test(x) && Buffer.from(x, 'base64').length === 64;
const createSignature = (payload, key) => sign(null, Buffer.from(canonical(payload)), key).toString('base64');
const verifySignature = (payload, signature, key) => validSignature(signature) && verify(null, Buffer.from(canonical(payload)), key, Buffer.from(signature, 'base64'));

/**
 * Trusted context MUST come from the authenticated GitHub API result, not from
 * the body of a comment or user-controlled fields supplied by an AI prompt.
 * The pinned public key MUST be independently approved by Captain.
 */
export function parseExecutiveComment(context, commentBody, pinnedExecutivePublicKey, now = Date.now()) {
  if (!exact(context, ['repository', 'issue', 'actor']) ||
      context.repository !== BRIDGE_REPOSITORY ||
      context.issue !== BRIDGE_ISSUE ||
      context.actor !== EXECUTIVE_GITHUB_ACTOR) throw Error('UNTRUSTED_PROVIDER_PROVENANCE');
  if (typeof commentBody !== 'string' || Buffer.byteLength(commentBody) > 16_384) throw Error('OVERSIZED_COMMENT');
  let envelope;
  try { envelope = JSON.parse(commentBody); }
  catch { throw Error('INVALID_ENVELOPE_JSON'); }
  const task = validateEnvelope(envelope, pinnedExecutivePublicKey, now);
  return { envelope, task };
}

export function signAcknowledgment(task, workerPrivateKey, now = Date.now()) {
  const ack = {
    taskId: task.id,
    taskDigest: digest(task),
    scope: SCOPE,
    status: 'ACCEPTED_STAGING_ONLY',
    acknowledgedAt: new Date(now).toISOString()
  };
  return { actor: 'ENGINEERING', ack, signature: createSignature(ack, workerPrivateKey) };
}

export function verifyAcknowledgment(signed, workerPublicKey, task) {
  if (!exact(signed, ['actor', 'ack', 'signature']) || signed.actor !== 'ENGINEERING') return false;
  const a = signed.ack;
  if (!exact(a, ['taskId', 'taskDigest', 'scope', 'status', 'acknowledgedAt']) ||
      a.taskId !== task.id || a.taskDigest !== digest(task) || a.scope !== SCOPE ||
      a.status !== 'ACCEPTED_STAGING_ONLY' || !Number.isFinite(Date.parse(a.acknowledgedAt))) return false;
  try { return verifySignature(a, signed.signature, workerPublicKey); }
  catch { return false; }
}

export function signExecutiveVerification(signedResult, workerPublicKey, task, executivePrivateKey, now = Date.now()) {
  if (!verifyReceipt(signedResult, workerPublicKey, task)) throw Error('INVALID_WORKER_RESULT');
  const verdict = {
    taskId: task.id,
    taskDigest: digest(task),
    receiptDigest: digest(signedResult),
    scope: SCOPE,
    decision: 'VERIFIED_STAGING_ONLY',
    verifiedAt: new Date(now).toISOString()
  };
  return { actor: 'EXECUTIVE', verdict, signature: createSignature(verdict, executivePrivateKey) };
}

export function verifyExecutiveVerification(signed, execPublicKey, signedResult, task) {
  if (!exact(signed, ['actor', 'verdict', 'signature']) || signed.actor !== 'EXECUTIVE') return false;
  const v = signed.verdict;
  if (!exact(v, ['taskId', 'taskDigest', 'receiptDigest', 'scope', 'decision', 'verifiedAt']) ||
      v.taskId !== task.id || v.taskDigest !== digest(task) || v.receiptDigest !== digest(signedResult) ||
      v.scope !== SCOPE || v.decision !== 'VERIFIED_STAGING_ONLY' ||
      !Number.isFinite(Date.parse(v.verifiedAt))) return false;
  try { return verifySignature(v, signed.signature, execPublicKey); }
  catch { return false; }
}
