import {
  isUuid, parseOperatorRoster, rolesForUser, mayAssign, parseTransition
} from "./operator-contract.mjs";

// A privileged Supabase client is injected ONLY from the Edge runtime; never
// called from the browser or exposed via logs. Guard public access first.
const basic = {
  "Content-Type":"application/json; charset=utf-8",
  "Cache-Control":"no-store",
  "X-Content-Type-Options":"nosniff",
  "Referrer-Policy":"no-referrer"
};
const response = (payload,status=200,origin=null) => {
  const headers = new Headers(basic);
  if (origin) {
    headers.set("Access-Control-Allow-Origin",origin);
    headers.set("Vary","Origin");
  }
  return new Response(JSON.stringify(payload),{status,headers});
};
export async function handleOperatorRequest(req,db,env) {
  const origin = req.headers.get("Origin");
  const trusted = env.get("ALPHA_SUPPORT_OPERATOR_ORIGIN");
  // Staging-only fallback when the Edge secret-manager cannot set a roster.
  // Roles/origins remain privileged DB-owned records, not JWT user metadata,
  // source-code UUIDs, public CORS wildcards or browser-provided claims.
  const stagingDbFallback = !trusted &&
    env.get("SUPABASE_URL")==="https://wdlmcriajgsretgyusmo.supabase.co";
  let allowOrigin = null;
  if (trusted) {
    if (origin && origin!==trusted)
      return response({error:"Forbidden origin"},403);
    allowOrigin=origin||null;
  } else if (stagingDbFallback && origin) {
    let allowed;
    try {
      allowed=await db.rpc("alpha_support_staging_origin_allowed",{
        p_origin:origin
      });
    } catch { return response({error:"Forbidden origin"},403); }
    if (allowed.error || allowed.data!==true)
      return response({error:"Forbidden origin"},403);
    allowOrigin=origin;
  } else if (origin) {
    return response({error:"Forbidden origin"},403);
  }
  if (req.method==="OPTIONS") {
    if (!allowOrigin) return response({error:"Forbidden origin"},403);
    return new Response(null,{status:204,headers:{
      "Access-Control-Allow-Origin":allowOrigin,
      "Access-Control-Allow-Methods":"GET, POST, OPTIONS",
      "Access-Control-Allow-Headers":"Authorization, apikey, Content-Type",
      "Access-Control-Max-Age":"300","Vary":"Origin"
    }});
  }
  if (!["GET","POST"].includes(req.method))
    return response({error:"Method not allowed"},405,allowOrigin);
  const bearer = /^Bearer ([A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+)$/.exec(
    req.headers.get("Authorization") || "");
  if (!bearer || bearer[1].length>4096)
    return response({error:"Authenticated user token required"},401,allowOrigin);
  let roster, useStagingDbGrants=false;
  const configuredRoster=env.get("ALPHA_SUPPORT_OPERATOR_ROSTER");
  if (configuredRoster) {
    try { roster=parseOperatorRoster(configuredRoster); }
    catch { return response({error:"Operator access not configured"},503,allowOrigin); }
  } else if (stagingDbFallback && allowOrigin) {
    useStagingDbGrants=true;
  } else {
    return response({error:"Operator access not configured"},503,allowOrigin);
  }

  // Both an Auth-server user lookup and cryptographically checked claims are
  // required. A valid generic user login does NOT grant an operator role.
  let user,claims;
  try {
    const u=await db.auth.getUser(bearer[1]);
    const c=await db.auth.getClaims(bearer[1]);
    if (u.error || c.error) throw Error("Auth denied");
    user=u.data?.user; claims=c.data?.claims;
  } catch { return response({error:"Unauthenticated"},401,allowOrigin); }
  if (!user?.id || claims?.sub!==user.id || claims?.role!=="authenticated")
    return response({error:"Unauthenticated"},401,allowOrigin);
  let roles;
  if (useStagingDbGrants) {
    let result;
    try {
      result=await db.rpc("alpha_support_staging_roles",{
        p_user_id:user.id,p_origin:allowOrigin,p_aal:claims.aal
      });
    } catch { return response({error:"Operator access not configured"},503,allowOrigin); }
    if (result.error || !Array.isArray(result.data) ||
        !result.data.every(role=>role==="GENERAL_SUPPORT"))
      return response({error:"Operator access not configured"},503,allowOrigin);
    roles=result.data;
    // This derived per-request roster contains only the verified actor's
    // authorized staging role. No escalation or cross-user assignment.
    roster=Object.create(null);
    if (roles.includes("GENERAL_SUPPORT")) roster.GENERAL_SUPPORT=[user.id];
  } else {
    roles=rolesForUser(roster,user.id,claims.aal);
  }
  if (!roles.length) return response({error:"Operator permission denied"},403,allowOrigin);
  const fields="case_id,request_id,category,severity,assigned_role,owner_user_id,status,resolution_summary,created_at,updated_at,resolved_at";
  if (req.method==="GET") {
    let url;
    try { url=new URL(req.url); } catch { return response({error:"Bad URL"},400,allowOrigin); }
    const id=url.searchParams.get("case_id");
    if (id!==null) {
      if (!isUuid(id)) return response({error:"Invalid case reference"},400,allowOrigin);
      const found=await db.from("alpha_support_cases")
        .select(fields).eq("case_id",id).maybeSingle();
      if (found.error) return response({error:"Case lookup unavailable"},503,allowOrigin);
      const ticket=found.data;
      // Unknown and unauthorized cases are deliberately indistinguishable.
      if (!ticket || !roles.includes(ticket.assigned_role))
        return response({error:"Case not found"},404,allowOrigin);
      const [intake,events]=await Promise.all([
        db.from("alpha_public_intake_requests")
          .select("full_name,email,description,created_at").eq("id",ticket.request_id).maybeSingle(),
        db.from("alpha_support_case_events")
          .select("event_id,action,status,owner_user_id,actor_user_id,summary,occurred_at")
          .eq("case_id",id).order("event_id",{ascending:false}).limit(100)
      ]);
      if (intake.error || !intake.data || events.error)
        return response({error:"Case history unavailable"},503,allowOrigin);
      return response({case:ticket,contact:intake.data,events:events.data||[],
        history_limit:100},200,allowOrigin);
    }
    const pageRaw=url.searchParams.get("page")||"0";
    if (!/^(0|[1-9][0-9]{0,2})$/.test(pageRaw) || Number(pageRaw)>100)
      return response({error:"Invalid queue page"},400,allowOrigin);
    const page=Number(pageRaw);
    const listed=await db.from("alpha_support_cases").select(fields)
      .in("assigned_role",roles).in("status",["OPEN","TRIAGED","IN_REVIEW","AWAITING_CUSTOMER"])
      // Lexical severity order is CRITICAL, HIGH, NORMAL; oldest first within each band.
      .order("severity",{ascending:true}).order("created_at",{ascending:true})
      .range(page*20,page*20+19);
    if (listed.error) return response({error:"Operator queue unavailable"},503,allowOrigin);
    return response({cases:listed.data||[],page,page_size:20,
      visible_roles:roles,operator_user_id:user.id},200,allowOrigin);
  }
  // Read-only by default, even with a valid operator login.
  if (env.get("ALPHA_SUPPORT_MUTATIONS_ENABLED")!=="true")
    return response({error:"Support case updates disabled"},423,allowOrigin);
  if (!(req.headers.get("Content-Type")||"").toLowerCase().startsWith("application/json")
      || Number(req.headers.get("Content-Length")||"0")>2400)
    return response({error:"Invalid request content"},415,allowOrigin);
  let transition, claimCaseId=null;
  try {
    const body=await req.text();
    if (body.length>2200) throw Error("Large payload");
    const payload=JSON.parse(body);
    if (payload?.action==="CLAIM") {
      if (!isUuid(payload.case_id) ||
          Object.keys(payload).sort().join(",")!=="action,case_id")
        throw Error("Invalid claim");
      claimCaseId=payload.case_id;
    } else transition=parseTransition(payload);
  } catch { return response({error:"Invalid case operation"},422,allowOrigin); }
  const found=await db.from("alpha_support_cases")
    .select("case_id,assigned_role").eq("case_id",
      claimCaseId||transition.case_id).maybeSingle();
  if (found.error) return response({error:"Case lookup unavailable"},503,allowOrigin);
  if (!found.data || !roles.includes(found.data.assigned_role))
    return response({error:"Case not found"},404,allowOrigin);
  if (claimCaseId) {
    const result=await db.rpc("alpha_support_claim",{
      p_case_id:claimCaseId,p_actor_user_id:user.id
    });
    if (result.error || result.data!==true)
      return response({error:"Case unavailable or already claimed"},409,allowOrigin);
    return response({committed:true,case_id:claimCaseId,status:"TRIAGED",
      action:"CLAIM"},200,allowOrigin);
  }
  if (transition.owner_user_id!==user.id ||
      !mayAssign(roster,found.data.assigned_role,transition.owner_user_id))
    return response({error:"Only the assigned operator may update this case"},403,allowOrigin);
  const saved=await db.rpc("alpha_support_transition",{
    p_case_id:transition.case_id,
    p_status:transition.status,
    p_owner_user_id:transition.owner_user_id,
    p_resolution:transition.resolution,
    p_actor_user_id:user.id
  });
  if (saved.error || saved.data!==true)
    return response({error:"Transition was not committed"},409,allowOrigin);
  return response({committed:true,case_id:transition.case_id,status:transition.status},200,allowOrigin);
}
