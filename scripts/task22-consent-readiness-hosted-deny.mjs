// Requires deployed production endpoint to reject unsigned requests.
// Never passes staff credentials or accesses customer data.
import assert from "node:assert/strict";
const url="https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-ghl-consent-readiness";
const res=await fetch(url,{method:"GET",redirect:"error",headers:{
 Origin:"https://alphacreativecontent.com",
 "User-Agent":"ALPHA-TASK22-LOCAL-CONSENT-UNSIGNED-NEGATIVE-20261112"
},signal:AbortSignal.timeout(18000)});
const body=await res.text();
assert.ok([401,403].includes(res.status),"Unsigned access must be denied; HTTP "+res.status);
assert.ok(body.length<2500,"No client data in unsigned response");
for(const word of ["contact_consent_recorded","revocation_events","missing_current_contact_consent"]){
 assert.ok(!body.includes(word),"Anonymous caller saw protected aggregate");
}
console.log("TASK22_HOSTED_CONSENT_ANON_DENIED_PASS status="+res.status);
