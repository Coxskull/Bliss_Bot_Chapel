import assert from "node:assert/strict";
import { createHash, webcrypto } from "node:crypto";
import { renderPack } from "./alpha-manual-creative-export.mjs";
import { buildDeskManifest, buildDeskReview } from "./alpha-creative-desk-manifest.mjs";
if (!globalThis.crypto) Object.defineProperty(globalThis,"crypto",{value:webcrypto});
const pack=renderPack({brand:"Fictional Brand",headline:"Sample headline",description:"Sample description.",cta:"Learn more",palette:"midnight",rightsConfirmed:true});
const manifest=await buildDeskManifest(pack);
assert.equal(manifest.status,"DRAFT_PENDING_HUMAN_REVIEW");
assert.equal(manifest.providerCalls,0);
assert.equal(manifest.delivery,"NOT_SENT");
for(const file of manifest.files) {
  assert.equal(file.sha256,createHash("sha256").update(pack.find(x=>x.name===file.name).svg).digest("hex"));
}
const text=JSON.stringify(manifest,null,2)+"\n";
for(const decision of ["REVISIONS_REQUESTED","REVIEWED_LOCALLY"]) {
  const review=await buildDeskReview(text,decision);
  assert.equal(review.exportManifestSha256,createHash("sha256").update(text).digest("hex"));
  assert.equal(review.authorization,"NOT_GRANTED");
  assert.equal(review.release,"HELD");
}
await assert.rejects(buildDeskReview(text,"APPROVED"));
await assert.rejects(buildDeskManifest([pack[0],pack[0],pack[2]]));
await assert.rejects(buildDeskManifest([{...pack[0],width:1},...pack.slice(1)]));
await assert.rejects(buildDeskManifest([{...pack[0],svg:"x".repeat(1024*1024+1)},...pack.slice(1)]));
await assert.rejects(buildDeskManifest([{...pack[0],name:"../square.svg"},...pack.slice(1)]));
console.log("PASS: exact draft bytes, held review, invalid pack refusal");
