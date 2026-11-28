// Shared browser/Node helper for the existing offline Creative Desk.
// The manifest describes bytes and held state, not signed approval.
export async function buildDeskManifest(pack) {
  const expected = { "square.svg":[1080,1080], "story.svg":[1080,1920], "landscape.svg":[1920,1080] };
  if (!Array.isArray(pack) || pack.length !== 3) throw Error("Expected three SVG drafts.");
  if (!globalThis.crypto?.subtle) throw Error("Local SHA-256 unavailable in this browser.");
  const seen = new Set(), files = [];
  for (const asset of pack) {
    if (!asset || !Object.hasOwn(expected,asset.name) || seen.has(asset.name) ||
        asset.width !== expected[asset.name][0] || asset.height !== expected[asset.name][1] ||
        typeof asset.svg !== "string") throw Error("Invalid draft format.");
    seen.add(asset.name);
    const bytes = new TextEncoder().encode(asset.svg);
    if (!bytes.length || bytes.length > 1024*1024) throw Error("Draft exceeds Factory SVG size limit. Use smaller source images.");
    const hash = await globalThis.crypto.subtle.digest("SHA-256",bytes);
    files.push({name:asset.name,width:asset.width,height:asset.height,
      sha256:Array.from(new Uint8Array(hash),x=>x.toString(16).padStart(2,"0")).join("")});
  }
  return {schema:"alpha-manual-creative-export/v1",status:"DRAFT_PENDING_HUMAN_REVIEW",
    delivery:"NOT_SENT",providerCalls:0,providerCostMinor:0,otherCosts:"NOT_MEASURED",files};
}

export async function buildDeskReview(manifestText, decision) {
  if (!["REVISIONS_REQUESTED","REVIEWED_LOCALLY"].includes(decision)) throw Error("Invalid local review decision.");
  const digest = await globalThis.crypto.subtle.digest("SHA-256",new TextEncoder().encode(manifestText));
  return {schema:"alpha.creative-desk.local-review.v1",
    exportManifestSha256:Array.from(new Uint8Array(digest),x=>x.toString(16).padStart(2,"0")).join(""),
    decision,reviewerIdentity:"UNVERIFIED",authorization:"NOT_GRANTED",
    rightsVerified:false,release:"HELD",delivery:"NOT_SENT"};
}
