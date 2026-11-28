import test from "node:test";
import assert from "node:assert/strict";
import {verifyHostedRetry} from "./alpha-p0-fictional-creative-roundtrip.mjs";

test("reject first unverified receipt without issuing a second request", async () => {
 let calls=0;
 await assert.rejects(verifyHostedRetry(async()=>{calls++;return {release:"RELEASED"};}),/Unverified fictional result/);
 assert.equal(calls,1);
});
test("do not retry transport failures", async () => {
 let calls=0;
 await assert.rejects(verifyHostedRetry(async()=>{calls++;throw Error("transport refused");}),/transport refused/);
 assert.equal(calls,1);
});

import {createHash} from "node:crypto";
import {REQUEST} from "./alpha-p0-fictional-creative-roundtrip.mjs";
const sha=b=>createHash("sha256").update(b).digest("hex");
function heldFixture(){
 const artwork=Buffer.alloc(33);
 Buffer.from("89504e470d0a1a0a","hex").copy(artwork);
 artwork.writeUInt32BE(960,16);artwork.writeUInt32BE(540,20);
 const artworkSha256=sha(artwork), compositionSha256=sha("composition"), previewSha256=sha("preview");
 const receipt=Buffer.from(JSON.stringify({schema:"alpha.henry-ford.held-multi-overlay-receipt.v1",compositionSha256,release:"HELD",delivery:"NOT_SENT",dispatchAuthorized:false,providerCalls:0,renderExecuted:true,previewSha256,sourceRenderReceiptSha256:sha("source"),artworkFrame:{sha256:artworkSha256,sourcePreviewSha256:previewSha256,atSeconds:1.5,width:960,height:540},placements:[{}]}));
 return {schema:"alpha.factory-fictional-creative-result/v1",jobId:"cafe0000-0000-4000-8000-000000000001",fictionalOnly:true,requestSha256:sha(REQUEST),compositionSha256,artworkSha256,receiptSha256:sha(receipt),technicalStatus:"TECHNICAL_CHECKS_PASS",release:"HELD",delivery:"NOT_SENT",dispatchAuthorized:false,paymentAuthorized:false,providerCalls:0,artworkBase64:artwork.toString("base64"),receiptBase64:receipt.toString("base64")};
}
test("identical held evidence permits exactly one replay", async()=>{
 const fixture=heldFixture();let calls=0;
 assert.deepEqual(await verifyHostedRetry(async()=>{calls++;return structuredClone(fixture);}),fixture);
 assert.equal(calls,2);
});
test("reject a replay whose evidence differs",async()=>{
 const fixture=heldFixture();let calls=0;
 await assert.rejects(verifyHostedRetry(async()=>{calls++;return calls===1?fixture:{...fixture,unexpected:true};}),/Retry returned different evidence/);
 assert.equal(calls,2);
});
