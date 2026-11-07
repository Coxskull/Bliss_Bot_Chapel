#!/usr/bin/env node
// ALPHA Task21. All browser traffic is intercepted, INCLUDING the Supabase
// project URL and the CDN import. No real customer/provider network access.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const { chromium } = require("playwright");
const root = path.resolve(__dirname, "../../frontend/private/support-operator");
const artifactDir = path.resolve(__dirname, "../../BrowserEvidence");
fs.mkdirSync(artifactDir,{recursive:true});
const origin = "https://staff.example.invalid";
const apiHost = "wdlmcriajgsretgyusmo.supabase.co";
const uuid = n => "00000000-0000-4000-8000-" + String(n).padStart(12,"0");
const general = {
 case_id:uuid(1),request_id:uuid(11),category:"general",severity:"NORMAL",
 assigned_role:"GENERAL_SUPPORT",owner_user_id:null,status:"OPEN",
 created_at:"2026-11-08T00:00:00Z",updated_at:"2026-11-08T00:00:00Z"
};
const security = {
 case_id:uuid(2),request_id:uuid(12),category:"security_incident",severity:"CRITICAL",
 assigned_role:"SECURITY_REVIEW",owner_user_id:null,status:"OPEN",
 created_at:"2026-11-08T01:00:00Z",updated_at:"2026-11-08T01:00:00Z"
};
const resolved = {...security,case_id:uuid(3),request_id:uuid(13),
 status:"CLOSED",resolution_summary:"Synthetic resolved event"};
