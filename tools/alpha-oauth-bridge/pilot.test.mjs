import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { validateProvider, validateTask, evaluate } from './pilot.mjs';

const task = JSON.parse(readFileSync(new URL('./task.json', import.meta.url), 'utf8'));
const now = Date.parse('2026-10-24T02:30:00Z');
const repo = 'Coxskull/Bliss_Bot_Chapel';
const owner = { login: 'Coxskull', id: 119336582 };
const makeEvent = () => ({
  action: 'opened',
  repository: { full_name: repo },
  sender: { ...owner },
  pull_request: {
    number: 312, draft: true, state: 'open', title: '[ALPHA-OAUTH-PILOT] synthetic staging only',
    user: { ...owner },
    base: { ref: 'main', repo: { full_name: repo } },
    head: { sha: 'a'.repeat(40), repo: { full_name: repo } }
  }
});
const enabled = { repository: repo, now, enabled: true };
const deny = (mutate, pattern) => { const event = makeEvent(); mutate(event); assert.throws(() => evaluate(event, task, enabled), pattern); };

test('read-only provider verified SHA-256 task succeeds', () => {
  const result = evaluate(makeEvent(), task, enabled);
  assert.equal(result.status, 'EVIDENCE_ONLY_PASS');
  assert.equal(result.resultSha256, createHash('sha256').update(task.payload).digest('hex'));
  assert.equal(result.account2Acknowledged, false);
});
test('worker starts stopped by default', () => assert.throws(() => evaluate(makeEvent(), task), /PILOT_DISABLED/));
test('wrong provider repo denied', () => assert.throws(() => evaluate(makeEvent(), task, { ...enabled, repository: 'attacker/repo' }), /UNSUPPORTED_PROVIDER_EVENT/));
test('wrong sender denied', () => deny(e => {e.sender.login='intruder';}, /UNTRUSTED_GITHUB_PROVENANCE/));
test('sender numeric identity mismatch denied', () => deny(e => {e.sender.id=999;}, /UNTRUSTED_GITHUB_PROVENANCE/));
test('wrong PR author denied', () => deny(e => {e.pull_request.user.login='intruder';}, /UNTRUSTED_GITHUB_PROVENANCE/));
test('fork head repository denied', () => deny(e => {e.pull_request.head.repo.full_name='intruder/fork';}, /UNTRUSTED_GITHUB_PROVENANCE/));
test('non-draft PR denied', () => deny(e => {e.pull_request.draft=false;}, /UNTRUSTED_GITHUB_PROVENANCE/));
test('wrong target branch denied', () => deny(e => {e.pull_request.base.ref='release';}, /UNTRUSTED_GITHUB_PROVENANCE/));
test('spoofed PR title denied', () => deny(e => {e.pull_request.title='deploy production';}, /UNTRUSTED_GITHUB_PROVENANCE/));
test('missing trusted PR commit denied', () => deny(e => {e.pull_request.head.sha='not-a-commit';}, /UNTRUSTED_GITHUB_PROVENANCE/));
test('comment activity not supported as source', () => deny(e => {e.action='created';}, /UNSUPPORTED_PROVIDER_EVENT/));
test('only one deterministic task type allowed', () => assert.throws(() => validateTask({...task,type:'RUN_SHELL'},now), /TASK_SCHEMA_OR_SCOPE_DENIED/));
test('additional spend must be zero', () => assert.throws(() => validateTask({...task,maxAdditionalSpendUsd:1},now), /TASK_SCHEMA_OR_SCOPE_DENIED/));
test('rejects oversized task body', () => assert.throws(() => validateTask({...task,payload:'x'.repeat(257)},now), /TASK_SCHEMA_OR_SCOPE_DENIED/));
test('unexpected fields rejected', () => assert.throws(() => validateTask({...task,command:'rm -rf /'},now), /TASK_SCHEMA_OR_SCOPE_DENIED/));
test('expired request denied', () => assert.throws(() => validateTask(task,Date.parse('2026-10-31T00:00:00Z')), /TASK_EXPIRED_OR_BAD_TTL/));
test('invalid TTL longer than 7 days denied', () => assert.throws(() => validateTask({...task,expiresAt:'2026-11-30T02:00:00Z'},now), /TASK_EXPIRED_OR_BAD_TTL/));
test('changed scope denied', () => assert.throws(() => validateTask({...task,scope:'Alpha-Galaxy/alpha-shipping-yard-test'},now), /TASK_SCHEMA_OR_SCOPE_DENIED/));
test('read-only task parse works at staging current timestamp', () => assert.equal(validateTask(task,now).id,task.id));
test('GitHub provider event parse gives trace IDs', () => assert.deepEqual(validateProvider(makeEvent(),repo),{repository:repo,pullRequest:312,commit:'a'.repeat(40),event:'opened',actor:'Coxskull'}));
