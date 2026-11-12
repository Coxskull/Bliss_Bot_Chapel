// Task22 — synthetic, strictly offline. Never call HighLevel or touch contacts.
import assert from "node:assert/strict";
import {emailSuppressionStatus,reconcileContact} from "../supabase/functions/alpha-ghl-readback/logic.mjs";
const sample=[
  ["lowercase email inactive",{dnd:false,dndSettings:{email:{status:"inactive"}}},"CLEAR"],
  ["lowercase email active",{dnd:false,dndSettings:{email:{status:"active"}}},"SUPPRESSED"],
  ["lowercase email permanent",{dnd:false,dndSettings:{email:{status:"permanent"}}},"SUPPRESSED"],
  ["uppercase Email inactive",{dnd:false,dndSettings:{Email:{status:"inactive"}}},"CLEAR"],
  ["uppercase Email permanent",{dnd:false,dndSettings:{Email:{status:"permanent"}}},"SUPPRESSED"],
  ["case mixed",{dndSettings:{email:{status:"InAcTiVe"}}},"CLEAR"],
  ["true global DND takes priority",{dnd:true,dndSettings:{email:{status:"inactive"}}},"SUPPRESSED"],
  ["global false without email status not clear",{dnd:false},"UNKNOWN"],
  ["inbound SMS/calls active does not mean outbound email",{dnd:false,inboundDndSettings:{all:{status:"active"}}},"UNKNOWN"],
  ["inbound active with explicit outbound inactive",{dnd:false,inboundDndSettings:{all:{status:"active"}},dndSettings:{email:{status:"inactive"}}},"CLEAR"],
  ["empty email settings not clear",{dnd:false,dndSettings:{}},"UNKNOWN"],
  ["unknown email status remains unknown",{dnd:false,dndSettings:{email:{status:"future-code"}}},"UNKNOWN"],
  ["mixed unsupported status fail closed",{dndSettings:{email:{status:"inactive"},Email:{status:"other"}}},"UNKNOWN"],
  ["conflicting active/inactive fail closed to suppressed",{dndSettings:{email:{status:"inactive"},Email:{status:"active"}}},"SUPPRESSED"],
  ["no contact null",null,"UNKNOWN"],
  ["no contact primitive","test","UNKNOWN"],
];
for(const [name,contact,expected] of sample){
  assert.equal(emailSuppressionStatus(contact),expected,name);
}
const id="TEST-PROVIDER-ID",location="TEST-LOCATION",email="synthetic@example.invalid";
const source={
  id:"synthetic-uuid",email,
  contact_consent:false,marketing_opt_in:true,
  notice_version:"AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1",
  followup_gate:"HOLD",business_identity_verified:false,
  decision_authority_verified:false
};
for(const status of ["active","inactive","permanent"]){
 const result=reconcileContact({
  request:source,
  delivery:{remote_contact_id:id},
  contacts:[{id,locationId:location,email,dndSettings:{email:{status}}}],
  providerTotal:1,internalDuplicateCount:1,locationId:location
 });
 assert.equal(result.deliveredContactMatch,true,"provider identity");
 assert.equal(result.emailIdentityMatch,true,"email identity");
 assert.equal(result.consentValid,false,"no current source consent");
 assert.equal(result.followupGate,"HOLD","follow-up must remain held");
 assert.equal(result.classification,"HOLD_NO_CURRENT_INQUIRY_CONTACT_CONSENT",
    "remote DND readback must never override missing current consent");
 assert.equal(result.suppressionStatus,status==="inactive"?"CLEAR":"SUPPRESSED");
}
console.log("TASK22_GHL_GET_EMAIL_DND_CASE_AND_PERMANENT_PASS",sample.length+3,"offline cases; no provider calls; current outreach HOLD");
