// Task22: production's existing staff credentials must be required by gateway.
// No bearer key or customer ID supplied; no provider readback possible.
import assert from "node:assert/strict";
const url="https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-ghl-operator-review";
const res=await fetch(url,{method:"POST",body:"{}",redirect:"error",headers:{
 "Origin":"https://alphacreativecontent.com","Content-Type":"application/json",
 "User-Agent":"ALPHA-TASK22-CRM-ANON-DENY-20261112"
},signal:AbortSignal.timeout(18000)});
const body=await res.text();
assert.ok([401,403].includes(res.status),
 "Operator-only CRM review must deny unsigned hosted POST, got HTTP "+res.status);
assert.ok(body.length<2000,"No provider data in unsigned response");
for(const s of ["remote_contact_id","suppression","provider_identity_matches","requestId"]){
 assert.ok(!body.includes(s),"Unsigned response exposed CRM data");
}
console.log("TASK22_LIVE_CRM_REVIEW_ANON_DENIED_PASS http="+res.status+" no_keys no_provider_data");
