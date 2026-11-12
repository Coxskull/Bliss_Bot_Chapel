// Task22 read-only CRM reconciliation through a verified human support operator.
// This helper NEVER returns customer PII, contact IDs, provider headers or tokens.
// Edge gateway must use verify_jwt=true. Credential remains server-side.
const ORIGIN="https://alphacreativecontent.com";
const JWT=/^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/;
const UUID=/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const out=(status,obj,allow=false)=>{
 const headers={"Content-Type":"application/json; charset=utf-8","Cache-Control":"no-store",
  "X-Content-Type-Options":"nosniff","Referrer-Policy":"no-referrer","Vary":"Origin"};
 if(allow)headers["Access-Control-Allow-Origin"]=ORIGIN;
 return new Response(JSON.stringify(obj),{status,headers});
};
function tally(results){
 const suppression={SUPPRESSED:0,CLEAR:0,UNKNOWN:0};
 const classifications={};
 let matched=0;
 for(const r of results){
  const s=["SUPPRESSED","CLEAR"].includes(r.suppressionStatus)?r.suppressionStatus:"UNKNOWN";
  suppression[s]++;
  const c=typeof r.classification==="string"&&/^HOLD_[A-Z_]+$/.test(r.classification)
   ?r.classification:"UNVERIFIED";
  classifications[c]=(classifications[c]||0)+1;
  if(r.deliveredContactMatch===true&&r.emailIdentityMatch===true)matched++;
 }
 return {suppression,classifications,provider_identity_matches:matched};
}
export async function handleCrmStaffReadback(req,{client,fetchReconcile,now=Date.now}){
 if(req.headers.get("Origin")!==ORIGIN)return out(403,{error:"Forbidden origin"});
 if(req.method==="OPTIONS")return new Response(null,{status:204,headers:{
  "Access-Control-Allow-Origin":ORIGIN,"Access-Control-Allow-Methods":"POST, OPTIONS",
  "Access-Control-Allow-Headers":"Authorization, apikey, Content-Type",
  "Access-Control-Max-Age":"300","Cache-Control":"no-store","Vary":"Origin"
 }});
 if(req.method!=="POST")return out(405,{error:"Read-only POST required"},true);
 if(req.headers.get("Content-Type")!=="application/json")
  return out(415,{error:"JSON request required"},true);
 const authorization=req.headers.get("Authorization")||"";
 if(!authorization.startsWith("Bearer ")||authorization.length>4100||!JWT.test(authorization.slice(7)))
  return out(401,{error:"Verified staff login required"},true);
 if(!client||!client.auth||typeof client.rpc!=="function"||typeof fetchReconcile!=="function")
  return out(503,{error:"Readback not configured"},true);
 let raw;
 try{raw=await req.text();}catch{return out(400,{error:"Invalid request"},true);}
 if(raw!=="{}"&&raw.trim()!=="{}")return out(422,{error:"Only empty review request allowed"},true);
 const jwt=authorization.slice(7);
 let user,claims;
 try{
  const [u,c]=await Promise.all([client.auth.getUser(jwt),client.auth.getClaims(jwt)]);
  if(u.error||c.error)throw Error("bad");
  user=u.data?.user;claims=c.data?.claims;
 }catch{return out(401,{error:"Staff login rejected"},true);}
 if(!UUID.test(user?.id||"")||claims?.sub!==user.id||claims.role!=="authenticated"||
    claims.aal!=="aal2"||!claims.session_id)
  return out(403,{error:"Fresh MFA session required"},true);
 try{
  const g=await client.rpc("alpha_support_manual_operator_authorized",{p_actor_user_id:user.id});
  if(g.error||g.data!==true)return out(403,{error:"Approved operator grant required"},true);
  // Skip repeat provider reads for 15 minutes. The canonical readback records
  // each provider audit row, so this is shared across different Edge isolates.
  const latest=await client.from("alpha_ghl_review_audit").select("checked_at")
    .order("checked_at",{ascending:false}).limit(1);
  if(latest.error)return out(503,{error:"Audit not available"},true);
  const timestamp=Date.parse(latest.data?.[0]?.checked_at||"");
  if(Number.isFinite(timestamp)&&now()-timestamp<15*60_000)
   return out(429,{error:"Readback recently recorded; review existing audit before retrying"},true);
 }catch{return out(503,{error:"Operator review prerequisites unavailable"},true);}
 let result;
 try{result=await fetchReconcile();}catch{return out(503,{error:"Remote provider readback unavailable"},true);}
 if(result?.ok!==true||!Array.isArray(result?.data?.results)||
    result.data.results.length>20||
    result.data.results.length!==result.data.processed)
  return out(503,{error:"Remote CRM verification could not be accepted"},true);
 const summary=tally(result.data.results);
 return out(200,{
  checked_at:new Date(now()).toISOString(),
  processed:result.data.processed,
  provider_status:"READBACK_RECORDED",
  ...summary,
  followup_permission:"HOLD — NO AUTOMATED OUTREACH APPROVED",
  note:"Read-only HighLevel contact reconciliation. This does not set DND, remove contacts or approve contact."
 },true);
}
