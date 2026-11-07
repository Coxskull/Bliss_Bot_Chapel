import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
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
 assert.match(section,/href="\/join-advertiser.html"/);
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
