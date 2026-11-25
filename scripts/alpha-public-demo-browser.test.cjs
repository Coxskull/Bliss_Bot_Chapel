"use strict";
// ALPHA fictional public demo: loopback-only browser proof. No Railway,
// production data, AI provider, account, payment or customer API calls.
const {chromium}=require("playwright");
const assert=require("node:assert/strict");
const fs=require("node:fs/promises");
const path=require("node:path");
const http=require("node:http");
const {spawn}=require("node:child_process");
const {createHash}=require("node:crypto");
const root=path.resolve(__dirname,"..");
const output=path.join(root,"BrowserEvidence");
const port=Number(process.env.ALPHA_DEMO_TEST_PORT||8765);
const host="http://127.0.0.1:"+port;
async function ready(){
 for(let i=0;i<100;i++){
  try{
   const status=await new Promise((resolve,reject)=>{
    const req=http.get(host+"/real-estate-showcase.html",resp=>{resp.resume();resolve(resp.statusCode);});
    req.on("error",reject);req.setTimeout(800,()=>req.destroy(new Error("timeout")));
   });
   if(status===200)return;
  }catch{}
  await new Promise(resolve=>setTimeout(resolve,100));
 }
 throw new Error("Fixture-only local HTTP server did not start");
}
async function go(){
 await fs.mkdir(output,{recursive:true});
 const server=spawn("python3",["-m","http.server",String(port),"--bind","127.0.0.1","--directory",".vercel-portal"],{cwd:root,stdio:"ignore"});
 let browser;
 try{
  await ready();
  browser=await chromium.launch({executablePath:process.env.CHROMIUM_PATH||"/usr/bin/google-chrome",headless:true,args:["--no-sandbox"]});
  const context=await browser.newContext({acceptDownloads:true});
  const page=await context.newPage();
  const errors=[],blocked=[];
  page.on("pageerror",err=>errors.push(err.message));
  await page.route("**/*",route=>{
   const url=route.request().url();
   if(url.startsWith(host+"/"))return route.continue();
   blocked.push(url);return route.abort();
  });
  const response=await page.goto(host+"/real-estate-showcase.html",{waitUntil:"load"});
  assert.equal(response.status(),200);
  await page.waitForFunction(()=>document.getElementById("status").textContent.includes("Existing ALPHA composer loaded"),{timeout:30000});
  const dimensions=await page.locator("#stage").evaluate(canvas=>[canvas.width,canvas.height]);
  assert.deepEqual(dimensions,[1920,1080]);
  const inv=await page.evaluate(()=>window.AlphaAdvertisingRealEstate.inventory);
  for(const count of [2,4,6]){
   assert.equal(inv[String(count)].slots.length,count);
   assert.equal(inv[String(count)].campaignReady,false);
   assert.equal(inv[String(count)].delivery,"NOT_SENT");
   const layout=inv[String(count)],p=layout.protectedCreatorArea;
   assert.equal(layout.slots.reduce((sum,s)=>sum+s.width*s.height,0)+p.width*p.height,1920*1080);
   await page.locator("#layout").selectOption(String(count));
   await page.waitForFunction(expected=>document.querySelector("#stage").dataset.configuration===expected,String(count));
   const downloadPromise=page.waitForEvent("download");
   await page.locator("#png").click();
   const dl=await downloadPromise;
   const filename=path.join(output,"ALPHA-FICTIONAL-"+count+".png");
   await dl.saveAs(filename);
   const png=await fs.readFile(filename);
   assert.equal(png.subarray(0,8).toString("hex"),"89504e470d0a1a0a");
   assert.equal(png.readUInt32BE(16),1920);
   assert.equal(png.readUInt32BE(20),1080);
   assert(png.length>10000);
   console.log("ALPHA_FICTIONAL_PNG_PASS",JSON.stringify({slots:count,width:1920,height:1080,bytes:png.length,sha256:createHash("sha256").update(png).digest("hex"),delivery:"NOT_SENT"}));
  }
  await page.locator("#play").click();
  assert.equal(await page.locator("#webm").isDisabled(),true);
  await page.locator("#play").click();
  assert.equal(await page.locator("#webm").isDisabled(),false);
  await page.locator("#layout").selectOption("cycle");
  const videoPromise=page.waitForEvent("download",{timeout:25000});
  await page.locator("#webm").click();
  assert.equal(await page.locator("#play").isDisabled(),true);
  assert.equal(await page.locator("#layout").isDisabled(),true);
  const videoDownload=await videoPromise;
  const videoFile=path.join(output,"ALPHA-FICTIONAL-2-4-6.webm");
  await videoDownload.saveAs(videoFile);
  const webm=await fs.readFile(videoFile);
  assert.equal(webm.subarray(0,4).toString("hex"),"1a45dfa3");
  assert(webm.length>5000);
  await page.waitForFunction(()=>document.querySelector("#status").textContent.includes("NOT_SENT"),{timeout:20000});
  assert.deepEqual(errors,[]);
  assert.deepEqual(blocked,[]);
  console.log("ALPHA_FICTIONAL_VIDEO_PASS",JSON.stringify({bytes:webm.length,sha256:createHash("sha256").update(webm).digest("hex"),aiCalls:0,externalCalls:blocked.length,delivery:"NOT_SENT"}));
  await context.close();
 }finally{
  if(browser)await browser.close();
  server.kill("SIGTERM");
 }
}
go().catch(err=>{console.error(err);process.exitCode=1});
