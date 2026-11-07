#!/usr/bin/env node
"use strict";
// Only assembles private static files in an isolated directory. No deployment,
// secrets, SMTP, CRM, customer data, or Supabase calls.
const fs=require("node:fs");
const path=require("node:path");
const crypto=require("node:crypto");
const root=path.resolve(__dirname,"../..");
const source=path.join(root,"frontend/private/support-operator");
const out=path.join(root,".task21-private-build");
const esbuild=require("esbuild");
const sdkName="@supabase/supabase-js";
if(esbuild.version!=="0.25.12")throw Error("Unapproved esbuild build version");
// Resolve only the pinned Supabase SDK from the ephemeral CI tool installation.
let sdkDir=path.dirname(require.resolve(sdkName)),sdkPackage=null;
while(sdkDir!==path.dirname(sdkDir)){
  const candidate=path.join(sdkDir,"package.json");
  if(fs.existsSync(candidate)){
    const pkg=JSON.parse(fs.readFileSync(candidate,"utf8"));
    if(pkg.name===sdkName){sdkPackage=pkg;break;}
  }
  sdkDir=path.dirname(sdkDir);
}
if(!sdkPackage||sdkPackage.version!=="2.95.0")
  throw Error("Unapproved Supabase SDK build version");
if(fs.existsSync(out))
  throw Error("Existing private bundle output: refusing to overwrite or package secrets");
fs.mkdirSync(out,{recursive:false,mode:0o700});
const allowlist=["index.html","operator.js","operator-view-model.mjs","operator.css"];
for(const filename of allowlist) {
  const input=path.join(source,filename),stat=fs.lstatSync(input);
  if(!stat.isFile()||stat.isSymbolicLink())throw Error("Unsafe source asset");
  fs.copyFileSync(input,path.join(out,filename),fs.constants.COPYFILE_EXCL);
}
const html=fs.readFileSync(path.join(out,"index.html"),"utf8");
const app=fs.readFileSync(path.join(out,"operator.js"),"utf8");
if(html.includes("cdn.jsdelivr.net")||
   !app.includes('from "./operator-sdk.js"'))
   throw Error("External runtime SDK or unexpected import");
const nodePaths=(process.env.NODE_PATH||"").split(path.delimiter).filter(Boolean);
const sdkBundle=path.join(out,"operator-sdk.js");
(async()=>{
  await esbuild.build({
    stdin:{contents:'export { createClient } from "@supabase/supabase-js";',
      resolveDir:root,sourcefile:"task21-supabase-sdk-entry.js",loader:"js"},
    bundle:true,platform:"browser",format:"esm",target:["es2022"],
    minify:true,legalComments:"none",write:true,logLevel:"warning",
    nodePaths, outfile:sdkBundle
  });
  const sdk=fs.readFileSync(sdkBundle,"utf8");
  if(!sdk.includes("createClient")||sdk.includes("cdn.jsdelivr.net"))
    throw Error("Private runtime SDK was not locally bundled");
  const files=[...allowlist,"operator-sdk.js"];
  const hashes=Object.fromEntries(files.map(filename=>[
    filename,crypto.createHash("sha256")
      .update(fs.readFileSync(path.join(out,filename))).digest("hex")
  ]));
  const manifest={
    purpose:"TASK21_PRIVATE_OPERATOR_SOURCE_BUILD_NOT_DEPLOYED",
    project:"ALPHA GLOBAL MEDIA LLC",
    sdk:{name:sdkName,version:sdkPackage.version},
    bundler:{name:"esbuild",version:esbuild.version},
    files:hashes,
    operatorConfigIncluded:false,
    secretMaterialIncluded:false,
    liveDeployment:false,
    releaseRequired:["approved origin","staff roster & MFA","staffed incident alerts",
      "hosted migration acceptance","approved CSP response headers","owner signoff"]
  };
  fs.writeFileSync(path.join(out,"manifest.json"),
    JSON.stringify(manifest,null,2)+"\n",{mode:0o600,flag:"wx"});
  const headers={
    "Cache-Control":"no-store",
    "Content-Security-Policy":"default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self' https://bkutbglzivfdnoerigyb.supabase.co; img-src 'self'; font-src 'self'; base-uri 'none'; form-action 'self'; object-src 'none'; frame-ancestors 'none'; upgrade-insecure-requests",
    "Strict-Transport-Security":"max-age=31536000; includeSubDomains",
    "X-Content-Type-Options":"nosniff",
    "Referrer-Policy":"no-referrer",
    "X-Frame-Options":"DENY",
    "Permissions-Policy":"camera=(), microphone=(), geolocation=()"
  };
  fs.writeFileSync(path.join(out,"REQUIRED_HEADERS.json"),
    JSON.stringify(headers,null,2)+"\n",{mode:0o600,flag:"wx"});
  if(fs.readdirSync(out).some(name=>/operator-config\.js|\.env|secret/i.test(name)))
    throw Error("Private build accidentally includes configuration or secret");
  console.log("TASK21_PRIVATE_BUNDLE_PASS: pinned SDK locally bundled; no CDN, no operator config, no deploy; "+files.length+" verified assets.");
})().catch(e=>{console.error(e);process.exitCode=1;});
