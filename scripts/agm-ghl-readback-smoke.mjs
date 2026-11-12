import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { emailSuppressionStatus, reconcileContact } from "../supabase/functions/alpha-ghl-readback/logic.mjs";

const locationId = "aBcD1234EFgh5678IJkl";
const request = {
  email: "buyer@example.invalid", notice_version: "AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1",
  contact_consent: true, marketing_opt_in: true, contact_consent_version: "AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1",
  contact_consent_at: "2026-11-08T12:00:00Z", business_identity_verified: true,
  decision_authority_verified: true, followup_gate: "HOLD", followup_approved_at: null
};
const delivery = { remote_contact_id: "mock_contact_123" };
const contact = {
  id: delivery.remote_contact_id, locationId, email: "BUYER@example.invalid", dnd: false,
  dndSettings: { Email: { status: "inactive" } }, inboundDndSettings: { all: { status: "inactive" } }
};
assert.equal(emailSuppressionStatus(contact), "CLEAR");
assert.equal(reconcileContact({request,delivery,contacts:[contact],providerTotal:1,internalDuplicateCount:1,locationId}).classification,
  "HOLD_EXPLICIT_APPROVAL_REQUIRED");
assert.equal(reconcileContact({request,delivery,contacts:[contact, {...contact,id:"duplicate"}],providerTotal:2,internalDuplicateCount:1,locationId}).classification,
  "HOLD_DUPLICATE_REVIEW");
assert.equal(reconcileContact({request:{...request,marketing_opt_in:false},delivery,contacts:[contact],providerTotal:1,internalDuplicateCount:1,locationId}).classification,
  "HOLD_MARKETING_OPTOUT");
assert.equal(reconcileContact({request:{...request,contact_consent:false},delivery,contacts:[contact],providerTotal:1,internalDuplicateCount:1,locationId}).classification,
  "HOLD_NO_CURRENT_INQUIRY_CONTACT_CONSENT");
assert.equal(reconcileContact({request,delivery,contacts:[{...contact,dnd:true}],providerTotal:1,internalDuplicateCount:1,locationId}).classification,
  "HOLD_CRM_SUPPRESSED");
assert.equal(reconcileContact({request,delivery,contacts:[{...contact,dndSettings:{Email:{status:"active"}}}],providerTotal:1,internalDuplicateCount:1,locationId}).classification,
  "HOLD_CRM_SUPPRESSED");
assert.equal(reconcileContact({request,delivery,contacts:[{...contact,id:"other"}],providerTotal:1,internalDuplicateCount:1,locationId}).classification,
  "HOLD_CRM_IDENTITY_MISMATCH");
assert.equal(reconcileContact({request:{...request,business_identity_verified:false},delivery,contacts:[contact],providerTotal:1,internalDuplicateCount:1,locationId}).classification,
  "HOLD_IDENTITY_OR_AUTHORITY_UNVERIFIED");
assert.equal(reconcileContact({request,delivery,contacts:[{...contact,dndSettings:undefined,inboundDndSettings:undefined}],providerTotal:1,internalDuplicateCount:1,locationId}).suppressionStatus,
  "CLEAR");

const [preflightSource,readbackSource]=await Promise.all([
  readFile(new URL("../supabase/functions/alpha-ghl-preflight/index.ts",import.meta.url),"utf8"),
  readFile(new URL("../supabase/functions/alpha-ghl-readback/index.ts",import.meta.url),"utf8")
]);
for(const source of [preflightSource,readbackSource]) {
  assert.ok(source.includes("req.headers.get(\"Authorization\") !== \"Bearer \" + adminKey"));
  assert.ok(!source.includes("Access-Control-Allow-Origin"));
  assert.ok(source.includes("SUPABASE_SERVICE_ROLE_KEY"));
  assert.ok(!source.includes("console.log(payload"));
}
assert.ok(readbackSource.includes("alpha_ghl_review_audit"));
assert.ok(!readbackSource.includes("followup_gate = \"CLEARED\""));
console.log("ALPHA CRM preflight/readback guards, suppression, consent, identity, duplicate and HOLD policy PASS");
