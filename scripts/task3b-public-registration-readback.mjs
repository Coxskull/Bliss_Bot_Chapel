// Zero-login, zero-POST public registration release readback.
// Fixed approved origins only; never output Supabase configuration or identity tokens.
const publicOrigin="https://alphacreativecontent.com";
const appOrigin="https://bliss-bot-production-5eb8.up.railway.app";
async function get(path,origin,contentType){
  const res=await fetch(origin+path,{method:"GET",redirect:"error",signal:AbortSignal.timeout(12000),headers:{"Accept":contentType}});
  if(res.status!==200)throw Error("HTTP_"+res.status+" on approved path "+path);
  const raw=await res.text();
  if(raw.length>400000)throw Error("Unexpected oversized public page");
  return raw;
}
let config,form,publicPage;
try{
  const body=await get("/api/auth/supabase-config",appOrigin,"application/json");
  config=JSON.parse(body);
  form=await get("/advertiser-registration.html",appOrigin,"text/html");
  publicPage=await get("/advertiser-login.html",publicOrigin,"text/html");
}catch{
  console.log("TASK3B_PUBLIC_READBACK=UNVERIFIED_NETWORK_OR_HTTP");
  process.exit(1);
}
const backendEnabled=config?.advertiserRegistrationEnabled===true;
const formPresent=form.includes('id="signup-form"')&&form.includes('id="registration-form"')
 && form.includes('advertiser-account-privacy.html')&&form.includes('advertiser-account-terms.html');
const directLink=publicPage.includes('href="'+appOrigin+'/advertiser-registration.html"');
const disabled=publicPage.includes('Account registration not yet available');
if(!formPresent||(directLink&&disabled))throw Error("TASK3B_PUBLIC_ENTRYPOINT_INCONSISTENT");
console.log("TASK3B_PUBLIC_BACKEND="+(backendEnabled?"ENABLED":"HELD"));
console.log("TASK3B_APP_REGISTRATION_PAGE="+(formPresent?"PRESENT":"MISSING"));
console.log("TASK3B_VERCEL_SIGNUP_LINK="+(directLink?"LIVE":disabled?"DISABLED":"MISSING"));
console.log("TASK3B_PUBLIC_READBACK=COMPLETE_GET_ONLY_NO_ACCOUNT_CREATED");
if(process.env.ALPHA_REQUIRE_PUBLIC_ADVERTISER==="true"&&(!backendEnabled||!directLink))
  throw Error("TASK3B_PUBLIC_RELEASE_GATE_NOT_ACCEPTED");
