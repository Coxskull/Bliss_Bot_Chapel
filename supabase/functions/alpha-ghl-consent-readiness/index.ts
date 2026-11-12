import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import {createClient} from "npm:@supabase/supabase-js@2.95.0";
import {handleConsentReadiness} from "./handler.mjs";

const PRODUCTION="https://bkutbglzivfdnoerigyb.supabase.co";
Deno.serve(async req=>{
 const url=Deno.env.get("SUPABASE_URL");
 const secret=Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
 if(url!==PRODUCTION||!secret)return new Response('{"error":"Service unavailable"}',{
  status:503,headers:{"Content-Type":"application/json","Cache-Control":"no-store"}
 });
 try{
  const client=createClient(url,secret,{auth:{autoRefreshToken:false,persistSession:false}});
  return await handleConsentReadiness(req,{client});
 }catch{
  return new Response('{"error":"Consent readiness unavailable"}',{
    status:503,headers:{"Content-Type":"application/json","Cache-Control":"no-store"}
  });
 }
});
