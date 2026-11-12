// ALPHA Task21 — one-operator production Auth interface.
// Public key is intentionally publishable; NEVER ship service_role credentials.
// Access token, refresh token, MFA secret and codes stay in memory only.
// No cookie/localStorage/sessionStorage, analytics or third-party JavaScript.
const PROJECT="https://bkutbglzivfdnoerigyb.supabase.co";
const PUBLISHABLE_KEY="sb_publishable_6eUPcIl4irVSlm1Z117KTQ_KVhdjPgU";
const OPERATOR_API=PROJECT+"/functions/v1/alpha-support-manual-operator";
const CRM_REVIEW_API=PROJECT+"/functions/v1/alpha-ghl-operator-review";
const CRM_CONSENT_API=PROJECT+"/functions/v1/alpha-ghl-consent-readiness";
const AUTH_API=PROJECT+"/auth/v1";
const UUID=/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const $=id=>document.getElementById(id);
const panels=["signin-panel","mfa-setup-panel","mfa-verify-panel","pending-panel","queue-panel"];
let token=null,expiresAt=0,verifiedId=null,activeFactor=null,pendingEnrollFactor=null;
let busy=false,page=0,cases=[];
function showPanel(id){
 for(const p of panels)$(p).hidden=p!==id;
 if(id!=="mfa-setup-panel"){
  $("mfa-enroll-secret").textContent="";
  $("mfa-enroll-code").value="";
  $("mfa-enroll-secret-panel").hidden=true;
  pendingEnrollFactor=null;
 }
}
function announce(message){$("status").textContent=message;}
function clearCaseRows(){$("case-rows").replaceChildren();}
function resetSession(){
 token=null;expiresAt=0;verifiedId=null;activeFactor=null;pendingEnrollFactor=null;
 $("staff-password").value="";$("mfa-code").value="";$("mfa-enroll-code").value="";
 $("mfa-enroll-secret").textContent="";$("pending-user-id").textContent="";
 clearCaseRows();cases=[];page=0;
 $("crm-review-result").textContent="Not checked in this session. Follow-up remains on hold.";
 $("crm-consent-result").textContent="Not checked. All historical inquiries remain on HOLD. This report cannot approve outreach.";
 showPanel("signin-panel");
}
function guardUI(){
 if(!token||expiresAt<=Date.now()+1500)throw Error("SESSION_EXPIRED");
}
function setBusy(value){
 busy=value;
 for(const e of document.querySelectorAll("button"))e.disabled=value;
 if(!value){
  $("previous").disabled=page===0;
  $("next").disabled=cases.length!==20||page>=100;
 }
}
function authHeaders(access){
 const headers={"apikey":PUBLISHABLE_KEY,"Content-Type":"application/json"};
 if(access)headers.Authorization="Bearer "+access;
 return headers;
}
async function authRequest(path,{method="GET",body=null,access=token}={}){
 const res=await fetch(AUTH_API+path,{
  method,
  headers:authHeaders(access),
  ...(body!==null?{body:JSON.stringify(body)}:{}),
  cache:"no-store",credentials:"omit",referrerPolicy:"no-referrer",
  redirect:"error",signal:AbortSignal.timeout(18000)
 });
 let json=null;
 try{json=await res.json();}catch{throw Error("AUTH_SERVICE_UNAVAILABLE");}
 if(!res.ok)throw Error("AUTH_REJECTED");
 return json;
}
function setTokenFromResponse(x){
 if(!x||typeof x.access_token!=="string"||x.access_token.length<30||
    !Number.isFinite(x.expires_in)||x.expires_in<60)
   throw Error("AUTH_INVALID_SESSION");
 token=x.access_token;
 expiresAt=Date.now()+x.expires_in*1000;
}
async function identity(){
 guardUI();
 const user=await authRequest("/user");
 if(!UUID.test(user?.id))throw Error("AUTH_UNVERIFIED");
 verifiedId=user.id;
 return user;
}
function listVerifiedFactors(user){
 return Array.isArray(user?.factors)
  ?user.factors.filter(f=>f?.factor_type==="totp"&&f?.status==="verified"&&UUID.test(f?.id))
  :[];
}
async function afterPasswordSignIn(){
 const user=await identity();
 const verified=listVerifiedFactors(user);
 if(verified.length){
  activeFactor=verified[0].id;
  showPanel("mfa-verify-panel");
  announce("Password verified. Enter the six-digit code from your enrolled authenticator.");
 }else{
  showPanel("mfa-setup-panel");
  announce("Password verified. You must enroll TOTP before any support case access.");
 }
}
async function authenticate(e){
 e.preventDefault();
 if(busy)return;
 const email=$("staff-email").value.trim(),password=$("staff-password").value;
 if(!email||!password)return;
 setBusy(true);
 try{
  const data=await authRequest("/token?grant_type=password",{
   method:"POST",body:{email,password},access:null
  });
  setTokenFromResponse(data);
  $("staff-password").value="";
  await afterPasswordSignIn();
 }catch{
  resetSession();
  announce("Staff account unavailable or sign-in rejected. Only an invited, verified production Supabase account can proceed.");
 }finally{setBusy(false);}
}
async function enroll(){
 if(busy)return;
 setBusy(true);
 try{
  guardUI();
  const enrolled=await authRequest("/factors",{
   method:"POST",body:{factor_type:"totp",friendly_name:"ALPHA Primary Support Operator"}
  });
  if(!UUID.test(enrolled?.id)||!/^[A-Za-z2-7]{16,256}$/.test(enrolled?.totp?.secret||""))
    throw Error("INVALID_MFA_ENROLLMENT");
  pendingEnrollFactor=enrolled.id;
  $("mfa-enroll-secret").textContent=enrolled.totp.secret;
  $("mfa-enroll-secret-panel").hidden=false;
  announce("Authenticator enrollment started. Enter its six-digit code to activate MFA.");
 }catch{
  $("mfa-enroll-secret").textContent="";
  $("mfa-enroll-secret-panel").hidden=true;
  pendingEnrollFactor=null;
  announce("MFA enrollment unavailable. No operator permissions were granted.");
 }finally{setBusy(false);}
}
async function verifyCode(factor,code){
 guardUI();
 if(!UUID.test(factor)||!/^[0-9]{6}$/.test(code))throw Error("INVALID_TOTP_INPUT");
 const challenge=await authRequest("/factors/"+factor+"/challenge",{method:"POST",body:{}});
 if(!UUID.test(challenge?.id))throw Error("INVALID_MFA_CHALLENGE");
 const response=await authRequest("/factors/"+factor+"/verify",{
  method:"POST",body:{challenge_id:challenge.id,code}
 });
 setTokenFromResponse(response);
 await identity();
}
async function verifyEnrolled(e){
 e.preventDefault();
 if(busy)return;
 setBusy(true);
 try{
  await verifyCode(pendingEnrollFactor,$("mfa-enroll-code").value.trim());
  $("mfa-enroll-code").value="";
  $("mfa-enroll-secret").textContent="";
  pendingEnrollFactor=null;
  showPanel("pending-panel"); // temporary UI until role-checked server queue
  announce("Authenticator verified. Checking private operator authorization.");
  await loadQueue(0);
 }catch{
  $("mfa-enroll-code").value="";
  announce("MFA verification rejected or unavailable. No case permission has been granted.");
 }finally{setBusy(false);}
}
async function verifyExisting(e){
 e.preventDefault();if(busy)return;
 setBusy(true);
 try{
  await verifyCode(activeFactor,$("mfa-code").value.trim());
  $("mfa-code").value="";
  showPanel("pending-panel");
  announce("Second factor verified. Checking private operator authorization.");
  await loadQueue(0);
 }catch{
  $("mfa-code").value="";
  announce("Incorrect or unavailable authenticator verification. No case data was loaded.");
 }finally{setBusy(false);}
}
async function operatorRequest(method,path,body=null){
 guardUI();
 const response=await fetch(OPERATOR_API+path,{
  method,
  headers:{apikey:PUBLISHABLE_KEY,Authorization:"Bearer "+token,
   ...(body!==null?{"Content-Type":"application/json"}:{})},
  ...(body!==null?{body:JSON.stringify(body)}:{}),
  credentials:"omit",cache:"no-store",referrerPolicy:"no-referrer",
  redirect:"error",signal:AbortSignal.timeout(18000)
 });
 let json=null;
 try{json=await response.json();}catch{throw Error("OPERATOR_SERVICE_UNAVAILABLE");}
 if(response.status===401)throw Error("SESSION_EXPIRED");
 if(response.status===403)throw Error("OPERATOR_NOT_AUTHORIZED");
 if(!response.ok)throw Error("OPERATOR_SERVICE_UNAVAILABLE");
 return json;
}
function showPending(){
 clearCaseRows();cases=[];page=0;
 if(UUID.test(verifiedId||""))$("pending-user-id").textContent=verifiedId;
 showPanel("pending-panel");
 announce("Verified login. Your owner-approved private operator grant is not yet enabled. No support records were loaded.");
}
function renderCases(list){
 clearCaseRows();
 for(const c of list){
  const tr=document.createElement("tr");
  const date=c.observed_at?new Date(c.observed_at):null;
  const values=[
   c.category||"—",c.status||"—",
   date&&Number.isFinite(date.valueOf())?
    new Intl.DateTimeFormat("en-PH",{dateStyle:"medium",timeStyle:"short",timeZone:"Asia/Manila"}).format(date):"—",
   c.case_id||"—"
  ];
  for(const value of values){
   const td=document.createElement("td");
   td.textContent=value;tr.append(td);
  }
  const actionCell=document.createElement("td");
  if(c.status==="OPEN"&&UUID.test(c.case_id||"")){
   const btn=document.createElement("button");
   btn.type="button";btn.textContent="Claim case";
   btn.addEventListener("click",()=>claim(c.case_id,btn));
   actionCell.append(btn);
  }else{actionCell.textContent=c.claimed_by_me?"Claimed by me":"In review";}
  tr.append(actionCell);$("case-rows").append(tr);
 }
 $("page-label").textContent="Page "+(page+1);
 $("previous").disabled=busy||page===0;
 $("next").disabled=busy||cases.length!==20||page>=100;
}
async function loadQueue(target=0){
 try{
  const result=await operatorRequest("GET","?page="+target);
  if(!Array.isArray(result?.cases)||result.cases.length>20||
    result.cases.some(c=>!UUID.test(c?.case_id||"")||
      !["OPEN","IN_REVIEW"].includes(c?.status)||typeof c?.category!=="string"))
    throw Error("OPERATOR_BAD_QUEUE");
  page=target;
  cases=result.cases;
  renderCases(cases);
  showPanel("queue-panel");
  announce(cases.length?
   "Private support queue loaded; only minimal case metadata is available.":
   "Operator authorized. No open support cases currently in this queue.");
 }catch(error){
  if(error.message==="OPERATOR_NOT_AUTHORIZED"){showPending();return;}
  if(error.message==="SESSION_EXPIRED"){
   resetSession();announce("Session expired. Sign in again.");return;
  }
  announce("Operator queue is unavailable. No case action or customer contact is confirmed.");
 }
}
async function claim(caseId,button){
 if(busy)return;
 setBusy(true);
 try{
  guardUI();
  if(!UUID.test(caseId)||!cases.some(c=>c.case_id===caseId&&c.status==="OPEN"))
   throw Error("INVALID_CASE");
  const result=await operatorRequest("POST","",{action:"CLAIM",case_id:caseId});
  if(result?.committed!==true||result?.case_id!==caseId||result?.status!=="IN_REVIEW")
   throw Error("UNCONFIRMED_CLAIM");
  announce("Claim recorded as IN_REVIEW in the protected audit ledger. No customer was contacted.");
  await loadQueue(page);
 }catch(error){
  if(error.message==="SESSION_EXPIRED"){resetSession();announce("Session expired.");}
  else announce("The claim was not confirmed. Refresh the queue before taking another action.");
 }finally{setBusy(false);}
}
async function signOut(){
 if(busy)return;
 setBusy(true);
 const oldToken=token;
 resetSession();
 announce("Signed out locally. All browser session tokens were discarded.");
 if(oldToken)try{await authRequest("/logout",{method:"POST",body:{},access:oldToken});}catch{/* local session already gone */}
 setBusy(false);
}
// Read-only GHL verification; the browser receives only aggregate suppression
// states from a JWT+AAL2+private-grant protected server endpoint.
async function runCrmReadback(){
 if(busy)return;
 setBusy(true);
 try{
  guardUI();
  $("crm-review-result").textContent="Checking five existing HighLevel delivery links; no contact or marketing is sent.";
  const response=await fetch(CRM_REVIEW_API,{
   method:"POST",
   headers:{"apikey":PUBLISHABLE_KEY,"Authorization":"Bearer "+token,
      "Content-Type":"application/json"},
   body:"{}",credentials:"omit",cache:"no-store",
   referrerPolicy:"no-referrer",redirect:"error",signal:AbortSignal.timeout(55000)
  });
  if(!response.ok){
   $("crm-review-result").textContent=response.status===429
     ?"Readback already checked recently. Follow-up remains on HOLD."
     :"Provider suppression could not be verified. Follow-up remains on HOLD.";
   return;
  }
  const v=await response.json();
  if(!Number.isInteger(v?.processed)||v.processed>20||
      !v?.suppression||typeof v.suppression!=="object")
   throw Error("INVALID_AGGREGATE");
  const c=v.suppression;
  const amounts=["SUPPRESSED","CLEAR","UNKNOWN"].map(k=>Number.isInteger(c[k])&&c[k]>=0?c[k]:null);
  if(amounts.some(x=>x===null)||amounts.reduce((a,b)=>a+b,0)!==v.processed)
   throw Error("INVALID_AGGREGATE");
  $("crm-review-result").textContent=
   "Remote CRM check recorded: "+v.processed+" links. Suppressed: "+amounts[0]+
   "; clear: "+amounts[1]+"; unknown: "+amounts[2]+
   ". This does not authorize outreach. Review history remains in the protected audit ledger.";
 }catch{
  $("crm-review-result").textContent=
    "Remote CRM verification unavailable. Do not contact historical leads; follow-up remains on HOLD.";
 }finally{setBusy(false);}
}
$("signin-form").addEventListener("submit",authenticate);
$("begin-enroll").addEventListener("click",enroll);
$("enroll-verify-form").addEventListener("submit",verifyEnrolled);
$("mfa-verify-form").addEventListener("submit",verifyExisting);
for(const id of ["enroll-signout","mfa-signout","pending-signout","queue-signout"])
 $(id).addEventListener("click",signOut);
