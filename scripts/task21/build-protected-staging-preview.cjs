#!/usr/bin/env node
"use strict";
// Private operator PREVIEW ONLY. Uses the existing Bliss Bot Vercel project,
// its Vercel Authentication-protected preview URLs, and the already linked
// staging Supabase branch. Never copies operator assets to a production build.
const assert=require("node:assert/strict");
const fs=require("node:fs");
const os=require("node:os");
const path=require("node:path");
const crypto=require("node:crypto");
const cp=require("node:child_process");
const root=path.resolve(__dirname,"../..");
const output=path.join(root,".vercel-portal","staff","task21");
const bundle=path.join(root,".task21-private-build");
const exactBranch="feat/agm-task21-support-cases-20261108";
const stage="https://wdlmcriajgsretgyusmo.supabase.co";
function fail(reason){throw Error("Task21 staging preview refused: "+reason);}
if(process.env.VERCEL_ENV!=="preview"||
   process.env.VERCEL_GIT_COMMIT_REF!==exactBranch||
   process.env.ALPHA_TASK21_STAGE_PREVIEW!=="enabled")
  fail("not an explicitly enabled, exact-branch Vercel preview");
const host=process.env.VERCEL_URL;
if(typeof host!=="string"||!/^[-a-z0-9]+(?:\.[-a-z0-9]+)*\.vercel\.app$/.test(host))
  fail("exact protected preview hostname unavailable");
const origin="https://"+host;
const key=process.env.ALPHA_TASK21_STAGING_PUBLISHABLE_KEY;
if(typeof key!=="string"||!/^sb_publishable_[A-Za-z0-9_-]{12,}$/.test(key))
  fail("valid staging publishable key not configured");
// Existing preview deployments require Vercel Authentication: project SSO
// setting is an operations gate checked via Vercel API before enabling.
if(fs.existsSync(bundle)||fs.existsSync(output))fail("existing output unexpected");
const tmp=fs.mkdtempSync(path.join(os.tmpdir(),"task21-preview-sdk-"));
try{
  cp.execFileSync(process.platform==="win32"?"npm.cmd":"npm",[
    "install","--prefix",tmp,"--no-package-lock","--no-audit",
    "--no-fund","--save-exact","@supabase/supabase-js@2.95.0",
    "esbuild@0.25.12"
  ],{cwd:root,timeout:150000,stdio:["ignore","pipe","pipe"]});
  cp.execFileSync(process.execPath,[
    path.join(root,"scripts/task21/build-private-operator.cjs")
  ],{cwd:root,env:{...process.env,NODE_PATH:path.join(tmp,"node_modules")},
    timeout:60000,stdio:["ignore","pipe","pipe"]});
  const manifest=JSON.parse(fs.readFileSync(path.join(bundle,"manifest.json"),"utf8"));
  assert.equal(manifest.purpose,"TASK21_PRIVATE_OPERATOR_SOURCE_BUILD_NOT_DEPLOYED");
  assert.equal(manifest.sdk.version,"2.95.0");
  assert.equal(manifest.bundler.version,"0.25.12");
  assert.equal(manifest.operatorConfigIncluded,false);
  for(const [name,expected] of Object.entries(manifest.files)){
    assert.match(name,/^(?:index\.html|operator\.css|operator\.js|operator-view-model\.mjs|operator-sdk\.js)$/);
    assert.equal(crypto.createHash("sha256")
      .update(fs.readFileSync(path.join(bundle,name))).digest("hex"),expected);
  }
  fs.mkdirSync(output,{recursive:true,mode:0o700});
  for(const name of Object.keys(manifest.files))fs.copyFileSync(
    path.join(bundle,name),path.join(output,name),fs.constants.COPYFILE_EXCL);
  // Public key only, never an operator roster, token, secret or customer data.
  const config={environment:"staging",supabaseUrl:stage,
    publishableKey:key,operatorOrigin:origin,mutationsEnabled:false};
  fs.writeFileSync(path.join(output,"operator-config.js"),
    "window.ALPHA_SUPPORT_OPERATOR_CONFIG = Object.freeze("+
      JSON.stringify(config)+");\n",{flag:"wx",mode:0o600});
  if(fs.readFileSync(path.join(output,"index.html"),"utf8")
     .includes("bkutbglzivfdnoerigyb"))fail("production project leaked to staging");
  const check=fs.readdirSync(output).sort();
  assert.deepEqual(check,[
    "index.html","operator-config.js","operator-sdk.js","operator-view-model.mjs",
    "operator.css","operator.js"].sort());
  console.log("TASK21_VERCEL_PROTECTED_STAGING_PREVIEW_BUILT: "+
    "staging-only HTTPS origin, read-only, same-origin SDK, no sensitive keys.");
}finally{
  // Never copy npm package files or cache into publicly served output.
  fs.rmSync(tmp,{recursive:true,force:true});
  fs.rmSync(bundle,{recursive:true,force:true});
}
