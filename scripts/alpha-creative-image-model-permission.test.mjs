import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { probeModel } from './alpha-creative-image-model-permission.mjs';
const options={key:'sk-offline-fake-test-key',scope:'ISOLATED_NO_DELIVERY',stop:'ENGAGED'};
test('Windows helper accepts the project key only as hidden input and restores process environment',()=>{
 const root=dirname(fileURLToPath(import.meta.url));
 const helper=readFileSync(join(root,'alpha-creative-image-model-permission.ps1'),'utf8');
 assert.match(helper,/Read-Host[\\s\\S]*?-AsSecureString/i);
 assert.match(helper,/SecureStringToBSTR/);
 assert.match(helper,/ZeroFreeBSTR/);
 assert.match(helper,/alpha-creative-image-model-permission\\.mjs/);
 assert.match(helper,/ISOLATED_NO_DELIVERY/);
 assert.match(helper,/ALPHA_GLOBAL_STOP/);
 assert.match(helper,/finally[\\s\\S]*?OPENAI_API_KEY = \\$previousKey/);
 assert.doesNotMatch(helper,/--execute|images\\/generations|Write-Host.*plainKey|Write-Output.*plainKey/i);
});

test('uses only GET model metadata and never performs paid generation',async()=>{
 let called=0;
 const result=await probeModel(async (url,opts)=>{
   called++;
   assert.equal(url,'https://api.openai.com/v1/models/gpt-image-2.5-flare');
   assert.equal(opts.method,'GET');
   assert.equal(opts.redirect,'error');
   assert.equal(opts.headers.Authorization,'Bearer '+options.key);
   return {ok:true,status:200,text:async()=>JSON.stringify({id:'gpt-image-2.5-flare',object:'model'})};
 },options);
 assert.equal(called,1);assert.equal(result.result,'PROJECT_MODEL_VISIBLE');
 assert.equal(result.generationEntitlement,'NOT_TESTED');
 assert.equal(result.imageCount,0);assert.equal(result.paidCalls,0);
 assert.equal(result.delivery,'NOT_SENT');
});
test('refuses when scope, stop or private key missing',async()=>{
 let count=0;
 const fetch=async()=>{count++;throw Error('should never be called')};
 for(const o of [{...options,stop:'RELEASED'},{...options,scope:'PRODUCTION'},{...options,key:''}]){
   await assert.rejects(()=>probeModel(fetch,o),/MODEL_PROBE_HELD/);
 }
 assert.equal(count,0);
});
test('access rejected but does not reveal provider error body',async()=>{
 const result=await probeModel(async()=>({ok:false,status:403,text:async()=>{throw Error('must never read denial body')}}),options);
 assert.equal(result.result,'ACCESS_NOT_VERIFIED');assert.equal(result.httpStatus,403);
 assert.equal(result.paidCalls,0);
});
test('rejects wrong model or malicious metadata response',async()=>{
 for(const value of [{id:'other',object:'model'},{id:'gpt-image-2.5-flare',object:'unknown'}]){
   await assert.rejects(()=>probeModel(async()=>({ok:true,status:200,text:async()=>JSON.stringify(value)}),options),/WRONG_MODEL_METADATA/);
 }
});
