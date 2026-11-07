import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2.95.0";
import { handleOperatorRequest } from "./operator-handler.mjs";

// Private, authenticated human operations only. Deploy with verify_jwt=true.
// No public URL, no service-role token in the client, no public CORS by default.
Deno.serve(async (req: Request) => {
  const url=Deno.env.get("SUPABASE_URL");
  const privateKey=Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  if (!url || !privateKey)
    return new Response('{"error":"Operator service unavailable"}',{
      status:503,headers:{"Content-Type":"application/json","Cache-Control":"no-store"}
    });
  const db=createClient(url,privateKey,{
    auth:{persistSession:false,autoRefreshToken:false}
  });
  try { return await handleOperatorRequest(req,db,Deno.env); }
  catch { return new Response('{"error":"Operator service unavailable"}',{
    status:503,headers:{"Content-Type":"application/json","Cache-Control":"no-store"}
  }); }
});
