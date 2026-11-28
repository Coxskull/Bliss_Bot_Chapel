// Operator-only fictional staging client. No production route or customer delivery.
import {createHash,createHmac} from "node:crypto";
import {readFile,writeFile,lstat,realpath} from "node:fs/promises";
import {join,isAbsolute} from "node:path";
import {pathToFileURL} from "node:url";
const JOB="cafe0000-0000-4000-8000-000000000001";
export const REQUEST=JSON.stringify({schema:"alpha.bliss-fictional-creative/v1",jobId:JOB,fixture:"ALPHA_P0_FICTIONAL_ONLY"});
const URL="https://alpha-hf-factory-engine-staging.up.railway.app/internal/bliss/fictional-creative-roundtrip";
const sha=b=>createHash("sha256").update(b).digest("hex");
const hex=x=>typeof x==="string"&&/^[a-f0-9]{64}$/.test(x);
function bytes(s,max){
 if(typeof s!=="string"||s.length>Math.ceil(max/3)*4)throw Error("Invalid result bytes");
 const b=Buffer.from(s,"base64");
 if(!b.length||b.length>max||b.toString("base64")!==s)throw Error("Invalid result encoding");
 return b;
}
export function verifyResult(r){
 if(r?.schema!=="alpha.factory-fictional-creative-result/v1"||r.jobId!==JOB||r.fictionalOnly!==true||r.requestSha256!==sha(REQUEST)||
    !hex(r.compositionSha256)||!hex(r.artworkSha256)||!hex(r.receiptSha256)||r.technicalStatus!=="TECHNICAL_CHECKS_PASS"||
    r.release!=="HELD"||r.delivery!=="NOT_SENT"||r.dispatchAuthorized!==false||r.paymentAuthorized!==false||r.providerCalls!==0)throw Error("Unverified fictional result");
 const artwork=bytes(r.artworkBase64,2_000_000),receipt=bytes(r.receiptBase64,100_000);
 if(sha(artwork)!==r.artworkSha256||sha(receipt)!==r.receiptSha256||artwork.length<33||artwork.subarray(0,8).toString("hex")!=="89504e470d0a1a0a"||artwork.readUInt32BE(16)!==960||artwork.readUInt32BE(20)!==540)throw Error("Artwork or receipt mismatch");
 const q=JSON.parse(receipt.toString("utf8"));
 if(q.schema!=="alpha.henry-ford.held-multi-overlay-receipt.v1"||q.compositionSha256!==r.compositionSha256||q.release!=="HELD"||q.delivery!=="NOT_SENT"||q.dispatchAuthorized!==false||q.providerCalls!==0||q.renderExecuted!==true||!hex(q.previewSha256)||!hex(q.sourceRenderReceiptSha256)||q.artworkFrame?.sha256!==r.artworkSha256||q.artworkFrame?.sourcePreviewSha256!==q.previewSha256||q.artworkFrame?.atSeconds!==1.5||q.artworkFrame?.width!==960||q.artworkFrame?.height!==540||q.placements?.length!==1)throw Error("Held receipt mismatch");
 return {artwork,receipt};
}
async function send(env){
 if(env.BLISS_FICTIONAL_ROUNDTRIP!=="YES"||
    env.RAILWAY_PROJECT_ID!=="43062727-bca5-400a-b801-27828161324b"||
    env.RAILWAY_ENVIRONMENT_ID!=="04edd217-035e-4c18-b864-9b7ded76de7d"||
    env.RAILWAY_SERVICE_ID!=="990e3d3e-b09a-4c43-8873-ab10e9bffd02"||
    !hex(env.BLISS_FICTIONAL_ROUNDTRIP_HMAC_KEY))throw Error("Exact Bliss staging configuration required");
 const timestamp=String(Math.floor(Date.now()/1000));
 const signature=createHmac("sha256",Buffer.from(env.BLISS_FICTIONAL_ROUNDTRIP_HMAC_KEY,"hex")).update("alpha-fictional-roundtrip-v1\n"+timestamp+"\n").update(REQUEST).digest("hex");
 const res=await fetch(URL,{method:"POST",redirect:"error",headers:{"content-type":"application/json","x-alpha-timestamp":timestamp,"x-alpha-signature":signature},body:REQUEST,signal:AbortSignal.timeout(240000)});
 if(!res.ok)throw Error("Factory refused fictional submission: "+res.status);
 const reader=res.body.getReader();let size=0;const chunks=[];
 try{while(true){const {value,done}=await reader.read();if(done)break;size+=value.byteLength;if(size>4_000_000)throw Error("Oversize response");chunks.push(Buffer.from(value));}}
 finally{await reader.cancel().catch(()=>{});}
 return JSON.parse(Buffer.concat(chunks).toString("utf8"));
}
async function save(path,data){
 try{await writeFile(path,data,{flag:"wx",mode:0o600});}
 catch(e){if(e.code!=="EEXIST")throw e;const s=await lstat(path);if(!s.isFile()||s.isSymbolicLink()||s.size!==data.length||!(await readFile(path)).equals(data))throw Error("Existing evidence differs");}
}
// One fixed fictional metadata-only job through the real restricted intake ledger.
// The same IDs and body are reused on restart; no asset, render, booking or payment.
async function heldAdmission(env,artwork=false){
 if(env.BLISS_FICTIONAL_HELD_ADMISSION!=="YES"||
    env.RAILWAY_PROJECT_ID!=="43062727-bca5-400a-b801-27828161324b"||
    env.RAILWAY_ENVIRONMENT_ID!=="04edd217-035e-4c18-b864-9b7ded76de7d"||
    env.RAILWAY_SERVICE_ID!=="990e3d3e-b09a-4c43-8873-ab10e9bffd02"||
    env.Runtime__RestrictedPreview!=="true"||env.Authentication__Enabled!=="false"||
    !hex(env.BLISS_FACTORY_CREATIVE_INTAKE_HMAC_KEY))throw Error("HELD_ADMISSION_SCOPE_REFUSED");
 if(artwork&&(!hex(env.BLISS_FACTORY_PRIVATE_ARTWORK_HMAC_KEY)||env.BLISS_FACTORY_PRIVATE_ARTWORK_HMAC_KEY===env.BLISS_FACTORY_CREATIVE_INTAKE_HMAC_KEY))throw Error("ARTWORK_KEY_REFUSED");
 const png=Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFklEQVR4nGMUCVjAwMDAxMDAwMDAAAALzgEI/t/+UgAAAABJRU5ErkJggg==","base64");
 const ids=["handoffId","placementId","campaignId","creatorId","slotId","candidateId","reviewId","advertiserApprovalId","alphaQaApprovalId","inventoryFitId","quoteVersionId","quoteLineId","agreementEvidenceId"];
 const m=Object.fromEntries(ids.map((k,i)=>[k,(artwork?"cafe2000-0000-4000-8000-":"cafe1000-0000-4000-8000-")+String(i+1).padStart(12,"0")]));
 Object.assign(m,{imageSha256:artwork?sha(png):sha("ALPHA_FICTIONAL_IMAGE_NOT_TRANSFERRED"),handoffSha256:sha("ALPHA_FICTIONAL_HANDOFF"),agreementEvidenceSha256:sha("ALPHA_FICTIONAL_AGREEMENT"),schema:"alpha-creator-fulfillment-preparation/v1",status:"PREPARATION_ONLY",delivery:"NOT_SENT",fundingVerified:false,bookingAuthorized:false,paymentAuthorized:false,deliveryAuthorized:false,factoryDispatchAuthorized:false,slotType:"LOWER_THIRD",startSecond:0,durationSeconds:2,imageWidth:artwork?2:960,imageHeight:artwork?2:540});
 const body=JSON.stringify(m),digest=sha(body);
 const base="https://alpha-hf-factory-engine-staging.up.railway.app/internal/bliss/held-creative-jobs";
 async function send(path,readOnly){
  const timestamp=String(Math.floor(Date.now()/1000));
  const signature=createHmac("sha256",Buffer.from(env.BLISS_FACTORY_CREATIVE_INTAKE_HMAC_KEY,"hex")).update((readOnly?"alpha-bliss-held-creative-read-v1":"alpha-bliss-held-creative-v1")+"\n"+timestamp+"\n").update(body).digest("hex");
  const res=await fetch(base+path,{method:"POST",redirect:"error",headers:{"content-type":"application/json","x-alpha-timestamp":timestamp,"x-alpha-signature":signature},body,signal:AbortSignal.timeout(12000)});
  if(!res.ok){await res.body?.cancel();throw Error("HELD_ADMISSION_HTTP_"+res.status);}
  const reader=res.body.getReader(),chunks=[];let size=0;
  try{while(true){const {value,done}=await reader.read();if(done)break;size+=value.byteLength;if(size>16384)throw Error("HELD_ADMISSION_RESPONSE_LIMIT");chunks.push(Buffer.from(value));}}
  finally{await reader.cancel().catch(()=>{});}
  const v=JSON.parse(Buffer.concat(chunks).toString("utf8"));
  if(v.placementId!==m.placementId||v.handoffId!==m.handoffId||v.manifestSha256!==digest||v.release!=="HELD"||v.delivery!=="NOT_SENT"||v.dispatchAuthorized!==false)throw Error("HELD_ADMISSION_RECEIPT_MISMATCH");
  if(path==="/render-status"){
   if(v.schema!=="alpha.henry-ford.held-render-status.v1"||v.visualQa!=="HUMAN_REVIEW_PENDING"||!Array.isArray(v.results)||v.results.length!==0)throw Error("HELD_STATUS_RECEIPT_MISMATCH");
  }else if(v.schema!=="alpha.henry-ford.held-job-admission.v1"||v.status!=="HELD_METADATA_ONLY"||v.renderAuthorized!==false||v.paymentAuthorized!==false||!Number.isFinite(Date.parse(v.admittedAt)))throw Error("HELD_ADMISSION_RECEIPT_MISMATCH");
  return v;
 }
 const admitted=await send("",false),again=await send("/readback",true);
 if(admitted.admittedAt!==again.admittedAt)throw Error("HELD_ADMISSION_READBACK_MISMATCH");
 await send("/render-status",true);
 if(artwork){
  const mb=Buffer.from(body),len=Buffer.alloc(4);len.writeUInt32BE(mb.length);const payload=Buffer.concat([len,mb,png]);
  const timestamp=String(Math.floor(Date.now()/1000));
  const signature=createHmac("sha256",Buffer.from(env.BLISS_FACTORY_PRIVATE_ARTWORK_HMAC_KEY,"hex")).update("alpha-bliss-held-artwork-v1\n"+timestamp+"\n").update(payload).digest("hex");
  const res=await fetch("https://alpha-hf-factory-engine-staging.up.railway.app/internal/bliss/held-creative-artwork",{method:"POST",redirect:"error",headers:{"content-type":"application/octet-stream","x-alpha-timestamp":timestamp,"x-alpha-signature":signature},body:payload,signal:AbortSignal.timeout(12000)});
  if(!res.ok){await res.body?.cancel();throw Error("ARTWORK_CUSTODY_HTTP_"+res.status);}
  const reader=res.body.getReader(),chunks=[];let size=0;
  try{while(true){const {value,done}=await reader.read();if(done)break;size+=value.byteLength;if(size>4096)throw Error("ARTWORK_RESPONSE_LIMIT");chunks.push(Buffer.from(value));}}finally{await reader.cancel().catch(()=>{});}
  const v=JSON.parse(Buffer.concat(chunks).toString("utf8"));
  if(v.schema!=="alpha.henry-ford.held-artwork-custody.v1"||v.placementId!==m.placementId||v.handoffId!==m.handoffId||v.manifestSha256!==digest||v.imageSha256!==sha(png)||v.imageWidth!==2||v.imageHeight!==2||v.status!=="HELD_ARTWORK_STORED"||v.release!=="HELD"||v.delivery!=="NOT_SENT"||v.renderAuthorized!==false||v.dispatchAuthorized!==false||v.paymentAuthorized!==false||!Number.isFinite(Date.parse(v.receivedAt)))throw Error("ARTWORK_RECEIPT_MISMATCH");
  console.log(JSON.stringify({marker:"BLISS_FICTIONAL_ARTWORK_CUSTODY_PASS",placementId:m.placementId,imageSha256:sha(png),delivery:"NOT_SENT"}));
 }
 console.log(JSON.stringify({marker:"BLISS_HELD_ADMISSION_PASS",placementId:m.placementId,manifestSha256:digest,admission:"HELD_METADATA_ONLY",statusReadback:"PASS",delivery:"NOT_SENT"}));
}
async function main(){
 const args=process.argv.slice(2);
 if(args.length===1&&args[0]==="--held-admission")return heldAdmission(process.env,process.env.BLISS_FICTIONAL_ARTWORK_CUSTODY==="YES");
 if(args[0]!=="--hosted"&&args[0]!=="--verify-local")throw Error("Use --hosted OUTPUT or --verify-local RESULT OUTPUT");
 const local=args[0]==="--verify-local",root=args[local?2:1];
 if(args.length!==(local?3:2)||!root||!isAbsolute(root))throw Error("Existing absolute private output directory required");
 const s=await lstat(root);if(!s.isDirectory()||s.isSymbolicLink()||s.mode&0o077||await realpath(root)!==root)throw Error("Private directory required");
 let r;
 if(local){const stat=await lstat(args[1]);if(!stat.isFile()||stat.isSymbolicLink()||stat.size>4_000_000)throw Error("Invalid local fixture response");r=JSON.parse(await readFile(args[1],"utf8"));}
 else{r=await send(process.env);const again=await send(process.env);verifyResult(again);if(JSON.stringify(r)!==JSON.stringify(again))throw Error("Retry returned different evidence");}
 const {artwork,receipt}=verifyResult(r);
 await save(join(root,"artwork.png"),artwork);await save(join(root,"receipt.json"),receipt);
 await save(join(root,"preview.html"),Buffer.from('<!doctype html><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src &apos;none&apos;; img-src &apos;self&apos;; style-src &apos;unsafe-inline&apos;"><title>Bliss fictional preview</title><h1>FICTIONAL ONLY — HELD</h1><p>Job '+JOB+'</p><img width="960" height="540" src="artwork.png" alt="Private fictional rendered frame"><p>Receipt SHA-256: '+r.receiptSha256+'</p><p>Not sent; no payment or production delivery.</p>'));
 console.log(JSON.stringify({jobId:JOB,artworkSha256:r.artworkSha256,receiptSha256:r.receiptSha256,compositionSha256:r.compositionSha256,execution:local?"LOCAL_FIXTURE_READBACK":"HOSTED_STAGING_RETRY_VERIFIED",delivery:"NOT_SENT"}));
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href)main().catch(e=>{console.error(e.message);process.exitCode=1});
