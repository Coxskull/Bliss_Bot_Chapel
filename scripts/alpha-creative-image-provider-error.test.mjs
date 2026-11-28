import assert from 'node:assert/strict';
import { test } from 'node:test';
import { summarizeProviderRejection } from './alpha-creative-image-provider-error.mjs';

function reply(error, id='req_synthetic123') {
  const body=JSON.stringify({error});
  return {status:429,headers:{get(name){
    if(name==='content-length')return String(body.length);
    if(name==='x-request-id')return id;
    return null;
  }},async text(){return body;}};
}

test('429 credit-exhaustion is classified without leaking error.message', async()=>{
  const e=await summarizeProviderRejection(reply({
    code:'credit_balance_exhausted',type:'insufficient_quota',
    message:'sk-proj-SECRET-MUST-NEVER-LEAK'
  }));
  assert.deepEqual(e,{
    httpStatus:429,errorCode:'credit_balance_exhausted',
    errorType:'insufficient_quota',providerRequestId:'req_synthetic123'
  });
  assert.doesNotMatch(JSON.stringify(e),/SECRET|message/);
});
test('429 rate-limit code is preserved, not treated as prepaid exhaustion', async()=>{
  const e=await summarizeProviderRejection(reply({code:'rate_limit_exceeded',type:'rate_limit_error'}));
  assert.equal(e.errorCode,'rate_limit_exceeded');
});
test('free-form, malformed, oversized and untrusted metadata are suppressed', async()=>{
  const e=await summarizeProviderRejection(reply({
    code:'sk-proj-PrivateKey',type:'\nsecret'
  },'attacker-req\nAuthorization: secret'));
  assert.equal(e.errorCode,'UNAVAILABLE');
  assert.equal(e.errorType,'UNAVAILABLE');
  assert.equal(e.providerRequestId,'UNAVAILABLE');
  const oversized={status:429,headers:{get(n){return n==='content-length'?'9000':null;}},async text(){throw Error('should not read');}};
  assert.equal((await summarizeProviderRejection(oversized)).errorCode,'UNAVAILABLE');
  const invalid={status:429,headers:{get(){return null;}},async text(){return 'not json';}};
  assert.equal((await summarizeProviderRejection(invalid)).errorCode,'UNAVAILABLE');
});
test('offline diagnostic code has no paid request or retry',async()=>{
  const fs=await import('node:fs');
  const src=fs.readFileSync(new URL('./alpha-creative-image-provider-error.mjs',import.meta.url),'utf8');
  assert.doesNotMatch(src,/api\.openai\.com|fetch\s*\(|Authorization:/);
});
