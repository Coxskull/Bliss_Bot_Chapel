import test from "node:test";
import assert from "node:assert/strict";
import {spawn} from "node:child_process";
import {createHmac, randomUUID} from "node:crypto";

const project="tools/staging/Alpha346.StripeSandboxReceiver/Alpha346.StripeSandboxReceiver.csproj";
const secret="whsec_synthetic_for_offline_tests_only";
const link="plink_1UZkkuH23maIy3rzRSbuvCtT";
const db=process.env.BLISS_FINANCIAL_TEST_POSTGRES;
assert.ok(db?.includes("Database=financial_test") &&
  /Host=127\.0\.0\.1/.test(db),"Require disposable loopback PostgreSQL test fixture");

const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function start(enabled,port,connection=db) {
  const base="http://127.0.0.1:"+port;
  const p=spawn("dotnet",[
    "run","--no-build","--no-launch-profile","--configuration","Release","--project",project
  ],{
    env:{...process.env,
      ASPNETCORE_URLS:base,
      ASPNETCORE_ENVIRONMENT:"AlphaStripeSandbox",
      ALPHA346_STRIPE_SANDBOX_WEBHOOK_ENABLED:enabled?"true":"false",
      ALPHA346_STRIPE_ACCOUNT_ID:"acct_1UOn83H23maIy3rz",
      ALPHA346_STRIPE_WEBHOOK_SECRET:enabled?secret:"",
      ALPHA346_STRIPE_RECEIPTS_DATABASE:enabled?connection:""
    },
    stdio:["ignore","pipe","pipe"]
  });
  let errors="";
  p.stderr.on("data",buf=>{errors=(errors+buf.toString()).slice(-3000);});
  let exit=null;
  p.on("exit",code=>{exit=code;});
  for(let i=0;i<75;i++) {
    if(exit!==null) break;
    try {
      const response=await fetch(base+"/internal/alpha346/sandbox-health",{
        signal:AbortSignal.timeout(800)
      });
      if(response.status===200 || response.status===404) return {p,base};
    }catch{}
    await pause(100);
  }
  p.kill("SIGTERM");
  throw Error("Synthetic webhook host failed to start: "+errors.slice(-1500));
}
async function shutdown(p) {
  if(p.exitCode!==null) return;
  p.kill("SIGTERM");
  await Promise.race([
    new Promise(resolve=>p.once("exit",resolve)),pause(2500)
  ]);
  if(p.exitCode===null) p.kill("SIGKILL");
}
function syntheticEvent({eventId,sessionId,intentId,
  type="checkout.session.completed",status="paid",paymentLink=link,amount=14900}={}) {
  const id=eventId||"evt_synthetic_http_"+randomUUID().replaceAll("-","");
  const session=sessionId||"cs_test_"+randomUUID().replaceAll("-","");
  const intent=intentId||"pi_synthetic_"+randomUUID().replaceAll("-","");
  return {id,object:"event",type,livemode:false,
    data:{object:{id:session,object:"checkout.session",livemode:false,
      payment_link:paymentLink,payment_intent:intent,mode:"payment",status:"complete",
      payment_status:status,amount_total:amount,currency:"usd",
      metadata:{environment:"SANDBOX_ONLY",payment:"TEST_MODE_ONLY",
        policy_status:"DRAFT",real_customer_use:"PROHIBITED"},
      automatic_tax:{enabled:true,status:"complete"},total_details:{amount_tax:0}}}};
}
function signed(body,timestamp=Math.floor(Date.now()/1000),signSecret=secret){
  return "t="+timestamp+",v1="+createHmac("sha256",signSecret)
    .update(String(timestamp)+".").update(body).digest("hex");
}
async function post(base,body,signature=signed(body)) {
  return fetch(base+"/internal/alpha346/stripe-sandbox-webhook",{
    method:"POST",
    headers:{"content-type":"application/json","stripe-signature":signature},
    body,signal:AbortSignal.timeout(6000)
  });
}
test("ALPHA-346 real local HTTP ingress: enabled only in isolated sandbox and receipts are idempotent",async()=>{
  const disabled=await start(false,40518);
  try {
    assert.equal((await fetch(disabled.base+"/internal/alpha346/sandbox-health")).status,404);
    const ignored=JSON.stringify(syntheticEvent());
    assert.equal((await post(disabled.base,ignored)).status,404);
  }finally{await shutdown(disabled.p);}

  // A valid signed payload must never receive a success ACK if durable storage
  // fails. Use an unreachable loopback port, never any hosted database.
  const unavailable=await start(true,40519,
    db.replace(/(?:^|;)Port=[^;]*/i,"")+";Port=1;Timeout=1;Pooling=false");
  try{
    const body=JSON.stringify(syntheticEvent());
    assert.equal((await post(unavailable.base,body)).status,503,"Persistence failure remains retryable");
    assert.equal((await post(unavailable.base,body,"t=0,v1=invalid")).status,400,"Invalid signatures rejected before DB access");
  }finally{await shutdown(unavailable.p);}

  const enabled=await start(true,40517);
  try{
    assert.equal((await fetch(enabled.base+"/internal/alpha346/sandbox-health")).status,200);
    const one=syntheticEvent(),first=JSON.stringify(one);
    assert.equal((await post(enabled.base,first)).status,204,"First signed test receipt persisted");
    assert.equal((await post(enabled.base,first)).status,204,"Identical retry ACK");
    const reevent=JSON.stringify(syntheticEvent({
      eventId:"evt_synthetic_http_"+randomUUID().replaceAll("-",""),
      sessionId:one.data.object.id,intentId:one.data.object.payment_intent
    }));
    assert.equal((await post(enabled.base,reevent)).status,204,"Same payment not double counted");
    const forged=syntheticEvent({eventId:one.id,amount:14901});
    assert.equal((await post(enabled.base,JSON.stringify(forged))).status,400,"Changed amount denied");
    const wrongLink=JSON.stringify(syntheticEvent({paymentLink:"plink_another_account"}));
    assert.equal((await post(enabled.base,wrongLink)).status,400,"Wrong link denied");
    assert.equal((await post(enabled.base,first,signed(first,Math.floor(Date.now()/1000),"whsec_wrong_synthetic_secret"))).status,400,"Forged signature denied");
    assert.equal((await post(enabled.base,first,signed(first,Math.floor(Date.now()/1000)-301))).status,400,"Expired signature denied");
    // Valid HMAC does not make malformed JSON types or impossible tax valid.
    // These permanent input failures must not masquerade as retryable DB errors.
    const malformed=[null, [], {livemode:"false"}, {livemode:false,object:123}];
    const wrongTax=syntheticEvent();wrongTax.data.object.total_details.amount_tax=14901;
    const wrongAmountType=syntheticEvent();wrongAmountType.data.object.amount_total="14900";
    const expired=syntheticEvent({type:"checkout.session.expired",status:"unpaid"});
    expired.data.object.status="expired";
    for(const event of [...malformed,wrongTax,wrongAmountType,expired]){
      const response=await post(enabled.base,JSON.stringify(event));
      assert.equal(response.status,400,"Signed invalid payload rejected without persistence");
      const text=await response.text();
      assert.ok(!text.includes(secret) && !text.includes("payment_intent"),"No secret/payload in error response");
    }
    const conflict=JSON.stringify(syntheticEvent({
      eventId:one.id,sessionId:one.data.object.id,
      intentId:one.data.object.payment_intent,type:"checkout.session.async_payment_failed",status:"unpaid"
    }));
    assert.equal((await post(enabled.base,conflict)).status,409,"Same event conflicting state HELD");
    const pending=syntheticEvent({status:"unpaid"}),pendingJson=JSON.stringify(pending);
    assert.equal((await post(enabled.base,pendingJson)).status,204,"Unpaid test receipt retained");
    const laterPaid=JSON.stringify(syntheticEvent({
      sessionId:pending.data.object.id,intentId:pending.data.object.payment_intent
    }));
    assert.equal((await post(enabled.base,laterPaid)).status,409,"Pending to paid requires review");

    const simultaneous=JSON.stringify(syntheticEvent());
    const outcomes=await Promise.all(Array.from({length:12},()=>post(enabled.base,simultaneous)));
    assert.deepEqual(outcomes.map(x=>x.status),Array(12).fill(204),"12 parallel webhooks ACK only when deduplicated");
    console.log("ALPHA-346 SANDBOX HTTP PASS: disabled 404, signed receipt, replay, forged/stale 400, conflict 409, 12 concurrent 204; no fulfillment");
  }finally{await shutdown(enabled.p);}
});
