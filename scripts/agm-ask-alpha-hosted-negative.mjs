// Task #20 hosted, PUBLIC and NON-BILLABLE negative acceptance.
// No keys, tokens, customer data, or positive/paid inference calls.
// Designed for the existing Github Actions runner, not an external test service.
import assert from 'node:assert/strict';

const url = 'https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-public-intake';
const allowedOrigin = 'https://alphacreativecontent.com';
const request = {
  action: 'ask_alpha',
  notice_version: 'AGM-ASK-ALPHA-AI-2026-11-08',
  ai_processing_consent: true,
  session_id: 'd70d9f71-406a-4d0e-90fb-d41e267a3025',
  request_key: '8593d71a-2ec5-4b83-adba-4d2fe6e88932',
  audience: 'general',
  message: 'Explain the public advertiser interest process.'
};
let checks = 0;
async function check(label, {method='POST', origin=allowedOrigin, body=request, contentType='application/json', extraHeaders={},allowGatewayReject=false}, status, code) {
  const headers = {Origin:origin,...extraHeaders};
  if (contentType) headers['Content-Type'] = contentType;
  const response = await fetch(url, {
    method,headers,body: method==='POST' ? JSON.stringify(body):undefined,
    signal:AbortSignal.timeout(12000),
    redirect:'error'
  });
  let result = {};
  try { result = await response.json(); } catch {}
  assert.equal(response.status,status,`${label}: unexpected HTTP status ${response.status}`);
  // Managed WAF may reject spoofed CF headers before the Edge Runtime sees
  // them. Accept a non-JSON 403 as an edge denial ONLY for the spoof probe.
  const gatewayReject = allowGatewayReject && response.status === 403 &&
    !(response.headers.get('content-type') || '').includes('application/json');
  if (!gatewayReject) {
    assert.equal(result.error,code,`${label}: unexpected public error code ${result.error}`);
  }
  assert.equal(result.receipt_id,undefined,`${label}: must not create an AI receipt`);
  checks += 1;
  console.log('PASS '+label+' '+response.status+' '+(gatewayReject ? 'EDGE_GATEWAY_REJECTED_FORGED_HEADER' : code));
}
await check('unknown-origin-denied',{origin:'https://attacker.invalid'},403,
 'This form is available from ALPHA\'s official website only.');
await check('missing-origin-denied',{origin:''},403,
 'This form is available from ALPHA\'s official website only.');
await check('no-consent-denied',{body:{...request,ai_processing_consent:false}},403,'CONSENT_REQUIRED');
await check('wrong-notice-denied',{body:{...request,notice_version:'legacy'}},403,'CONSENT_REQUIRED');
// Secure no-consent test: attempt to supply a fabricated Cloudflare client IP.
// Supabase logs can establish whether the managed gateway overwrote it; this
// branch never performs a billed request even if AI is enabled in the future.
await check('cf-header-forgery-cannot-enable-inference',{
  body:{...request,ai_processing_consent:false},
  extraHeaders:{
    'cf-connecting-ip':'203.0.113.9',
    'X-Forwarded-For':'198.51.100.7',
    'User-Agent':'ALPHA-TASK20-CF-IP-PROBE-20261108-DO-NOT-REPLY'
  },
  allowGatewayReject:true
},403,'CONSENT_REQUIRED');
await check('bad-audience-denied',{body:{...request,audience:'admin'}},422,'INVALID_AUDIENCE');
await check('get-method-denied',{method:'GET'},405,'Method not allowed');
// Never submit a fully valid inference request in recurring CI: once enabled,
// it could invoke the paid provider. Invalid syntax is rejected before gates.
await check('oversize-message-denied',{body:{...request,message:'X'.repeat(401)}},422,'INVALID_MESSAGE');
await check('invalid-session-denied',{body:{...request,request_key:'invalid-id'}},422,'INVALID_SESSION');
console.log(`TASK20_HOSTED_PUBLIC_FAILCLOSED_PASS checks=${checks} no_secrets no_paid_calls`);
