import test from 'node:test';
import assert from 'node:assert/strict';
import {createReviewApi,validateResult} from './api.js';
const id='00000000-0000-4000-8000-000000000001',key='00000000-0000-4000-8000-000000000002',fp='A'.repeat(64);
const note={id,operationId:id,actorSubject:'server-actor',evidenceFingerprint:fp,note:'Observed.',recordedAt:'2026-10-14T00:00:00Z'};
const envelope=result=>({mode:'MOCK',stripeAccountStatus:'UNVERIFIED / OWNER DECISION REQUIRED',paymentCompleted:false,approvalGranted:false,holdReleased:false,result});
const response=(data,status=200)=>({ok:status===200,status,json:async()=>data});
test('annotation uses cookie/CSRF and sends only versioned observation, never financial authority',async()=>{let call;const api=createReviewApi(async(path,options)=>{call={path,options};return response(envelope(note));});await api.add(id,fp,'Observed.',key,'synthetic-csrf');assert.equal(call.options.credentials,'same-origin');assert.equal(call.options.redirect,'error');assert.equal(call.options.headers['X-CSRF-TOKEN'],'synthetic-csrf');assert.deepEqual(JSON.parse(call.options.body),{operationId:id,evidenceFingerprint:fp,note:'Observed.',idempotencyKey:key});});
test('rejects live/approval/release and malformed monetary responses',async()=>{for(const change of [{mode:'LIVE'},{holdReleased:true},{approvalGranted:true}]){const api=createReviewApi(async()=>response({...envelope(note),...change}));await assert.rejects(()=>api.add(id,fp,'Observed.',key,'csrf'));}assert.equal(validateResult('queue',{items:[{operationId:id,scope:{merchantId:id,campaignId:id,module:0},currency:'USD',amountMinor:9007199254740993,reason:'review'}],nextAfter:null}),false);});
test('API numeric enum maps without losing large exact integer amounts',async()=>{const api=createReviewApi(async()=>response(envelope({items:[{operationId:id,scope:{merchantId:id,campaignId:id,module:2},currency:'USD',amountMinor:'9007199254740993',reason:'review'}],nextAfter:null})));const q=await api.queue('',null);assert.equal(q.items[0].scope.module,'WeddingPlanner');assert.equal(q.items[0].amountMinor,'9007199254740993');});
test('unknown writes and stale/denied responses are not automatically retried',async()=>{for(const status of [0,401,403,409,429]){let calls=0;const api=createReviewApi(async()=>{calls++;if(!status)throw Error('connection');return response({},status);});await assert.rejects(()=>api.add(id,fp,'Observed.',key,'csrf'),e=>e.status===status);assert.equal(calls,1);}});
test('exact retry lookup is read-only and explicit null remains unresolved',async()=>{let call;const api=createReviewApi(async(path,options)=>{call={path,options};return response(envelope(null));});assert.equal(await api.retry(id,key),null);assert.ok(call.path.endsWith('/note-retries/'+key));assert.equal(call.options.method,undefined);});

test('recovery context is read-only and refuses missing actor or live authority',async()=>{let call;const api=createReviewApi(async(path,options)=>{call={path,options};return response(envelope({actorKey:fp}));});assert.equal((await api.recoveryContext()).actorKey,fp);assert.equal(call.options.method,undefined);for(const data of [envelope({}),envelope({actorKey:'subject'}),{...envelope({actorKey:fp}),paymentCompleted:true}])await assert.rejects(()=>createReviewApi(async()=>response(data)).recoveryContext());});


test('read-only mock commercial evidence verifies balanced exact minor units and no unapproved payouts',async()=>{
  const large='9007199254740993';
  const base={operationId:id,quoteVersionId:key,scope:{merchantId:id,campaignId:key,module:1},
    currency:'USD',amountMinor:large,captureJournalId:id,sourceEvidenceSha256:fp,mode:'MOCK',
    paymentCompleted:false,providerCalled:false,alphaCommissionStatus:'NO_APPROVED_ALLOCATION',
    creatorCompensationStatus:'NO_APPROVED_ALLOCATION',creatorPayoutAuthorized:false,
    campaignDeliveryAuthorized:false,lines:[
      {accountId:id,ownerId:key,role:'TEST_CASH',side:'Debit',amountMinor:large},
      {accountId:key,ownerId:id,role:'TEST_RECIPIENT',side:'Credit',amountMinor:large}
    ]};
  let called;
  const api=createReviewApi(async(path,options)=>{called={path,options};return response(envelope(base));});
  const result=await api.campaignEvidence(id);
  assert.equal(result.amountMinor,large);
  assert.ok(called.path.endsWith('/'+id+'/campaign-evidence'));
  assert.equal(called.options.method,undefined);
  assert.equal(called.options.credentials,'same-origin');
  assert.equal(validateResult('campaignEvidence',base),true);
  assert.equal(validateResult('campaignEvidence',{...base,captureJournalId:null,lines:[]}),true);
  for (const changed of [
    {amountMinor:9007199254740993},
    {creatorPayoutAuthorized:true},
    {alphaCommissionStatus:'PAID'},
    {paymentCompleted:true},
    {lines:[...base.lines.slice(0,1),{...base.lines[1],amountMinor:'1'}]},
    {lines:[...base.lines.slice(0,1),{...base.lines[1],side:'Debit'}]},
    {captureJournalId:null},
    {lines:[{...base.lines[0],role:'TEST_OTHER'},base.lines[1]]},
    {lines:[base.lines[1],base.lines[0]]},
    {lines:[...base.lines,{...base.lines[0],amountMinor:'0'}]},
    {scope:{...base.scope,merchantId:'not-a-guid'}}
  ]) assert.equal(validateResult('campaignEvidence',{...base,...changed}),false);
  assert.throws(()=>api.campaignEvidence('not-a-uuid'),/Exact campaign funding operation ID required/);
});
