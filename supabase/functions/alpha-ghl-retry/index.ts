import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2.95.0";
import { deliverToGhl } from "../alpha-public-intake/ghl-sync.mjs";

// Internal operations endpoint. JWT gateway guard + exact service-role
// credential check. Never grant a public browser origin or expose customer PII.
const result = (data: unknown, status=200) => new Response(JSON.stringify(data), {
  status, headers: {"Content-Type":"application/json","Cache-Control":"no-store",
    "X-Content-Type-Options":"nosniff"}
});
Deno.serve(async req => {
  if (req.method !== "POST") return result({error:"Method not allowed"},405);
  const adminKey=Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  if (!adminKey || req.headers.get("Authorization") !== "Bearer "+adminKey)
    return result({error:"Forbidden"},403);
  if (Deno.env.get("ALPHA_GHL_SYNC_ENABLED") !== "true" ||
      !Deno.env.get("ALPHA_GHL_PRIVATE_INTEGRATION_TOKEN") ||
      !Deno.env.get("ALPHA_GHL_LOCATION_ID")) {
    return result({enabled:false,attempted:0,reason:"highlevel_not_configured"},503);
  }
  const url=Deno.env.get("SUPABASE_URL");
  if (!url) return result({error:"Database configuration not available"},503);
  const db=createClient(url,adminKey,{auth:{persistSession:false,autoRefreshToken:false}});
  const limit=20;
  const {data,error}=await db.from("alpha_ghl_delivery")
    .select("request_id")
    .in("state",["PENDING","RETRY","SENDING"])
    .lte("next_attempt_at",new Date().toISOString())
    .lt("attempts",5)
    .order("created_at",{ascending:true})
    .limit(limit);
  if (error) return result({error:"Failed to read delivery state"},503);
  const summary:Record<string,number>={eligible:(data||[]).length,
    delivered:0,retry:0,blocked:0,not_due:0};
  for (const row of (data||[])) {
    try {
      const status=await deliverToGhl(db,row.request_id);
      if(status==="DELIVERED") summary.delivered++;
      else if(status==="RETRY") summary.retry++;
      else if(status==="BLOCKED") summary.blocked++;
      else summary.not_due++;
    } catch {summary.retry++;}
  }
  return result(summary,200);
});
