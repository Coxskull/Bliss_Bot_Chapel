import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {runInNewContext} from 'node:vm';
import {URLSearchParams} from 'node:url';
const get=async path=>readFile(new URL(path,import.meta.url),'utf8');
const services=await get('../frontend/public/services.html');
const approval=await get('../docs/commercial/ALPHA-346-PLANNING-SERVICE-APPROVAL-20261107.md');
const signoff=await get('../docs/commercial/ALPHA-346-COMMERCIAL-RELEASE-SIGNOFF.md');
const builder=await get('./build-vercel-portal.mjs');
test('public service has exactly five planning outputs and existing request form',()=>{
 const start=services.indexOf('<h2>Campaign Planning &amp; Coordination');
 const end=services.indexOf('<h2>How a commercial engagement',start);
 assert.ok(start>=0 && end>start);
 const section=services.slice(start,end);
 assert.equal((section.match(/<li>/g)||[]).length,5);
 for(const x of ['Campaign discovery brief','Creator/channel suitability plan','Creative direction outline','Coordination schedule','Final planning summary'])assert.ok(section.includes(x));
 assert.ok(section.includes('href="/join-advertiser.html?service=planning"'));
 assert.match(section,/creates no order or payment obligation/);
 assert.match(services,/No online checkout is enabled/);
});
test('approval is limited to internal preparation and individualized quote procedure',()=>{
 assert.match(approval,/APPROVED_TO_PREPARE_MANUAL_PLANNING_SERVICE_AND_CUSTOM_QUOTES/);
 assert.match(approval,/USD \$50–\$1,000/);
 assert.match(approval,/approved price for any identified customer/);
 assert.match(signoff,/INTERNAL USD \$50–\$1,000 REFERENCE RANGE APPROVED/);
 assert.match(approval,/Production payment release:\*\* `DENIED`/);
 assert.match(approval,/Global STOP:\*\* `ENGAGED`/);
 assert.match(approval,/does \*\*not silently resolve\*\*/);
 assert.match(signoff,/CUSTOM-QUOTE METHOD APPROVED FOR PREPARATION; ACTUAL PRICE, CURRENCY AND PAYMENT NOT APPROVED/);
});
test('private draft proposal desk is absent from public build and no checkout is added',()=>{
 assert.ok(!builder.includes('alpha346-proposal-desk.html'));
 assert.ok(!services.includes('stripe.com/checkout'));
 assert.ok(!services.includes('type="payment"'));
});

test('launch planning URL prefills only fixed customer-visible service text',async()=>{
 const script=await get('../frontend/public/alpha-intake.js');
 for(const [kind,search,before,prefill] of [
  ['advertiser','?service=planning','',true],
  ['advertiser','?service=other','',false],
  ['creator','?service=planning','',false],
  ['advertiser','?service=planning','Existing customer text',false],
  ['advertiser','?service=planning%3Cscript%3E','',false]
 ]){
  const description={value:before};
  const consent={checked:false},marketing={checked:false};
  const events=[];
  const form={dataset:{kind},elements:{namedItem(n){
    return n==='description'?description:
      n==='contact_consent'?consent:
      n==='marketing_opt_in'?marketing:null;
  }},addEventListener(event){events.push(event);}};
  const document={querySelector(sel){
    return sel==='#alpha-intake-form'?form:
      sel==='#form-status'?{hidden:true}:
      sel==='#submit-button'?{textContent:'Submit'}:null;
  }};
  runInNewContext(script,{document,window:{location:{search}},URLSearchParams});
  assert.deepEqual(events,['input','submit']);
  assert.equal(description.value.startsWith('Please send me a Campaign Planning & Coordination planning-only proposal.'),prefill);
  if(!prefill)assert.equal(description.value,before);
  assert.equal(consent.checked,false);
  assert.equal(marketing.checked,false);
 }
});


test('Starter Package inquiry prefills only the selected fixed topic',async()=>{
 const script=await get('../frontend/public/alpha-intake.js');
 assert.match(services,/Digital Advertising Starter Package — \$149 USD/);
 assert.ok(services.includes('href="/join-advertiser.html?service=starter"'));
 assert.doesNotMatch(services,/buy\.stripe\.com/);
 for(const [kind,search,before,expected] of [
 ['advertiser','?service=starter','','Please send me a Digital Advertising Starter Package'],
 ['advertiser','?service=starter','Customer description','Customer description'],
 ['creator','?service=starter','',''],
 ['advertiser','?service=starter%3Cscript%3E','','']]){
 const description={value:before}, consent={checked:false},marketing={checked:false},events=[];
 const form={dataset:{kind},elements:{namedItem(n){return ({description,contact_consent:consent,marketing_opt_in:marketing})[n]??null;}},addEventListener(n){events.push(n);}};
 const document={querySelector(s){return s==='#alpha-intake-form'?form:s==='#form-status'?{}:s==='#submit-button'?{}:null;}};
 runInNewContext(script,{document,window:{location:{search}},URLSearchParams});
 if(expected.startsWith('Please')){assert.ok(description.value.startsWith(expected));assert.ok(description.value.includes('creates no order or payment obligation'));}else assert.equal(description.value,expected);
 assert.equal(consent.checked,false);assert.equal(marketing.checked,false);assert.deepEqual(events,['input','submit']);
 }
});
