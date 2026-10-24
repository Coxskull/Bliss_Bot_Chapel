import test from 'node:test';
import assert from 'node:assert/strict';
import { webcrypto, generateKeyPairSync, sign } from 'node:crypto';
import {
  QUEUE_NAME, PINS, BOUNDARY, canonical, sha256,verifyApproval,inspectSeed,
  envConfigured,consumeOne,sourceCheck,emergencyStop
} from '../worker.mjs';
globalThis.crypto ??= webcrypto;

const { privateKey,publicKey }=generateKeyPairSync('ed25519');
const spki=publicKey.export({type:'spki',format:'der'}).toString('base64');
const TEST_NOW=Date.parse('2026-10-24T08:02:00.000Z');
function envelope(code='PH',override={}) {
  const e={task_id:`AGM-SCOUT-REQ-20261024-${code}`,manifest_sha256:PINS[code][0],approved_by:'ALPHA Captain',
    approved_at:'2026-10-24T08:00:00.000Z', expires_at:'2026-10-24T08:10:00.000Z',nonce:'1234567890abcdef1234567890abcdef'};
  Object.assign(e,override);
  e.signature=sign(null, Buffer.from(canonical(e),'utf8'),privateKey).toString('base64');
  return e;
}
function manifest(code='PH',override={}) {
  const item={id:'ABC',candidate_record_id:'SRC-1',name:'Test Creator',creator:'Test Creator',
    official_url:`https://${PINS[code][1]}/`, contact:'hello@example.org',contact_url:`https://${PINS[code][1]}/`,
    category:'Test',location_status:'PUBLIC_LOCATION_SUPPORTED_PENDING_WORKER_RECHECK',
    source_urls:[`https://${PINS[code][1]}/`],verify_marker:'Test Creator',verify_contact_marker:'hello@example.org'};
  return JSON.stringify({task_id:`AGM-SCOUT-REQ-20261024-${code}`,schema:'AGM_SIX_CITY_CREATOR_SOURCE_CHECK_V1',
    scope:'STAGING_CREATOR_PUBLIC_SOURCE_ONLY',market:{country_code:code,city:'Test City'},
    execution_policy:{ai_inference_calls:0,max_spend_cents:0,no_outreach:true,no_production_writes:true,requires_captain_signed_envelope:true},
    seeds:[{...item,...override}]});
}
const validEnv={SCOUT_DB:{},SCOUT_RUN_ENABLED:'YES',SCOUT_COST_CAP_READY:'YES',SCOUT_QUEUE_AUTOPAUSE_READY:'YES',
  SCOUT_QUEUE_CONTROL_TOKEN:'test-value-no-live-access',CAPTAIN_PUBLIC_KEY_SPKI_B64:spki,
  CF_ACCOUNT_ID:'2f29b8ee363840bd737f867a54019513',SCOUT_QUEUE_ID:'cecb61fd9d154360a686febf8693db36'};

