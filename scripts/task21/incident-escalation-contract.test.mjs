import assert from "node:assert/strict";
import {prepareInternalEscalation,assessEscalationAcceptance} from "./incident-escalation-contract.mjs";
const sample={
 alert_id:"11111111-1111-4111-8111-111111111111",
 case_id:"22222222-2222-4222-8222-222222222222",
 category:"security_incident",severity:"CRITICAL",
 assigned_role:"SECURITY_REVIEW",delivery_state:"UNROUTED",
 created_at:"2026-11-08T03:00:00+00:00",
 full_name:"SECRET FAKE NAME",email:"private@example.invalid",
 description:"PRIVATE CUSTOMER DETAILS",authorization:"Bearer fake-secret"
};
const v=prepareInternalEscalation(sample);
assert.equal(v.schema,"agm.task21.internal-alert.v1");
assert.equal(v.environment,"staging");
assert.equal(v.synthetic,true);
assert.equal(v.case_id,sample.case_id);
assert.equal(v.assigned_role,"SECURITY_REVIEW");
assert.equal(v.created_at,"2026-11-08T03:00:00.000Z");
assert.equal(Object.isFrozen(v),true);
for(const secret of ["SECRET FAKE NAME","private@example.invalid",
 "PRIVATE CUSTOMER DETAILS","Bearer fake-secret"])
 assert.ok(!JSON.stringify(v).includes(secret));
const routes=[
 ["billing","BILLING_REVIEW"],["refund","BILLING_REVIEW"],
 ["failed_campaign","BILLING_REVIEW"],["privacy","PRIVACY_REVIEW"]
];
for(const [category,assigned_role] of routes)
 assert.equal(prepareInternalEscalation({...sample,category,assigned_role,severity:"HIGH"}).assigned_role,assigned_role);
for(const patch of [
 {delivery_state:"DELIVERED"},{delivery_state:"ACKNOWLEDGED"},
 {category:"general",severity:"NORMAL",assigned_role:"GENERAL_SUPPORT"},
 {category:"security_incident",severity:"HIGH"},
 {category:"privacy",severity:"CRITICAL",assigned_role:"PRIVACY_REVIEW"},
 {category:"billing",severity:"HIGH",assigned_role:"SECURITY_REVIEW"},
 {case_id:"wrong"},{alert_id:"wrong"},{created_at:"not a date"},
 {created_at:"2026-11-08T04:00:00"}
])assert.throws(()=>prepareInternalEscalation({...sample,...patch}),/TASK21_ALERT_INVALID/);
for(const invalid of [null,[],"test",{},1])
 assert.throws(()=>prepareInternalEscalation(invalid),/TASK21_ALERT_INVALID/);
const notReady=assessEscalationAcceptance({
 approved_internal_channel:true,distinct_human_ack_receipt:true,
 verified_primary_and_backup:false
});
assert.equal(notReady.decision,"NO_GO");
for(const key of ["app_generated_delivery_receipt","verified_primary_and_backup",
 "highlevel_sandbox_acceptance","owner_policy_approval"])
 assert.ok(notReady.blocked.some(g=>g.key===key));
assert.equal(Object.isFrozen(notReady),true);
const keyList=["approved_internal_channel","app_generated_delivery_receipt",
 "distinct_human_ack_receipt","verified_primary_and_backup",
 "documented_out_of_office_escalation","owner_policy_approval",
 "highlevel_sandbox_acceptance","production_release_review"];
assert.equal(assessEscalationAcceptance(Object.fromEntries(keyList.map(k=>[k,true]))).decision,
 "EVIDENCE_READY_FOR_OWNER_REVIEW");
assert.equal(assessEscalationAcceptance(Object.fromEntries(keyList.map(k=>[k,"true"]))).decision,"NO_GO");
assert.throws(()=>assessEscalationAcceptance(null),/TASK21_EVIDENCE_REQUIRED/);
console.log("TASK21_ESCALATION_CONTRACT_SOURCE_ACCEPTANCE_PASS");