const threat="<img src=x onerror=window.__TASK21_XSS=true>";
const sdk=String.raw`
export function createClient() {
  let session=null;
  return {auth:{
    signInWithPassword:async ({email,password}) => {
      if (email!=="staff@example.invalid" || password!=="synthetic-only")
        return {data:null,error:{message:"denied"}};
      session={access_token:"synthetic.jwt.initial"};
      return {data:{session},error:null};
    },
    getSession:async()=>({data:{session},error:null}),
    getUser:async()=>session?
      {data:{user:{id:"00000000-0000-4000-8000-000000000099"}},error:null}:
      {data:{user:null},error:{message:"no session"}},
    signOut:async()=>{session=null;return {error:null};},
    mfa:{
      enroll:async ({factorType,friendlyName})=>{
        if(factorType!=="totp"||friendlyName!=="ALPHA Support Operator")
          return {data:null,error:{message:"wrong MFA setup"}};
        return {data:{id:"synthetic-factor",totp:{
          secret:"JBSWY3DPEHPK3PXP"}},error:null};
      },
      listFactors:async()=>({data:{totp:[{
        status:"verified",id:"synthetic-factor",friendly_name:"Fixture TOTP"
      }]},error:null}),
      challengeAndVerify:async({factorId,code})=>{
        if (factorId!=="synthetic-factor" || code!=="123456")
          return {error:{message:"invalid factor"}};
        session={access_token:"synthetic.jwt.aal2"};
        return {error:null};
      }
    }
  }};
}
`;
async function runScenario(browser, scenario) {
  const context=await browser.newContext({
    ignoreHTTPSErrors:true,viewport:{width:1280,height:850},serviceWorkers:"block"
  });
  const page=await context.newPage();
  const accidentalEgress=[],pageErrors=[],writes=[];
  let claimed=false;
  page.on("pageerror",e=>pageErrors.push(e.message));
  await context.route("**/*",async route=>{
    const request=route.request(),url=new URL(request.url());
    const parts=url.pathname.split("/");
    if(url.hostname==="staff.example.invalid"){
      const filename=parts.at(-1)||"index.html";
      if(filename==="operator-sdk.js"){
        return route.fulfill({status:200,contentType:"text/javascript",
          body:scenario==="enroll-first" ?
            sdk.replace('status:"verified"','status:"unverified"') : sdk});
      }
      if(filename==="operator-config.js"){
        const config={
          environment:"staging",supabaseUrl:"https://"+apiHost,
          publishableKey:"sb_publishable_SyntheticBrowserFixtureOnly",
          operatorOrigin:origin,mutationsEnabled:scenario==="claim-enabled"
        };
        return route.fulfill({status:200,contentType:"text/javascript",
          body:"window.ALPHA_SUPPORT_OPERATOR_CONFIG = Object.freeze("+
            JSON.stringify(config)+");"});
      }
      if(!["index.html","operator.js","operator-view-model.mjs","operator.css"].includes(filename))
        return route.fulfill({status:404,body:"Not available"});
      const file=path.join(root,filename);
      return route.fulfill({status:200,
        contentType:filename.endsWith(".html")?"text/html":
          filename.endsWith(".css")?"text/css":"text/javascript",
        body:fs.readFileSync(file)});
    }
    if(url.hostname===apiHost &&
       url.pathname==="/functions/v1/alpha-support-operator"){
      const auth=request.headers()["authorization"]||"";
      const elevated=auth==="Bearer synthetic.jwt.aal2";
      const limited=auth==="Bearer synthetic.jwt.initial";
      const status=(n,body)=>route.fulfill({
        status:n,contentType:"application/json",
        headers:{"Access-Control-Allow-Origin":origin,"Cache-Control":"no-store"},
        body:JSON.stringify(body)
      });
      if(!elevated && !limited)
        return status(401,{error:"Unauthenticated"});
      if(request.method()!=="GET"){
        writes.push({method:request.method(),url:request.url()});
        if(scenario!=="claim-enabled")return status(423,{error:"Writes disabled"});
        const payload=JSON.parse(request.postData()||"{}");
        if(!elevated||payload.action!=="CLAIM"||payload.case_id!==security.case_id)
          return status(403,{error:"Invalid claim"});
        if(claimed)return status(409,{error:"Already claimed"});
        claimed=true;
        return status(200,{committed:true,case_id:security.case_id,
          status:"TRIAGED",action:"CLAIM"});
      }
      if(scenario==="unrostered"||
         (scenario==="sensitive-only" && !elevated))
        return status(403,{error:"Operator permission denied"});
      const caseId=url.searchParams.get("case_id");
      const currentSecurity=claimed
        ? {...security,owner_user_id:uuid(99),status:"TRIAGED"} : security;
      const allowed=elevated?[currentSecurity,general,resolved]:[general];
      if(caseId){
        const c=allowed.find(item=>item.case_id===caseId);
        if(!c)return status(404,{error:"Case not found"});
        return status(200,{case:c,
          contact:{full_name:"Fictional Operator Fixture",
            email:"fixture@example.invalid",
            description:c.category==="security_incident"?threat:"General question",
            created_at:"2026-11-08T00:00:00Z"},
          events:[{event_id:1,action:"CREATED",status:"OPEN",
            summary:"Synthetic event "+threat,occurred_at:"2026-11-08T00:00:00Z"}],
          history_limit:100});
      }
      const roles=elevated?["GENERAL_SUPPORT","SECURITY_REVIEW"]:["GENERAL_SUPPORT"];
      const cases=elevated?[currentSecurity,general]:[general];
      return status(200,{cases,page:Number(url.searchParams.get("page")||"0"),
        page_size:20,visible_roles:roles,operator_user_id:uuid(99)});
    }
    accidentalEgress.push(request.url());
    return route.abort("blockedbyclient");
  });

  await page.goto(origin+"/index.html",{waitUntil:"load"});
  await page.locator("#signin-panel").waitFor({state:"visible"});
  assert.equal(await page.locator("#workspace").isVisible(),false);
  await page.locator("#email").fill("staff@example.invalid");
  await page.locator("#password").fill("synthetic-only");
  await page.locator("#signin-button").click();

  if(scenario==="enroll-first"){
    await page.locator("#enroll-offer-panel").waitFor({state:"visible"});
    assert.equal(await page.locator("#workspace").isVisible(),false);
    assert.equal(await page.locator("#enroll-secret").innerText(),"");
    await page.locator("#enroll-start").click();
    await page.locator("#enroll-panel").waitFor({state:"visible"});
    // Validate only local in-browser display of the fictional setup secret.
    assert.ok((await page.locator("#enroll-secret").innerText()).length>=16);
    await page.locator("#enroll-code").fill("123456");
    await page.locator("#enroll-verify").click();
    await page.locator("#workspace").waitFor({state:"visible"});
    assert.equal(await page.locator("#enroll-secret").innerText(),"");
    assert.equal(await page.locator("#enroll-panel").isVisible(),false);
  }else{
    if(scenario==="sensitive-only"||scenario==="unrostered"){
      await page.locator("#mfa-panel").waitFor({state:"visible"});
      assert.equal(await page.locator("#workspace").isVisible(),false);
    }else{
      await page.locator("#workspace").waitFor({state:"visible"});
      const names=await page.locator("#queue button strong").allTextContents();
      assert.equal(names.length,1);
      assert.match(names[0],/NORMAL/);
      await page.locator("#verify-mfa").waitFor({state:"visible"});
      await page.locator("#verify-mfa").click();
      await page.locator("#mfa-panel").waitFor({state:"visible"});
    }
    await page.locator("#totp").fill("123456");
    await page.locator("#mfa-button").click();
    if(scenario==="unrostered"){
      await page.locator("#identity-panel").waitFor({state:"visible"});
      assert.equal(await page.locator("#identity-id").innerText(),uuid(99));
      assert.equal(await page.locator("#workspace").isVisible(),false);
      assert.equal(await page.locator("#contact-fields").innerText(),"");
      assert.equal(writes.length,0);
      await page.locator("#identity-signout").click();
      await page.locator("#signin-panel").waitFor({state:"visible"});
      assert.equal(await page.locator("#identity-id").innerText(),"");
      assert.deepEqual(accidentalEgress,[]);
      assert.deepEqual(pageErrors,[]);
      await context.close();
      return;
    }
    await page.locator("#workspace").waitFor({state:"visible"});
  }
  assert.equal(await page.locator("#mfa-panel").isVisible(),false);
  assert.equal(await page.locator("#verify-mfa").isVisible(),false);
  const names=await page.locator("#queue button strong").allTextContents();
  assert.equal(names.length,2);
  assert.match(names[0],/CRITICAL/);
  assert.match(names[1],/NORMAL/);

  await page.locator("#queue button").first().click();
  await page.locator("#case-detail").waitFor({state:"visible"});
  assert.match(await page.locator("#contact-fields").innerText(),/Fictional Operator Fixture/);
  assert.ok((await page.locator("#contact-fields").innerText()).includes(threat));
  assert.ok((await page.locator("#events").innerText()).includes(threat));
  assert.equal(await page.locator("img").count(),0);
  assert.equal(await page.evaluate(()=>window.__TASK21_XSS===true),false);
  assert.equal(await page.locator("#save").isEnabled(),false);
  if(scenario==="claim-enabled"){
    await page.locator("#claim-case").waitFor({state:"visible"});
    await page.locator("#claim-case").click();
    // The prior detail is visible during async queue refresh. Wait for the
    // *new authenticated server readback*, not just visibility of the panel.
    await page.waitForFunction(()=>document.querySelector("#case-fields")
      ?.textContent?.includes("TRIAGED"),{timeout:15000});
    assert.match(await page.locator("#case-fields").innerText(),/TRIAGED/);
    assert.equal(await page.locator("#owner-id").inputValue(),uuid(99));
    assert.equal(await page.locator("#save").isEnabled(),true);
    assert.equal(claimed,true);
  }else{
    assert.equal(await page.locator("#claim-case").isVisible(),false);
  }
  await page.locator("#case-search").fill(resolved.case_id);
  await page.locator("#case-search-button").click();
  await page.locator("#case-detail").waitFor({state:"visible"});
  assert.match(await page.locator("#case-fields").innerText(),/CLOSED/);

  if(scenario==="dual-role"){
    await page.screenshot({path:path.join(artifactDir,"task21-operator-synthetic-browser.png"),
      fullPage:true});
  }
  await page.locator("#signout").click();
  await page.locator("#signin-panel").waitFor({state:"visible"});
  assert.equal(await page.locator("#workspace").isVisible(),false);
  assert.equal(await page.locator("#contact-fields").innerText(),"");
  assert.equal(writes.length,scenario==="claim-enabled"?1:0,
    "Only one audited self-claim may be sent in the opt-in scenario");
  assert.deepEqual(accidentalEgress,[],"Browser must never reach the Internet");
  assert.deepEqual(pageErrors,[],"Browser must have no uncaught errors");
  await context.close();
}
(async()=>{
  const binary=process.env.CHROMIUM_PATH;
  assert.ok(binary,"CHROMIUM_PATH is required for offline browser acceptance");
  const browser=await chromium.launch({headless:true,executablePath:binary,
    args:["--no-sandbox","--disable-dev-shm-usage"]});
  try{
    await runScenario(browser,"dual-role");
    await runScenario(browser,"sensitive-only");
    await runScenario(browser,"claim-enabled");
    await runScenario(browser,"enroll-first");
    await runScenario(browser,"unrostered");
    console.log("TASK21_OPERATOR_BROWSER_PASS: synthetic staging Auth identity handoff for unrostered user; first-time MFA enrollment, sensitive role denial, self-claim, audit, logout, XSS and no network egress.");
  }finally{await browser.close();}
})().catch(err=>{console.error(err);process.exitCode=1;});