test('exact bounded six-city task pins',()=>{
  assert.deepEqual(Object.keys(PINS),['CO','DO','ID','MY','PA','PH']);
  for(const entry of Object.values(PINS))assert.match(entry[0],/^[a-f0-9]{64}$/);
  assert.equal(BOUNDARY,'ALPHA-CREATOR-SCOUT-ONESHOT-v2-MANIFEST');
  assert.equal(QUEUE_NAME,'alpha-creator-scout-staging-trigger');
});
test('valid Ed25519 for all six cities',async()=>{
  for(const city of Object.keys(PINS))assert.equal((await verifyApproval(envelope(city),spki,TEST_NOW)).ok,true,city);
});
test('expired approval is rejected',async()=>{
  assert.equal((await verifyApproval(envelope(),spki,Date.parse('2026-10-24T08:11:00Z'))).reason,'EXPIRED_OR_INVALID_WINDOW');
});
test('future approval is rejected',async()=>{
  assert.equal((await verifyApproval(envelope(),spki,Date.parse('2026-10-24T07:59:00Z'))).ok,false);
});
test('tampering with approval fails crypto',async()=>{
  const e=envelope();e.approved_by='Other Captain';
  assert.equal((await verifyApproval(e,spki,TEST_NOW)).reason,'BAD_SIGNATURE');
});
test('missing public verification key fails closed',async()=>{
  assert.equal((await verifyApproval(envelope(),null,TEST_NOW)).reason,'PUBLIC_KEY_UNAVAILABLE');
});
test('unknown task fails closed',async()=>{
  const e=envelope();e.task_id='AGM-SCOUT-REQ-20261024-XX';
  assert.equal((await verifyApproval(e,spki,TEST_NOW)).reason,'TASK_OR_MANIFEST_NOT_PINNED');
});
test('manifest digest mismatch fails closed',async()=>{
  const e=envelope();e.manifest_sha256='a'.repeat(64);
  assert.equal((await verifyApproval(e,spki,TEST_NOW)).ok,false);
});
test('additional envelope fields fail closed',async()=>{
  const e=envelope();e.extra='x';
  assert.equal((await verifyApproval(e,spki,TEST_NOW)).reason,'INVALID_FIELDS');
});
test('invalid nonce fails closed',async()=>{
  const e=envelope('DO',{nonce:'not-a-nonce'});
  assert.equal((await verifyApproval(e,spki,TEST_NOW)).reason,'INVALID_NONCE');
});
test('bad signature bytes are rejected',async()=>{
  const e=envelope();e.signature=Buffer.alloc(64).toString('base64');
  assert.equal((await verifyApproval(e,spki,TEST_NOW)).ok,false);
});
test('seed allowlist accepts six structurally valid drafts',()=>{
  for(const c of Object.keys(PINS))assert.equal(inspectSeed(manifest(c),`AGM-SCOUT-REQ-20261024-${c}`,PINS[c][0]).id,'ABC');
});
test('seed rejects SSRF host change',()=>{
  assert.throws(()=>inspectSeed(manifest('PH',{official_url:'https://169.254.169.254/',source_urls:['https://169.254.169.254/']}),'AGM-SCOUT-REQ-20261024-PH',PINS.PH[0]),/SOURCE_NOT_ALLOWLISTED/);
});
test('seed rejects spending change',()=>{
  const o=JSON.parse(manifest());o.execution_policy.max_spend_cents=1;
  assert.throws(()=>inspectSeed(JSON.stringify(o),'AGM-SCOUT-REQ-20261024-PH',PINS.PH[0]),/SEED_POLICY_INVALID/);
});
test('seed rejects mismatched country and wrong digest',()=>{
  assert.throws(()=>inspectSeed(manifest('PH'),'AGM-SCOUT-REQ-20261024-DO',PINS.DO[0]));
  assert.throws(()=>inspectSeed(manifest('PH'),'AGM-SCOUT-REQ-20261024-PH','a'.repeat(64)));
});
test('missing or false security env gate',()=>{
  assert.equal(envConfigured(validEnv),true);
  for(const k of Object.keys(validEnv))assert.equal(envConfigured({...validEnv,[k]:k==='SCOUT_RUN_ENABLED'?'NO':null}),false,k);
});
test('disabled Worker cannot research',async()=>{
  assert.equal((await consumeOne(envelope(),{...validEnv,SCOUT_RUN_ENABLED:'NO'},{now:TEST_NOW})).reason,'CONFIGURATION_GATES');
});
test('unverified signature cannot reach D1',async()=>{
  const r=await consumeOne(envelope(),{...validEnv,CAPTAIN_PUBLIC_KEY_SPKI_B64:undefined},{now:TEST_NOW});
  assert.equal(r.status,'BLOCKED');
});
test('emergency stop reports queue pause and D1 stop without leaking token',async()=>{
  const calls=[];
  const db={prepare(sql){assert.match(sql,/UPDATE scout_control|SELECT key,value/);return {async run(){return {meta:{changes:2}}},async all(){return {results:[{key:'captain_stop',value:'1'},{key:'scout_enabled',value:'0'}]}}};}};
  const result=await emergencyStop({...validEnv,SCOUT_DB:db},async(url,options)=>{
    calls.push({url,method:options.method,body:options.body});
    return {ok:true,async json(){return {success:true,result:{settings:{delivery_paused:true}}}}};
  });
  assert.equal(result.d1,true);assert.equal(result.queue,true);
  assert.equal(calls.length,2);assert.equal(calls[0].method,'PATCH');assert.equal(JSON.parse(calls[0].body).settings.delivery_paused,true);assert.equal(calls[1].method,'GET');
});
test('source check never follows redirects',async()=>{
  const r=await sourceCheck('https://www.chinoy.tv/chinoy-podcast-network/','CHiNOY',async(url,options)=>{
    assert.equal(options.redirect,'manual');return {status:302,ok:false,headers:new Headers({location:'https://evil.example'})};
  });
  assert.equal(r,'REDIRECT_REVIEW');
});
test('source check obtains deterministic review outcome',async()=>{
  const response=new Response('Welcome Test Creator, a real official webpage.',{status:200,headers:{'content-type':'text/html'}});
  assert.equal(await sourceCheck('https://example.com/','Test Creator',async()=>response),'PUBLIC_MARKER_FOUND_NEEDS_HUMAN_REVIEW');
});
test('source check rejects oversized stream',async()=>{
  const response=new Response('a'.repeat(524289),{status:200,headers:{'content-type':'text/html'}});
  assert.equal(await sourceCheck('https://example.com/','Test Creator',async()=>response),'SIZE_REVIEW');
});
test('digest function matches Node sha256',async()=>{
  assert.equal(await sha256('hello'),'2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824');
});
