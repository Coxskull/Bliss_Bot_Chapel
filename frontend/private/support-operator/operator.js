import { createClient } from "./operator-sdk.js";
import {parseQueue,parseDetail,validateTransition,safeOperatorConfig}
  from "./operator-view-model.mjs";

// This is a source-only, independently hosted PRIVATE page. It is explicitly
// excluded from the public Vercel allowlist and has no auto-activation path.
// User sessions remain in MEMORY, never localStorage, cookies, or URL.
const el=id=>document.getElementById(id);
const panels=["signin-panel","mfa-panel","workspace"];
const status=el("status");
let config=null,client=null,caseId=null,activeRoles=[],currentPage=0,operatorId=null;
let busy=false,queue=[],connected=false,hasMfa=false,mfaVerified=false;
let pendingFactorId=null; // private in-memory enrollment state only
let syntheticTrialEnabled=false; // server-approved single fictional case only
let verifiedAuthId=null; // self-only Auth UUID, never an access-control role
function show(id,visible){el(id).hidden=!visible;}
function announce(text,error=false){
  status.textContent=String(text);
  status.className="status"+(error?" error":"");
}
function clearNode(node){while(node.firstChild)node.removeChild(node.firstChild);}
function plain(node,text){node.textContent=String(text??"—");}
function appendDescription(node,key,value){
  const dt=document.createElement("dt"),dd=document.createElement("dd");
  dt.textContent=key;dd.textContent=String(value??"—");
  node.append(dt,dd);
}
function hidePrivate(){
  show("workspace",false);show("mfa-panel",false);
  show("enroll-offer-panel",false);show("enroll-panel",false);
  show("identity-panel",false);el("identity-id").textContent="";
  el("enroll-secret").textContent="";el("enroll-code").value="";
  pendingFactorId=null;verifiedAuthId=null;
  clearNode(el("queue"));clearNode(el("case-fields"));
  clearNode(el("contact-fields"));clearNode(el("events"));
  show("case-detail",false);show("case-empty",true);
  caseId=null;operatorId=null;activeRoles=[];queue=[];currentPage=0;connected=false;
  syntheticTrialEnabled=false;
  el("connection").textContent="NOT CONNECTED";
}
function setBusy(on){
  busy=Boolean(on);
  for(const id of ["signin-button","mfa-button","mfa-cancel","refresh","verify-mfa",
    "enroll-start","enroll-signout","enroll-verify","enroll-cancel",
    "identity-copy","identity-signout","signout","case-search-button"])
    el(id).disabled=busy;
  el("prev").disabled=busy||!connected||currentPage===0;
  el("next").disabled=busy||!connected||queue.length!==20||currentPage>=100;
  const updateAllowed=config?.mutationsEnabled===true||syntheticTrialEnabled;
  el("save").disabled=busy||!connected||!caseId||
    !updateAllowed||el("owner-id").value!==operatorId;
  el("claim-case").disabled=busy||!connected||!caseId||
    !updateAllowed||el("claim-case").hidden;
  el("verify-mfa").hidden=!connected||!hasMfa||mfaVerified;
}
function requireConfig(){
  const cfg=safeOperatorConfig(window.ALPHA_SUPPORT_OPERATOR_CONFIG,location.origin);
  config=cfg;
  client=createClient(cfg.supabaseUrl,cfg.publishableKey,{
    auth:{persistSession:false,autoRefreshToken:false,detectSessionInUrl:false}
  });
}
async function authorizedApi(method,path="",body=null){
  const session=await client.auth.getSession();
  if(session.error||!session.data?.session?.access_token)
    throw Error("SESSION_EXPIRED");
  const token=session.data.session.access_token;
  const res=await fetch(config.supabaseUrl+"/functions/v1/alpha-support-operator"+path,{
    method,credentials:"omit",mode:"cors",cache:"no-store",
    headers:{"Authorization":"Bearer "+token,"apikey":config.publishableKey,
      "Accept":"application/json",...(body?{"Content-Type":"application/json"}:{})},
    ...(body?{body:JSON.stringify(body)}:{}),
    signal:AbortSignal.timeout(15000)
  });
  let data=null;try{data=await res.json();}catch{ /* invalid response => denied */}
  if(!res.ok||!data){
    if(res.status===401)throw Error("SESSION_EXPIRED");
    if(res.status===403)throw Error("OPERATOR_DENIED");
    if(res.status===423)throw Error("UPDATES_LOCKED");
    throw Error("Operator service unavailable or denied ("+res.status+")");
  }
  return data;
}
function errMessage(e){
  if(e?.message==="SESSION_EXPIRED")return "Session expired. Sign in again with your approved staff account.";
  if(e?.message==="OPERATOR_DENIED")return "No verified operator role for this session. Contact the authorized ALPHA administrator.";
  if(e?.message==="UPDATES_LOCKED")return "Case updates remain disabled on the server. No change was made.";
  return "The protected support operation could not be verified. No action is confirmed.";
}
function resetDetail(){
  caseId=null;syntheticTrialEnabled=false;
  show("case-detail",false);show("case-empty",true);
  el("claim-case").hidden=true;el("claim-case").disabled=true;
  clearNode(el("case-fields"));clearNode(el("contact-fields"));clearNode(el("events"));
}
function caseButton(c){
  const btn=document.createElement("button");btn.type="button";
  btn.setAttribute("aria-current",String(caseId===c.case_id));
  const title=document.createElement("strong");
  title.textContent=c.severity+" · "+c.category.replaceAll("_"," ")+" · "+c.status;
  const small=document.createElement("span");small.className="small";
  small.textContent=c.case_id+" · "+new Date(c.created_at).toLocaleString();
  btn.append(title,small);
  btn.addEventListener("click",()=>{if(!busy)loadDetail(c.case_id);});
  return btn;
}
function renderQueue(){
  const list=el("queue");clearNode(list);
  if(queue.length===0){
    const p=document.createElement("p");p.className="subtle";
    p.textContent="No open cases visible on this page for your authorized roles.";
    list.append(p);
  }else queue.forEach(c=>list.append(caseButton(c)));
  el("page").textContent="Page "+(currentPage+1);
  el("prev").disabled=busy||currentPage===0;
  el("next").disabled=busy||queue.length!==20||currentPage>=100;
  el("role-summary").textContent="Authorized queues: "+activeRoles.join(", ");
}
async function loadQueue(page=0){
  if(busy)return;
  setBusy(true);resetDetail();announce("Loading authorized cases…");
  try {
    const raw=await authorizedApi("GET","?page="+page);
    const parsed=parseQueue(raw,page);
    queue=parsed.cases;activeRoles=parsed.roles;
    operatorId=parsed.operatorId;currentPage=page;
    connected=true;show("signin-panel",false);show("mfa-panel",false);
    show("workspace",true);show("verify-mfa",hasMfa&&!mfaVerified);
    el("connection").textContent="SIGNED IN · ROLE-SCOPED";
    renderQueue();
    announce("Case queue loaded. No automated notification or customer contact was triggered.");
  } catch(e) {
    const canVerify=e?.message==="OPERATOR_DENIED"&&hasMfa&&!mfaVerified;
    // An Auth-verified, but not case-authorized, user may view ONLY their
    // own Auth UUID. No error path may substitute a queue or trusted role.
    // CORS not configured, backend 503, and missing roster remain indistinguishable
    // to the user: we must NOT suggest the operator was approved.
    const identityOnly=!!verifiedAuthId&&!canVerify;
    const id=verifiedAuthId;
    hidePrivate();
    if(canVerify){
      verifiedAuthId=id;
      show("mfa-panel",true);
      announce("A second factor may be required for your assigned support queue. Verify your existing authenticator.",true);
    }else if(identityOnly){
      verifiedAuthId=id;
      el("identity-id").textContent=id;
      show("identity-panel",true);
      announce("Your Auth identity is verified. Support access is not authorized or available. No case was loaded; submit this UUID for administrator review.");
    }else{
      try{await client.auth.signOut();}catch{/* clear private UI regardless */}
      show("signin-panel",true);
      announce(errMessage(e),true);
    }
  }finally {setBusy(false);if(connected)renderQueue();}
}
async function loadDetail(id){
  if(busy||!activeRoles.length)return;
  setBusy(true);resetDetail();announce("Loading private case details…");
  try {
    const data=await authorizedApi("GET","?case_id="+encodeURIComponent(id));
    const detail=parseDetail(data,activeRoles,id);
    caseId=id;
    const ticket=detail.case,contact=detail.contact;
    const fields=el("case-fields");const people=el("contact-fields"),hist=el("events");
    clearNode(fields);clearNode(people);clearNode(hist);
    for(const [k,v] of Object.entries({
      "Case ID":ticket.case_id,"Category":ticket.category,"Severity":ticket.severity,
      "Assigned queue":ticket.assigned_role,"Status":ticket.status,
      "Owner":ticket.owner_user_id||"Unassigned",
      "Opened":ticket.created_at,"Updated":ticket.updated_at
    })) appendDescription(fields,k,v);
    for(const [k,v] of Object.entries({
      "Name":contact.full_name,"Email":contact.email,
      "Issue description":contact.description,"Submitted":contact.created_at
    })) appendDescription(people,k,v);
    if(!detail.events.length){
      const li=document.createElement("li");plain(li,"No event history returned.");hist.append(li);
    }else for(const event of detail.events){
      const li=document.createElement("li");
      plain(li,String(event.occurred_at||"")+" · "+String(event.action||"")+
        " · "+String(event.status||"")+" · "+String(event.summary||""));
      hist.append(li);
    }
    el("case-status").value=ticket.status;
    el("owner-id").value=ticket.owner_user_id||"";
    el("resolution").value=ticket.resolution_summary||"";
    const ownerIsMe=ticket.owner_user_id===operatorId;
    syntheticTrialEnabled=detail.synthetic_trial_enabled===true&&
      ticket.assigned_role==="GENERAL_SUPPORT"&&ticket.category==="general"&&
      ticket.severity==="NORMAL"&&
      contact.email==="task21-browser-fixture@example.invalid";
    const canUpdate=config.mutationsEnabled===true||syntheticTrialEnabled;
    const canClaim=canUpdate&&!ticket.owner_user_id&&
      ticket.status==="OPEN";
    el("claim-case").hidden=!canClaim;
    el("claim-case").disabled=!canClaim;
    const allowed=canUpdate&&ownerIsMe&&ticket.status!=="CLOSED";
    for(const id of ["case-status","resolution"])el(id).disabled=!allowed;
    el("owner-id").disabled=true; // never operator-entered
    el("save").disabled=!allowed;
    el("save").textContent=allowed?"Record human case update":"Case updates not enabled";
    show("case-empty",false);show("case-detail",true);
    renderQueue();announce(syntheticTrialEnabled
      ?"FICTIONAL CASE-ONLY staging trial available. No customer contact or payment."
      :"Private case record loaded. No customer was contacted.");
  } catch(e){resetDetail();announce(errMessage(e),true);}
  finally {setBusy(false);if(connected)renderQueue();}
}
async function signin(e){
  e.preventDefault();if(busy||!client)return;
  setBusy(true);announce("Authenticating staff account…");
  let verified=false;
  const email=el("email").value.trim(),password=el("password").value;
  try {
    if(!email||!password)throw Error("Missing credentials");
    const {data,error}=await client.auth.signInWithPassword({email,password});
    if(error||!data?.session)throw Error("Auth failed");
    const identity=await client.auth.getUser();
    if(identity.error||!identity.data?.user?.id||
       !/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i
         .test(identity.data.user.id))throw Error("Verified Auth account not available");
    verifiedAuthId=identity.data.user.id;
    el("password").value="";
    show("signin-panel",false);
    // List factors only after the password sign-in has been verified by Auth.
    const factors=await client.auth.mfa.listFactors();
    if(factors.error||!Array.isArray(factors.data?.totp))
      throw Error("Cannot verify existing factors");
    const available=factors.data.totp.filter(x=>x.status==="verified");
    clearNode(el("factor"));
    for(const factor of available) {
      const opt=document.createElement("option");opt.value=factor.id;
      opt.textContent=factor.friendly_name||"Verified authenticator";el("factor").append(opt);
    }
    hasMfa=available.length>0;
    mfaVerified=false;
    if(!hasMfa){
      show("enroll-offer-panel",true);
      announce("Password sign-in verified. Set up TOTP MFA to prepare this staff account; no operator role is granted by enrollment.");
    }else{
      show("mfa-panel",true);
      announce("Identity verified. Checking authorized support roles…");
    }
    verified=true;
  }catch{el("password").value="";verifiedAuthId=null;show("signin-panel",true);
    try{await client.auth.signOut();}catch{/* clear unverified session */}
    announce("Sign-in or verified Auth identity was not available. Use an approved staging staff account.",true);}
  finally{setBusy(false);}
  if(verified&&hasMfa)await loadQueue(0);
}
async function startEnrollment(){
  if(busy||!client||hasMfa||pendingFactorId)return;
  setBusy(true);announce("Requesting one-time MFA setup for your signed-in account…");
  try {
    const {data,error}=await client.auth.mfa.enroll({
      factorType:"totp",friendlyName:"ALPHA Support Operator"
    });
    const secret=data?.totp?.secret;
    if(error||typeof data?.id!=="string"||typeof secret!=="string"||
       !/^[A-Za-z2-7]{16,256}$/.test(secret))
      throw Error("MFA enrollment failed");
    pendingFactorId=data.id;
    el("enroll-secret").textContent=secret; // never stored, logged or sent elsewhere
    show("enroll-offer-panel",false);show("enroll-panel",true);
    announce("Enter the secret directly into your authenticator app, then verify your six-digit code.");
  }catch{
    pendingFactorId=null;el("enroll-secret").textContent="";
    show("enroll-offer-panel",true);show("enroll-panel",false);
    announce("Authenticator setup could not start. No staff permission was granted.",true);
  }finally{setBusy(false);}
}
async function finishEnrollment(e){
  e.preventDefault();if(busy||!client||!pendingFactorId)return;
  const code=el("enroll-code").value.trim();
  if(!/^\d{6}$/.test(code)){
    announce("Enter a six-digit code from your newly enrolled authenticator.",true);
    return;
  }
  setBusy(true);announce("Verifying authenticator enrollment…");
  let verified=false;
  try {
    const {error}=await client.auth.mfa.challengeAndVerify({
      factorId:pendingFactorId,code
    });
    if(error)throw Error("Enrollment verification rejected");
    verified=true;
    hasMfa=true;mfaVerified=true;
    el("enroll-secret").textContent="";el("enroll-code").value="";
    pendingFactorId=null;
    show("enroll-panel",false);
    announce("Authenticator enrollment verified. Checking separately approved operator roster…");
  }catch{
    el("enroll-code").value="";
    announce("Authenticator code not verified. The setup secret remains in this private session for retry.",true);
  }finally{setBusy(false);}
  if(verified)await loadQueue(0);
}
async function mfa(e){
  e.preventDefault();if(busy||!client)return;
  const factorId=el("factor").value,code=el("totp").value.trim();
  if(!factorId||!/^\d{6}$/.test(code)){announce("Enter a valid 6-digit MFA code.",true);return;}
  setBusy(true);announce("Verifying second factor…");
  try{
    const {error}=await client.auth.mfa.challengeAndVerify({factorId,code});
    if(error)throw Error("MFA not verified");
    mfaVerified=true;el("totp").value="";show("mfa-panel",false);
    announce("Second factor verified. Loading permitted queues…");
  }catch{el("totp").value="";announce("MFA verification failed. No sensitive cases were revealed.",true);}
  finally{setBusy(false);}
  await loadQueue(0);
}
async function signout(){
  if(busy)return;setBusy(true);hidePrivate();hasMfa=false;mfaVerified=false;
  try{await client.auth.signOut();}catch{/* cleared locally */}
  finally{setBusy(false);show("signin-panel",true);announce("Signed out; private case details cleared.");}
}
async function claimCurrentCase(){
  if(busy||!caseId||(config?.mutationsEnabled!==true&&!syntheticTrialEnabled))return;
  const target=caseId;
  setBusy(true);announce("Requesting an atomic human case acknowledgement…");
  let success=false;
  try{
    const result=await authorizedApi("POST","",{action:"CLAIM",case_id:target});
    if(result.committed!==true||result.case_id!==target||
       result.status!=="TRIAGED"||result.action!=="CLAIM")
      throw Error("Unverified claim response");
    success=true;
    announce("Human case acknowledgement committed. No customer was contacted.");
  }catch(e){announce(errMessage(e),true);}
  finally{setBusy(false);}
  if(success){await loadQueue(currentPage);if(connected)await loadDetail(target);}
}
async function saveTransition(e){
  e.preventDefault();
  if(busy||!caseId||(config.mutationsEnabled!==true&&!syntheticTrialEnabled))return;
  let body;
  try{body=validateTransition(caseId,el("case-status").value,el("owner-id").value.trim(),
    el("resolution").value);}
  catch {announce("An approved owner UUID and valid status/resolution are required.",true);return;}
  setBusy(true);announce("Requesting audited human case transition…");
  let ok=false;
  try{
    const result=await authorizedApi("POST","",body);
    if(result.committed!==true||result.case_id!==caseId||result.status!==body.status)
      throw Error("Unverified transition");
    ok=true;
    announce("Audited case update committed by the server. No refund or customer contact was issued.");
  }catch(e){announce(errMessage(e),true);}
  finally{setBusy(false);}
  if(ok)await loadQueue(currentPage);
}
function init(){
  hidePrivate();
  try{requireConfig();show("signin-panel",true);announce("Private operator sign-in is available. Only approved roles may view cases.");}
  catch{show("signin-panel",false);
    announce("Private operator deployment is not configured. No sign-in or customer data access is enabled.",true);
    return;}
  el("signin-form").addEventListener("submit",signin);
  el("mfa-form").addEventListener("submit",mfa);
  el("enroll-start").addEventListener("click",startEnrollment);
  el("enroll-signout").addEventListener("click",signout);
  el("enroll-form").addEventListener("submit",finishEnrollment);
  el("enroll-cancel").addEventListener("click",signout);
  el("identity-signout").addEventListener("click",signout);
  el("identity-copy").addEventListener("click",async()=>{
    if(!verifiedAuthId||busy)return;
    try{
      await navigator.clipboard.writeText(verifiedAuthId);
      announce("Verified staging Auth UUID copied. Do not share login tokens or MFA secrets.");
    }catch{announce("Copy was unavailable. Select the displayed UUID manually.",true);}
  });
  el("refresh").addEventListener("click",()=>loadQueue(currentPage));
  el("prev").addEventListener("click",()=>loadQueue(Math.max(0,currentPage-1)));
  el("next").addEventListener("click",()=>{if(queue.length===20)loadQueue(Math.min(100,currentPage+1));});
  el("signout").addEventListener("click",signout);
  el("mfa-cancel").addEventListener("click",signout);
  el("verify-mfa").addEventListener("click",()=>{
    if(busy||!hasMfa||mfaVerified)return;
    show("workspace",false);show("mfa-panel",true);
    announce("Complete MFA to unlock additional sensitive support roles.");
  });
  el("case-search-form").addEventListener("submit",event=>{
    event.preventDefault();
    const id=el("case-search").value.trim();
    if(!/^[0-9a-fA-F-]{36}$/.test(id)) {announce("Use an exact valid case UUID.",true);return;}
    loadDetail(id);
  });
  el("transition-form").addEventListener("submit",saveTransition);
  el("claim-case").addEventListener("click",claimCurrentCase);
}
init();
