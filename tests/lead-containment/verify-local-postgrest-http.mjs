// CI-only real PostgREST HTTP acceptance against an empty localhost fixture.
// NEVER use a production Supabase origin or log response data/keys.
import assert from 'node:assert/strict';

const base = process.env.ALPHA_CI_POSTGREST_ORIGIN || 'http://127.0.0.1:3000';
if (base !== 'http://127.0.0.1:3000') {
  throw new Error('REFUSED: PostgREST must be the disposable local fixture');
}
const resources = [
  'advertiser_inquiries', 'contact_inquiries',
  'creator_applications', 'alpha_lead_summary'
];
async function fetchLocal(path) {
  return fetch(base + '/'+path+'?select=*&limit=0', {
    method: 'GET',
    redirect: 'manual',
    signal: AbortSignal.timeout(6000),
    headers: {accept:'application/json'}
  });
}
// Prove the test actually reaches PostgREST with a working anon database role.
const control = await fetchLocal('ci_public_control');
assert.ok(control.status >= 200 && control.status < 300,
  'Positive-control harmless empty table did not return HTTP 2xx');
for (const object of resources) {
  const res = await fetchLocal(object);
  assert.ok(res.status === 401 || res.status === 403,
    'FAIL: ' + object + ' returned unexpected HTTP ' + res.status);
  const payload = await res.json();
  assert.equal(payload.code, '42501',
    'FAIL: ' + object + ' did not return actual PostgREST 42501 denial');
  console.log('PASS: ' + object + ' HTTP ' + res.status + ' code 42501');
}
console.log('PASS: real PostgREST local HTTP 4/4 denied with positive control 2xx');
