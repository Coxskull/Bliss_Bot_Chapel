// Task21 production manual-mail operator API: ONLY private metadata, one case claim.
// A valid JWT is necessary but insufficient: independent Supabase Auth user
// lookup, verified aal2 MFA, exact Origin, and a private enabled operator grant.
// No emails, refunds, resolution, arbitrary text or customer PII are exposed.
const ORIGIN="https://alphacreativecontent.com";
const UUID=/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const JWT=/^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/;
const baseHeaders={
 "Content-Type":"application/json; charset=utf-8",
 "Cache-Control":"no-store",
 "X-Content-Type-Options":"nosniff",
 "Referrer-Policy":"no-referrer",
 "Vary":"Origin"
};
function reply(status,value,allow=false){
 const headers=new Headers(baseHeaders);
 if(allow)headers.set("Access-Control-Allow-Origin",ORIGIN);
 return new Response(JSON.stringify(value),{status,headers});
}
export async function handleManualOperatorRequest(req,{client}){
 const origin=req.headers.get("Origin");
 if(origin!==ORIGIN)return reply(403,{error:"Forbidden origin"});
 if(req.method==="OPTIONS")return new Response(null,{status:204,headers:{
   "Access-Control-Allow-Origin":ORIGIN,
   "Access-Control-Allow-Methods":"GET, POST, OPTIONS",
   "Access-Control-Allow-Headers":"Authorization, apikey, Content-Type",
   "Access-Control-Max-Age":"300","Cache-Control":"no-store","Vary":"Origin"
 }});
 if(!["GET","POST"].includes(req.method))return reply(405,{error:"Method not allowed"},true);
 if(!client||!client.auth||typeof client.rpc!=="function")
  return reply(503,{error:"Operator service unavailable"},true);

 const raw=req.headers.get("Authorization")||"";
 if(!raw.startsWith("Bearer ")||raw.length>4100)return reply(401,{error:"Human login required"},true);
 const token=raw.slice(7);
 if(!JWT.test(token))return reply(401,{error:"Human login required"},true);
 let user,claims;
 try{
  const [a,b]=await Promise.all([
   client.auth.getUser(token),
   client.auth.getClaims(token)
  ]);
  if(a.error||b.error)throw Error("auth");
  user=a.data?.user;claims=b.data?.claims;
 }catch{return reply(401,{error:"Human login required"},true);}
 if(!user?.id||claims?.sub!==user.id||claims.role!=="authenticated"||
    claims.aal!=="aal2"||!UUID.test(user.id))
  return reply(403,{error:"Verified staff MFA required"},true);
 let grant;
 try{grant=await client.rpc("alpha_support_manual_operator_authorized",{
   p_actor_user_id:user.id
 });}catch{return reply(503,{error:"Operator authorization unavailable"},true);}
 if(grant.error||grant.data!==true)
  return reply(403,{error:"Operator permission denied"},true);

 if(req.method==="GET"){
  let page;
  try{
   const u=new URL(req.url);const rawPage=u.searchParams.get("page")??"0";
   if(!/^(0|[1-9][0-9]{0,2})$/.test(rawPage)||Number(rawPage)>100||
       [...u.searchParams.keys()].some(x=>x!=="page"))throw Error("page");
   page=Number(rawPage);
  }catch{return reply(400,{error:"Invalid queue page"},true);}
  let q;
  try{q=await client.rpc("alpha_support_manual_operator_queue",{
   p_actor_user_id:user.id,p_page:page
  });}catch{return reply(503,{error:"Case queue unavailable"},true);}
  if(q.error||!Array.isArray(q.data))return reply(503,{error:"Case queue unavailable"},true);
  // Database RPC explicitly restricts the output to coarse case metadata.
  const cases=q.data.map(c=>({
   case_id:c.case_id,category:c.category,status:c.status,
   observed_at:c.observed_at,created_at:c.created_at,
   claimed_by_me:c.claimed_by_me===true
  }));
  return reply(200,{cases,page,page_size:20},true);
 }
 if(!(req.headers.get("Content-Type")||"").toLowerCase().startsWith("application/json"))
  return reply(415,{error:"JSON required"},true);
 if(Number(req.headers.get("Content-Length")||"0")>500)
  return reply(413,{error:"Request too large"},true);
 let body;
 try{
  const rawBody=await req.text();
  if(rawBody.length>500)throw Error("size");
  body=JSON.parse(rawBody);
 }catch{return reply(422,{error:"Invalid case operation"},true);}
 if(!body||Array.isArray(body)||typeof body!=="object"||
     body.action!=="CLAIM"||!UUID.test(body.case_id)||
     Object.keys(body).sort().join(",")!=="action,case_id")
  return reply(422,{error:"Invalid case operation"},true);
 let saved;
 try{saved=await client.rpc("alpha_support_manual_operator_claim",{
  p_actor_user_id:user.id,p_case_id:body.case_id
 });}catch{return reply(503,{error:"Case claim unavailable"},true);}
 if(saved.error)return reply(503,{error:"Case claim unavailable"},true);
 if(saved.data!==true)return reply(409,{error:"Case unavailable or already claimed"},true);
 return reply(200,{case_id:body.case_id,status:"IN_REVIEW",action:"CLAIM",committed:true},true);
}