$("pending-recheck").addEventListener("click",async()=>{
 if(busy)return;
 setBusy(true);await loadQueue(0);setBusy(false);
});
$("copy-user-id").addEventListener("click",async()=>{
 if(!UUID.test(verifiedId||""))return;
 try{await navigator.clipboard.writeText(verifiedId);announce("Verified user ID copied for owner review only.");}
 catch{announce("Copy was unavailable. The verified account ID is displayed above.");}
});
// Local consent readiness ONLY: no contact names, IDs, messages or provider writes.
// The JWT+AAL2+existing operator grant is verified again by the server.
async function runConsentReadiness(){
 if(busy)return;
 setBusy(true);
 try{
  guardUI();
  $("crm-consent-result").textContent="Checking only aggregate source consent and hold status.";
  const response=await fetch(CRM_CONSENT_API,{
   method:"GET",
   headers:{"apikey":PUBLISHABLE_KEY,Authorization:"Bearer "+token},
   credentials:"omit",cache:"no-store",referrerPolicy:"no-referrer",
   redirect:"error",signal:AbortSignal.timeout(18000)
  });
  if(!response.ok)throw Error("NO_CONSENT_STATUS");
  const v=await response.json();
  if(v?.status!=="READ_ONLY_LOCAL_READINESS"||typeof v?.followup_permission!=="string"
   ||!v.followup_permission.startsWith("HOLD"))
   throw Error("UNTRUSTED_CONSENT_RESULT");
  const keys=["inquiries","contact_consent_recorded","missing_current_contact_consent",
   "marketing_opted_in","marketing_opted_out","pending_operator_review",
   "locally_cleared","hold_count","reviewed_identity_and_authority","revocation_events"];
  if(keys.some(k=>!Number.isSafeInteger(v[k])||v[k]<0)||v.inquiries!==v.hold_count+v.locally_cleared)
   throw Error("INVALID_CONSENT_TOTALS");
  $("crm-consent-result").textContent=
    "Local inquiry records: "+v.inquiries+
    ". Current contact consent recorded: "+v.contact_consent_recorded+
    ". Missing valid contact consent: "+v.missing_current_contact_consent+
    ". Marketing opted out: "+v.marketing_opted_out+
    ". Awaiting operator review: "+v.pending_operator_review+
    ". Locally CLEARED: "+v.locally_cleared+
    ". HOLD: "+v.hold_count+
    ". Recorded consent withdrawal events: "+v.revocation_events+
    ". Report is read-only. No outreach has been authorized.";
 }catch{
  $("crm-consent-result").textContent=
    "Consent readiness unavailable. All historical inquiries remain on HOLD; no outreach approved.";
 }finally{setBusy(false);}
}
$("crm-consent-readiness").addEventListener("click",runConsentReadiness);
$("crm-readback").addEventListener("click",runCrmReadback);
$("refresh").addEventListener("click",async()=>{if(busy)return;setBusy(true);await loadQueue(page);setBusy(false);});
$("previous").addEventListener("click",async()=>{if(busy||page===0)return;setBusy(true);await loadQueue(page-1);setBusy(false);});
$("next").addEventListener("click",async()=>{if(busy||cases.length!==20||page>=100)return;setBusy(true);await loadQueue(page+1);setBusy(false);});
// Deliberately no browser-stored access or refresh token, telemetry or background polling.
