import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
const load=async path=>readFile(new URL(path,import.meta.url),'utf8');
const desk=await load('../tools/commercial/alpha346-proposal-desk.html');
const contact=await load('../frontend/public/contact.html');
const publicBuilder=await load('./build-vercel-portal.mjs');

test('ALPHA346 internal proposal desk blocks network and third-party code',()=>{
  assert.match(desk,/connect-src 'none'/);
  assert.match(desk,/default-src 'none'/);
  for(const source of ['fetch(', 'XMLHttpRequest', 'WebSocket', 'sendBeacon(', 'localStorage','sessionStorage','src="https:', 'action="https:'])
    assert.ok(!desk.includes(source),'forbidden token '+source);
  assert.match(desk,/form-action 'none'/);
  assert.match(desk,/No entries are transmitted or saved to a server/);
});

test('proposal editor enforces internal review watermark, fixed five planning deliverables and zero payment',()=>{
  const ids=['campaign discovery brief','creator and channel suitability plan','creative direction outline','milestone and coordination schedule','final planning summary'];
  for(const phrase of ids) assert.ok(desk.toLowerCase().includes(phrase));
  assert.equal((desk.match(/data-approval="/g)||[]).length,8);
  for(const phrase of ['INTERNAL REVIEW DRAFT','NOT A CUSTOMER OFFER','GLOBAL STOP ENGAGED','PAYMENT DISABLED','DELIVERY NOT SENT'])
    assert.ok(desk.includes(phrase));
  assert.ok(desk.includes('.textContent=value'));
  assert.ok(!desk.includes('.innerHTML='));
});

test('owner-selected Gmail inbox is shown as inquiry-only, not approved post-purchase support',()=>{
  assert.match(contact,/mailto:dejesuserwin1@gmail.com/);
  assert.match(contact,/paid-customer support service and response time are not yet verified/);
  assert.match(desk,/dejesuserwin1@gmail.com/);
  assert.match(desk,/VERIFY MONITORING BEFORE PAID ORDERS/);
});

test('internal proposal desk is NEVER copied into public Vercel allowlist',()=>{
  assert.ok(!publicBuilder.includes('alpha346-proposal-desk.html'));
  assert.ok(!publicBuilder.includes('tools/commercial/'));
  assert.match(contact,/Online paid orders are <strong>not currently available/);
});
