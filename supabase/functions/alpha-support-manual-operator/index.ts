import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2.95.0";
import { handleManualOperatorRequest } from "./handler.mjs";

// Dedicated restricted production support operator endpoint.
// Supabase JWT verification MUST remain enabled (verify_jwt=true).
// This endpoint is not a public ticket form and never sends emails.
const productionUrl="https://bkutbglzivfdnoerigyb.supabase.co";
Deno.serve(async (request: Request) => {
 const projectUrl=Deno.env.get("SUPABASE_URL");
 const key=Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
 if(projectUrl!==productionUrl||!key){
  return new Response(JSON.stringify({error:"Operator service unavailable"}),{
   status:503,headers:{"Content-Type":"application/json","Cache-Control":"no-store"}
  });
 }
 try {
  const admin=createClient(projectUrl,key,{auth:{
   persistSession:false,autoRefreshToken:false
  }});
  return await handleManualOperatorRequest(request,{client:admin});
 } catch {
  return new Response(JSON.stringify({error:"Operator service unavailable"}),{
   status:503,headers:{"Content-Type":"application/json","Cache-Control":"no-store"}
  });
 }
});
