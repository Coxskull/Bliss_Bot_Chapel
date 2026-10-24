// ALPHA-HYBRID-003: OAuth/provider-identity pilot. No user-managed signing key.
import { createHash } from 'node:crypto';
import { readFileSync, appendFileSync } from 'node:fs';

const REPO = 'Coxskull/Bliss_Bot_Chapel';
const SCOPE = REPO + '/staging';
const OWNER = { login: 'Coxskull', id: 119336582 };
const exact = (v, fields) => !!v && typeof v === 'object' && !Array.isArray(v) &&
  Object.keys(v).sort().join('|') === fields.slice().sort().join('|');
const sha256 = s => createHash('sha256').update(s, 'utf8').digest('hex');
const iso = s => typeof s === 'string' && /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$/.test(s) && Number.isFinite(Date.parse(s));

export function validateProvider(event, repository = REPO) {
  // event MUST be the GitHub-generated GITHUB_EVENT_PATH, not AI-produced text.
  const p = event?.pull_request;
  if (repository !== REPO || !p || !['opened', 'synchronize', 'reopened'].includes(event.action))
    throw Error('UNSUPPORTED_PROVIDER_EVENT');
  if (event.repository?.full_name !== REPO || p.base?.repo?.full_name !== REPO ||
      p.base?.ref !== 'main' || p.head?.repo?.full_name !== REPO ||
      p.user?.login !== OWNER.login || p.user?.id !== OWNER.id ||
      event.sender?.login !== OWNER.login || event.sender?.id !== OWNER.id ||
      p.draft !== true || p.state !== 'open' || !Number.isSafeInteger(p.number) || p.number < 1 ||
      !/^[0-9a-f]{40}$/.test(p.head?.sha ?? '') ||
      !p.title?.startsWith('[ALPHA-OAUTH-PILOT]'))
    throw Error('UNTRUSTED_GITHUB_PROVENANCE');
  return { repository: REPO, pullRequest: p.number, commit: p.head.sha, event: event.action, actor: OWNER.login };
}

export function validateTask(task, now = Date.now()) {
  if (!exact(task, ['protocol', 'id', 'type', 'scope', 'mode', 'payload', 'issuedAt', 'expiresAt', 'maxAdditionalSpendUsd']) ||
      task.protocol !== 'ALPHA-OAUTH/v1' || !/^ALPHA-OAUTH-\d{8}-[A-Z0-9]{3,20}$/.test(task.id) ||
      task.type !== 'SHA256_EVIDENCE' || task.scope !== SCOPE ||
      task.mode !== 'READ_ONLY' || task.maxAdditionalSpendUsd !== 0 ||
      typeof task.payload !== 'string' || Buffer.byteLength(task.payload, 'utf8') > 256 ||
      !iso(task.issuedAt) || !iso(task.expiresAt))
    throw Error('TASK_SCHEMA_OR_SCOPE_DENIED');
  const created = Date.parse(task.issuedAt), expiry = Date.parse(task.expiresAt);
  if (created > now + 30_000 || expiry <= now || expiry > created + 7 * 86_400_000)
    throw Error('TASK_EXPIRED_OR_BAD_TTL');
  return task;
}

export function evaluate(event, task, { repository = REPO, now = Date.now(), enabled = false } = {}) {
  // A real OAuth/ChatGPT account connection is NOT implied by this deterministic result.
  if (!enabled) throw Error('PILOT_DISABLED');
  const source = validateProvider(event, repository);
  const allowed = validateTask(task, now);
  return {
    protocol: 'ALPHA-OAUTH/v1',
    status: 'EVIDENCE_ONLY_PASS',
    account2Acknowledged: false,
    taskId: allowed.id,
    taskSha256: sha256(JSON.stringify(allowed)),
    resultSha256: sha256(allowed.payload),
    source,
    mode: 'READ_ONLY',
    verifiedAt: new Date(now).toISOString(),
  };
}

function main() {
  if (process.env.ALPHA_OAUTH_PILOT_ENABLED !== 'YES') throw Error('PILOT_DISABLED');
  if (process.env.GITHUB_EVENT_NAME !== 'pull_request' || !process.env.GITHUB_EVENT_PATH)
    throw Error('MISSING_TRUSTED_GITHUB_EVENT');
  const event = JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
  const task = JSON.parse(readFileSync(new URL('./task.json', import.meta.url), 'utf8'));
  const result = evaluate(event, task, { repository: process.env.GITHUB_REPOSITORY, enabled: true });
  console.log(JSON.stringify(result, null, 2));
  if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY,
    '### ALPHA OAuth Pilot — provider-authenticated deterministic result\n\n' +
    '- Task: ' + result.taskId + '\n- SHA-256: ' + result.resultSha256 + '\n' +
    '- Source PR: #' + result.source.pullRequest + ' at ' + result.source.commit + '\n' +
    '- Account 2 OAuth/Work trigger: **NOT VERIFIED**\n' +
    '- No API calls, secrets, deployments, messages, merges or production changes by this worker.\n');
}

if (process.argv[1]?.endsWith('/pilot.mjs')) {
  try { main(); } catch (error) { console.error(error.message); process.exitCode = 1; }
}
