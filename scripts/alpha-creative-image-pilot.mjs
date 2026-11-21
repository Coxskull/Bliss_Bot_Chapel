#!/usr/bin/env node
// ALPHA-CREATIVE-IMAGE-PILOT-20261120
// Existing Bliss Creative Academy / controlled-AI owner-side image pilot adapter.
// Default is OFFLINE DRY RUN. No customer, Factory STOP, Sailor, or payment action.
// One paid image per HTTPS request, no auto retries, durable debit BEFORE dispatch.
import { mkdirSync, openSync, closeSync, readFileSync, writeFileSync, renameSync, existsSync, unlinkSync, chmodSync, fsyncSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { createHash } from 'node:crypto';
import { performance } from 'node:perf_hooks';

const DIRECTIVE = 'ALPHA-CREATIVE-IMAGE-PILOT-20261120';
const MODEL = 'gpt-image-2.5-flare';
const API = 'https://api.openai.com/v1/images/generations';
const CAP = 20;
const RESERVATION = 2.00; // conservative, charges reconciled after each successful response
const QUALITY = 'low'; // bound image output size/cost; no auto/xhigh/max
const SIZE = '1024x1024';
const MAX_IMAGES = 11;
const args = process.argv.slice(2);
const execute = args.includes('--execute');
const dryRun = args.includes('--dry-run') || !execute;
const maxNew = args.find(x => x.startsWith('--max-new='));
const count = maxNew ? Number(maxNew.split('=')[1]) : 1;
const stateDir = resolve(process.env.ALPHA_IMAGE_PILOT_DIR || join(process.cwd(), '.private', 'alpha-creative-image-pilot'));
const statePath = join(stateDir, 'pilot-ledger.json');
const lockPath = join(stateDir, 'pilot.lock');
const briefPath = new URL('./alpha-creative-image-pilot-brief.json', import.meta.url);
const hash = b => createHash('sha256').update(b).digest('hex');
function abort(msg) { throw new Error('ALPHA_IMAGE_PILOT_HOLD: ' + msg); }
function accurateUsd(u) {
  if (!u || !Number.isSafeInteger(u.input_tokens) || !Number.isSafeInteger(u.output_tokens)
      || u.output_tokens <= 0 || !u.input_tokens_details) abort('PROVIDER_USAGE_MISSING');
  const t = u.input_tokens_details.text_tokens, i = u.input_tokens_details.image_tokens;
  if (!Number.isSafeInteger(t) || !Number.isSafeInteger(i) || t < 0 || i < 0 ||
      u.input_tokens !== t + i) abort('PROVIDER_USAGE_INVALID');
  const microUsd = t * 5 + i * 8 + u.output_tokens * 30;
  if (!Number.isSafeInteger(microUsd) || microUsd <= 0) abort('PROVIDER_CHARGE_INVALID');
  // integer micro-USD arithmetic (rates are dollars per million tokens).
  return Math.ceil(microUsd) / 1_000_000;
}
function atomicWrite(path, body) {
  const tmp = path + '.new-' + process.pid;
  const fd = openSync(tmp, 'wx', 0o600);
  try { writeFileSync(fd, body); fsyncSync(fd); } finally { closeSync(fd); }
  renameSync(tmp, path);
  // Best-effort directory metadata durability across OS/filesystems.
  let dirFd;
  try { dirFd=openSync(stateDir,'r'); fsyncSync(dirFd); }
  catch { /* Windows may refuse directory flush; file content was fsynced. */ }
  finally { if(dirFd !== undefined) closeSync(dirFd); }
}
function readState() {
  if (!existsSync(statePath)) return {schema:'alpha.creative.pilot.ledger.v1',
    directive:DIRECTIVE,model:MODEL,capUsd:CAP,spentMicroUsd:0,heldMicroUsd:0,
    entries:[],closed:false, noDelivery:true};
  const s = JSON.parse(readFileSync(statePath, 'utf8'));
  if (s.schema !== 'alpha.creative.pilot.ledger.v1' || s.directive !== DIRECTIVE ||
      s.model !== MODEL || s.capUsd !== CAP || !s.noDelivery ||
      !Number.isSafeInteger(s.spentMicroUsd) || !Number.isSafeInteger(s.heldMicroUsd) ||
      !Array.isArray(s.entries) || s.entries.length > MAX_IMAGES || s.closed) abort('LEDGER_CLOSED_OR_INVALID');
  // No spend/retry after an interrupted or ambiguous reservation.
  if (s.entries.some(e => e.status !== 'COMPLETE' || !e.providerRequestId ||
      !Number.isSafeInteger(e.costMicroUsd))) abort('UNKNOWN_OR_UNRECONCILED_PROVIDER_CHARGE');
  const total = s.entries.reduce((sum,e)=>sum + e.costMicroUsd,0);
  if (total !== s.spentMicroUsd || s.heldMicroUsd !== 0 || total > CAP * 1_000_000)
    abort('LEDGER_MISMATCH_OR_OVER_BUDGET');
  return s;
}
function verifyIntent() {
  if (process.env.ALPHA_IMAGE_PILOT_OWNER_APPROVAL !== DIRECTIVE ||
      process.env.ALPHA_IMAGE_PILOT_PROVIDER_LIMIT_USD !== '20.00' ||
      process.env.ALPHA_IMAGE_PILOT_AUTO_RECHARGE !== 'OFF' ||
      process.env.ALPHA_IMAGE_PILOT_SCOPE !== 'ISOLATED_NO_DELIVERY' ||
      process.env.ALPHA_GLOBAL_STOP !== 'ENGAGED') abort('OWNER_BILLING_OR_ISOLATION_ATTESTATION_MISSING');
  if (!process.env.OPENAI_API_KEY || !process.env.OPENAI_API_KEY.startsWith('sk-'))
    abort('PRIVATE_OPENAI_PROJECT_KEY_MISSING');
  if (process.env.CI || process.env.GITHUB_ACTIONS) abort('NEVER_RUN_PAID_CALL_IN_CI');
}
function validateBrief(b) {
  if (b.directive !== DIRECTIVE || b.model !== MODEL || !Array.isArray(b.artworks) ||
      b.artworks.length !== MAX_IMAGES || b.artworks[0].role !== 'MASTER' ||
      b.artworks.slice(1).some(x => x.role !== 'VARIATION')) abort('INVALID_OWNER_ARTWORK_BRIEF');
  if (b.artworks.some((x, n) => x.id !== n || typeof x.prompt !== 'string' ||
      x.prompt.length < 35 || x.prompt.length > 1200 || typeof x.language !== 'string'))
    abort('INVALID_ARTWORK_PROMPT');
}
async function paidOne(job, ledger) {
  const reservedMicroUsd = Math.round(RESERVATION * 1_000_000);
  if (ledger.spentMicroUsd + ledger.heldMicroUsd + reservedMicroUsd > CAP * 1_000_000)
    abort('INSUFFICIENT_REMAINING_RESERVATION');
  const receipt = {
    index:job.id, role:job.role, language:job.language, model:MODEL,
    promptSha256:hash(job.prompt), quality:QUALITY, size:SIZE,
    reservedMicroUsd, status:'UNKNOWN_AFTER_RESERVATION',
    startedAt:new Date().toISOString(), delivery:'NOT_SENT', campaignReady:false
  };
  ledger.entries.push(receipt); ledger.heldMicroUsd += reservedMicroUsd;
  // Durable reservation before API network connection. Any crash/timeout stops future calls.
  atomicWrite(statePath, JSON.stringify(ledger,null,2) + '\n');
  const started = performance.now();
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 180000);
  try {
    const response = await fetch(API, {
      method:'POST',signal:controller.signal,headers:{
        'Content-Type':'application/json',
        'Authorization':'Bearer ' + process.env.OPENAI_API_KEY,
        'Idempotency-Key':DIRECTIVE + '-' + job.id
      },
      body:JSON.stringify({model:MODEL,quality:QUALITY,size:SIZE,
        n:1,output_format:'png',prompt:job.prompt})
    });
    // Never auto-retry including on 429, 5xx, network interruption, or missing billing receipt.
    if (!response.ok) abort('PROVIDER_REJECTED_HTTP_' + response.status);
    const raw = await response.text();
    if (raw.length > 36_000_000) abort('PROVIDER_RESPONSE_TOO_LARGE');
    const payload = JSON.parse(raw);
    const costUsd = accurateUsd(payload.usage);
    const costMicroUsd = Math.ceil(costUsd * 1_000_000);
    if (costMicroUsd > reservedMicroUsd ||
        ledger.spentMicroUsd + costMicroUsd > CAP * 1_000_000)
      abort('PROVIDER_CHARGE_EXCEEDS_RESERVED_BUDGET');
    const b64 = payload.data?.[0]?.b64_json;
    if (typeof b64 !== 'string' || b64.length > 35_000_000) abort('INVALID_IMAGE_RESPONSE');
    const bytes = Buffer.from(b64,'base64');
    if (bytes.length < 100 || bytes.length > 25_000_000 ||
        bytes.subarray(0,8).toString('hex') !== '89504e470d0a1a0a') abort('INVALID_PNG');
    const name = 'alpha-image-' + String(job.id).padStart(2,'0') + '.png';
    const imagePath = join(stateDir,name);
    const imageFd = openSync(imagePath,'wx',0o600);
    try { writeFileSync(imageFd,bytes); } finally { closeSync(imageFd); }
    receipt.status='COMPLETE';
    receipt.providerRequestId=String(response.headers.get('x-request-id') || payload.id || '').slice(0,150);
    if (!receipt.providerRequestId) abort('MISSING_PROVIDER_REQUEST_ID');
    receipt.costMicroUsd=costMicroUsd;
    receipt.inputTokens=payload.usage.input_tokens;
    receipt.textInputTokens=payload.usage.input_tokens_details.text_tokens;
    receipt.imageInputTokens=payload.usage.input_tokens_details.image_tokens;
    receipt.outputTokens=payload.usage.output_tokens;
    receipt.latencyMs=Math.round(performance.now()-started);
    receipt.imageSha256=hash(bytes);
    receipt.imageFile=name;
    receipt.completedAt=new Date().toISOString();
    ledger.spentMicroUsd += costMicroUsd;
    ledger.heldMicroUsd -= reservedMicroUsd;
    atomicWrite(statePath, JSON.stringify(ledger,null,2) + '\n');
    console.log('PILOT_IMAGE_COMPLETE=' + job.id + ' COST_USD=' + costUsd.toFixed(6) +
      ' CUMULATIVE_USD=' + (ledger.spentMicroUsd/1e6).toFixed(6));
  } catch (err) {
    ledger.closed = true;
    // Keep the entire uncertain reserve permanently held. No retry allowed.
    atomicWrite(statePath, JSON.stringify(ledger,null,2) + '\n');
    throw new Error('PILOT_STOPPED_UNKNOWN_CHARGE_NO_RETRY; reason=' +
      String(err?.message||'PROVIDER_FAILURE').replace(/sk-[A-Za-z0-9_-]+/g,'REDACTED'));
  } finally { clearTimeout(timer); }
}
async function main() {
  const b=JSON.parse(readFileSync(briefPath,'utf8')); validateBrief(b);
  if (!Number.isInteger(count) || count<1 || count>MAX_IMAGES) abort('MAX_NEW_MUST_BE_1_TO_11');
  if (dryRun) {
    console.log(JSON.stringify({mode:'DRY_RUN_NO_API',directive:DIRECTIVE,model:MODEL,
      capUsd:CAP,reservationUsd:RESERVATION,quality:QUALITY,size:SIZE,
      planned:b.artworks.map(({id,role,language})=>({id,role,language})),
      stop:'ENGAGED_NOT_MODIFIED',delivery:'NOT_SENT',liveUsageUsd:'NOT_VERIFIED'},null,2));
    return;
  }
  verifyIntent();
  mkdirSync(stateDir,{recursive:true,mode:0o700});
  chmodSync(stateDir,0o700);
  let lock;
  try { lock=openSync(lockPath,'wx',0o600); }
  catch { abort('PILOT_LOCK_EXISTS_DO_NOT_RUN_CONCURRENTLY'); }
  try {
    const state=readState();
    let submitted=0;
    for(const job of b.artworks) {
      if (state.entries.some(e=>e.index===job.id)) continue; // no replay, no new cost
      if(submitted>=count) break;
      await paidOne(job,state);
      submitted++;
    }
    console.log('PILOT_REMAINING_AUTHORIZED_USD=' +
      ((CAP*1e6-state.spentMicroUsd-state.heldMicroUsd)/1e6).toFixed(6));
    console.log('PILOT_ACCOUNTING=PROVIDER_TOKEN_ESTIMATE_RECONCILE_WITH_ACTUAL_API_BILLING');
    console.log('PILOT_GLOBAL_STOP=UNMODIFIED; PUBLIC_DELIVERY=NOT_SENT');
  } finally {
    closeSync(lock);
    // The lock is removed only after all state transitions are durably written.
    unlinkSync(lockPath);
  }
}
await main().catch(err => { console.error(String(err.message||'PILOT_FAILED')); process.exitCode=2; });
