import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {inspect,deliverables} from './alpha346-manual-proposal-gate.mjs';
const p=JSON.parse(await readFile(new URL('./alpha346-synthetic-proposal.json',import.meta.url),'utf8'));
test('existing intake to CRM simulated handoff blocked without finance/terms/support',()=>{
 const r=inspect(p);
 assert.equal(r.deliverableCount,5); assert.deepEqual(p.deliverables,deliverables);
 assert.equal(r.validation,'BLOCKED_PENDING_BUSINESS_APPROVALS');
 assert.ok(r.missingApprovals.includes('approvedAmountAndCurrency'));
 assert.equal(r.customerContactPerformed,false); assert.equal(r.delivery,'NOT_SENT');
});
test('fail closed on contact, payment, STOP and real-customer simulation',()=>{
 for(const change of [{fixture:'LIVE'}, {globalStop:'RELEASED'}, {paymentEnabled:true}, {outreachEnabled:true}, {campaignReady:true},{customerAccepted:true},{delivery:'SENT'},{creatorAuthorization:'VERIFIED'},{inquiryReceipt:'b9ec2e4a-01a1-4ba1-86be-987aaaaa0000'}])assert.throws(()=>inspect({...p,...change}));
});
test('rejects changes to deliverables and CRM status',()=>{
 assert.throws(()=>inspect({...p,deliverables:['guaranteed_placements']}));
 assert.throws(()=>inspect({...p,crmStatus:'CUSTOMER_APPROVED'}));
});
test('even completed fictional approvals do not send, pay or change STOP',()=>{
 const a={...p,ownerApprovedScope:true,verifiedCustomerSupport:true,commercialTermsApproved:true,refundPolicyApproved:true,commercialPrivacyApproved:true,financeApprovedQuote:true,verifiedCustomerIdentity:true,approvedTimelineAndRevisions:true,price:{amountMinor:250000,currency:'PHP'},quoteVersion:1};
 const r=inspect(a);
 assert.equal(r.validation,'OFFLINE_READY_FOR_HUMAN_REVIEW_ONLY');
 assert.equal(r.paymentEnabled,false);assert.equal(r.globalStop,'ENGAGED');assert.equal(r.customerContactPerformed,false);
});
