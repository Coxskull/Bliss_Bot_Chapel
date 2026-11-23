import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import {createClient} from "npm:@supabase/supabase-js@2.95.0";
import {handleCrmStaffReadback} from "./handler.mjs";

const production="https://bkutbglzivfdnoerigyb.supabase.co";
Deno.serve(async req=>{
 const url=Deno.env.get("SUPABASE_URL");
 const serviceKey=Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
 if(url!==production||!serviceKey)return new Response('{"error":"Service unavailable"}',{
  status:503,headers:{"Content-Type":"application/json","Cache-Control":"no-store"}
 });
 try{
  const client=createClient(url,serviceKey,{auth:{autoRefreshToken:false,persistSession:false}});
  return await handleCrmStaffReadback(req,{client,fetchReconcile:async()=>{
   const response=await fetch(production+"/functions/v1/alpha-ghl-readback",{
    method:"POST",headers:{"Authorization":"Bearer "+serviceKey,"apikey":serviceKey,
     "Content-Type":"application/json"},
    body:"{}",cache:"no-store",redirect:"error",signal:AbortSignal.timeout(45000)
   });
   if(!response.ok)return {ok:false};
   const data=await response.json();
   return {ok:true,data};
  }});
 }catch{
  return new Response('{"error":"CRM verification unavailable"}',{
   status:503,headers:{"Content-Type":"application/json","Cache-Control":"no-store"}
  });
 }
});
