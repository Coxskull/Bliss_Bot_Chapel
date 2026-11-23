// Task22: no PII, no consent mutation, no external CRM calls.
// Verify live Supabase user, AAL2 session and existing single-operator grant.
const ORIGIN="https://alphacreativecontent.com";
const UUID=/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const JWT=/^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/;
const KEYS=["inquiries","contact_consent_recorded","missing_current_contact_consent",
 "marketing_opted_in","marketing_opted_out","pending_operator_review",
 "locally_cleared","hold_count","reviewed_identity_and_authority","revocation_events"];
const respond=(status,json,allow=false)=>{
 const headers={"Content-Type":"application/json; charset=utf-8","Cache-Control":"no-store",
  "X-Content-Type-Options":"nosniff","Referrer-Policy":"no-referrer","Vary":"Origin"};
 if(allow)headers["Access-Control-Allow-Origin"]=ORIGIN;
 return new Response(JSON.stringify(json),{status,headers});
};
export async function handleConsentReadiness(req,{client}={}){
 if(req.headers.get("Origin")!==ORIGIN)return respond(403,{error:"Forbidden origin"});
 if(req.method==="OPTIONS")return new Response(null,{status:204,headers:{
   "Access-Control-Allow-Origin":ORIGIN,"Access-Control-Allow-Methods":"GET, OPTIONS",
   "Access-Control-Allow-Headers":"Authorization, apikey",
   "Access-Control-Max-Age":"300","Cache-Control":"no-store","Vary":"Origin"
 }});
 if(req.method!=="GET")return respond(405,{error:"Read-only GET required"},true);
 if(new URL(req.url).search)return respond(422,{error:"No customer filters supported"},true);
 const hdr=req.headers.get("Authorization")||"";
 if(!hdr.startsWith("Bearer ")||hdr.length>4100||!JWT.test(hdr.slice(7)))
  return respond(401,{error:"Verified staff login required"},true);
 if(!client?.auth||typeof client.rpc!=="function")return respond(503,{error:"Unavailable"},true);
 const jwt=hdr.slice(7);
 let user,claims;
 try{
  const [u,c]=await Promise.all([client.auth.getUser(jwt),client.auth.getClaims(jwt)]);
  if(u.error||c.error)throw Error("Not signed in");
  user=u.data?.user;claims=c.data?.claims;
 }catch{return respond(401,{error:"Sign-in rejected"},true);}
 if(!UUID.test(user?.id||"")||claims?.sub!==user.id||claims?.role!=="authenticated"||
    claims?.aal!=="aal2"||!UUID.test(claims?.session_id||""))
  return respond(403,{error:"Verified MFA session required"},true);
 try{
  const grant=await client.rpc("alpha_support_manual_operator_authorized",{p_actor_user_id:user.id});
  if(grant.error||grant.data!==true)
   return respond(403,{error:"Operator grant required"},true);
  const totals=await client.rpc("alpha_ghl_consent_readiness_summary");
  if(totals.error||!totals.data||typeof totals.data!=="object"||Array.isArray(totals.data))
   return respond(503,{error:"Consent summary unavailable"},true);
  const safe={};
  for(const key of KEYS){
   const n=totals.data[key];
   if(!Number.isSafeInteger(n)||n<0||n>10000000)
    return respond(503,{error:"Invalid consent summary"},true);
   safe[key]=n;
  }
  if(safe.inquiries!==safe.hold_count+safe.locally_cleared ||
     safe.inquiries!==safe.marketing_opted_in+safe.marketing_opted_out ||
     safe.inquiries!==safe.contact_consent_recorded+safe.missing_current_contact_consent)
   return respond(503,{error:"Consent totals inconsistent"},true);
  return respond(200,{status:"READ_ONLY_LOCAL_READINESS",...safe,
    followup_permission:"HOLD — NEVER RELEASE FROM THIS REPORT",
    message:"Only independently verified current consent and operator authorization can justify separate future approval. This report does not contact customers or change HighLevel."},true);
 }catch{return respond(503,{error:"Consent readiness unavailable"},true);}
}
