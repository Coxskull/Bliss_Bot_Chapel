(() => {
  "use strict";
  const el = id => document.getElementById(id);
  const status = el("status"), panel = el("panel"), file = el("manifest"), summary = el("summary");
  const review = el("review"), prepare = el("prepare"), recipe = el("recipe"), save = el("save");
  const fields = ["family","headline","cta"].map(el);
  const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
  const hex = /^[0-9a-f]{64}$/;
  let manifest = null, brand = null, busy = false, epoch = 0;
  const get = path => fetch(path,{credentials:"same-origin",cache:"no-store",headers:{"Accept":"application/json"}});
  async function session() {
    const s = await get("/api/auth/session");
    if (!s.ok) throw Error("Sign in with an authorized operator account.");
    const data = await s.json();
    if (!data.isAuthenticated || typeof data.csrfToken !== "string" || !data.csrfToken)
      throw Error("Sign in with an authorized operator account.");
    const ready = await get("/api/internal/creative-readiness");
    if (!ready.ok) throw Error("Staff creative preparation is unavailable or access is not authorized.");
    return data.csrfToken;
  }
  function sync() {
    file.disabled = busy; review.disabled = busy;
    prepare.disabled = busy || !manifest || !review.checked;
    save.disabled = busy || !manifest || !review.checked || !brand || fields.some(f => !f.value.trim());
    fields.forEach(f => f.disabled = busy);
  }
  function reset() {
    manifest = brand = null; review.checked = false;
    summary.hidden = recipe.hidden = true; fields.forEach(f => f.value = "");
    ["campaign-name","campaign-id","brief"].forEach(id => el(id).textContent = "");
    el("result").textContent = "No preparation requested."; sync();
  }
  file.addEventListener("change", async () => {
    const current = ++epoch; reset();
    const f = file.files[0]; if (!f) return;
    try {
      if (f.size > 32768) throw Error("Manifest exceeds the permitted size.");
      const m = JSON.parse(await f.text());
      if (!m || m.schema !== "alpha.advertiser.campaign-creative-handoff.v1"
          || !uuid.test(m.campaignId) || !hex.test(m.briefSha256)
          || typeof m.campaignName !== "string" || !m.campaignName.trim() || m.campaignName.length > 256
          || typeof m.creativeBrief !== "string" || !m.creativeBrief.trim() || m.creativeBrief.length > 4000
          || m.status !== "DRAFT" || m.authorityStatus !== "UNVERIFIED" || m.release !== "HELD"
          || m.delivery !== "NOT_SENT" || m.modelCall !== "NOT_REQUESTED" || m.signedAuthorization !== false)
        throw Error("Not a valid held advertiser QA manifest.");
      const bytes = new TextEncoder().encode(m.campaignId + "\n" + m.campaignName + "\n" + m.creativeBrief);
      const digest = Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256",bytes)),b=>b.toString(16).padStart(2,"0")).join("");
      if (digest !== m.briefSha256) throw Error("Campaign checksum does not match.");
      if (current !== epoch) return;
      manifest = {campaignId:m.campaignId,briefSha256:digest};
      el("campaign-name").textContent = m.campaignName;
      el("campaign-id").textContent = m.campaignId;
      el("brief").textContent = m.creativeBrief;
      summary.hidden = false; status.textContent = "Manifest checksum matches. Review the brief before preparing.";
    } catch(e) { if (current === epoch) status.textContent = e instanceof SyntaxError ? "Invalid JSON manifest." : e.message; }
    finally { if (current === epoch) sync(); }
  });
  async function submit(withRecipe) {
    if (busy || !manifest || !review.checked || (withRecipe && (!brand || fields.some(f=>!f.value.trim())))) return;
    const current = epoch; busy = true; sync(); status.textContent = "Preparing the selected campaign…";
    try {
      const csrf = await session();
      if (current !== epoch) return;
      const payload = {...manifest,operatorReviewedCampaign:true};
      if (withRecipe) Object.assign(payload,{brandDnaId:brand,family:fields[0].value.trim(),headline:fields[1].value.trim(),cta:fields[2].value.trim()});
      const response = await fetch("/api/internal/creative-preparation",{method:"POST",credentials:"same-origin",cache:"no-store",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},body:JSON.stringify(payload)});
      if (current !== epoch) return;
      if (!response.ok) throw Error("Preparation not accepted (HTTP " + response.status + "). Check current campaign, permissions and consent.");
      const result = await response.json();
      if (result.campaignId !== manifest.campaignId || result.briefSha256 !== manifest.briefSha256
          || !uuid.test(result.workspaceId) || result.providerCall !== "NOT_REQUESTED"
          || result.release !== "HELD" || result.delivery !== "NOT_SENT" || result.dispatchAuthorized !== false
          || result.preparation !== (withRecipe ? "RECIPE_SAVED" : "WORKSPACE_READY")
          || (withRecipe && (!uuid.test(result.productionBriefId) || !hex.test(result.recipeSha256))))
        throw Error("Unexpected response; preparation not confirmed.");
      brand = result.brandStatus === "CONFIRMED" && uuid.test(result.brandDnaId) ? result.brandDnaId : null;
      recipe.hidden = !brand;
      el("result").textContent = "Workspace: " + result.workspaceId + " | Brand: " + result.brandStatus
        + (withRecipe ? " | Recipe: " + result.productionBriefId + " | SHA-256: " + result.recipeSha256 : "")
        + " | Release HELD; no provider call or delivery.";
      status.textContent = withRecipe ? "Recipe saved." : brand ? "Workspace ready; confirmed brand available." : "Workspace ready; confirmed brand required before saving a recipe.";
    } catch(e) { status.textContent = e.message; }
    finally { busy = false; sync(); }
  }
  review.addEventListener("change",sync); fields.forEach(f=>f.addEventListener("input",sync));
  prepare.addEventListener("click",()=>submit(false)); save.addEventListener("click",()=>submit(true));
  window.addEventListener("pagehide",()=>{epoch++; reset(); file.value=""; panel.hidden=true;});
  reset();
  session().then(()=>{panel.hidden=false;status.textContent="Authorized staff access confirmed.";}).catch(e=>{status.textContent=e.message;});
})();
