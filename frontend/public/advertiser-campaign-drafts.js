(() => {
  "use strict";
  const el = id => document.getElementById(id);
  const panel = el("draft-panel"), form = el("draft-form"), list = el("draft-list");
  const status = el("draft-status"), save = el("draft-save");
  const name = el("draft-name"), brief = el("draft-brief");
  const endpoint = "/api/advertisers/my-campaign-drafts";
  let generation = 0, csrf = null, busy = false, retry = null;
  let previewUrls = [];
  function disposePreviews() {
    for (const url of previewUrls) URL.revokeObjectURL(url);
    previewUrls = [];
  }
  function clear() {
    generation++; csrf = null; busy = false; retry = null;
    disposePreviews();
    panel.hidden = true; form.reset(); list.replaceChildren(); status.textContent = "";
    save.disabled = true;
  }
  function render(rows) {
    disposePreviews(); list.replaceChildren();
    if (!rows.length) { list.textContent = "No campaign drafts yet."; return; }
    for (const row of rows) {
      const card = document.createElement("article");
      for (const [tag, text] of [["h3",row.name],["p",row.brief],["p","Status: "+row.status]]) {
        const node = document.createElement(tag); node.textContent = text; card.appendChild(node);
      }
      const preview = document.createElement("button");
      preview.type = "button"; preview.textContent = "Prepare creative QA preview";
      card.appendChild(preview);
      preview.addEventListener("click", async () => {
        if (!csrf || panel.hidden || busy || preview.disabled) return;
        const current = generation; preview.disabled = true;
        status.textContent = "Preparing read-only QA manifest. No AI model will be called.";
        try {
          const response = await fetch(endpoint + "/creative-handoff-preview", {
            method:"POST",credentials:"same-origin",cache:"no-store",
            headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},
            body:JSON.stringify({campaignId:row.campaignId})
          });
          if (current !== generation) return;
          if (!response.ok) {
            status.textContent = "Creative QA preview is not yet enabled for this account.";
            return;
          }
          const manifest = await response.json();
          if (manifest.schema !== "alpha.advertiser.campaign-creative-handoff.v1"
              || manifest.campaignId !== row.campaignId || manifest.modelCall !== "NOT_REQUESTED"
              || manifest.release !== "HELD" || manifest.delivery !== "NOT_SENT"
              || manifest.signedAuthorization !== false)
            throw new Error("QA manifest returned unexpected authorization state");
          const blob = new Blob([JSON.stringify(manifest,null,2)],{type:"application/json"});
          const url = URL.createObjectURL(blob); previewUrls.push(url);
          const link = document.createElement("a");
          link.href = url; link.download = "alpha-campaign-" + row.campaignId + "-held-qa.json";
          link.textContent = "Download private QA-only manifest";
          card.appendChild(document.createTextNode(" "));
          card.appendChild(link);
          status.textContent = "QA manifest ready. No image, Factory mission, payment or delivery was triggered.";
          preview.remove();
        } catch {
          if (current === generation) status.textContent = "Creative QA handoff unavailable; no external action was taken.";
        } finally {if (current === generation && preview.isConnected) preview.disabled = false;}
      });
      const consentButton = document.createElement("button"); consentButton.type = "button";
      consentButton.textContent = "Review prepared recipe and AI consent"; card.appendChild(consentButton);
      const consentPanel = document.createElement("section"); card.appendChild(consentPanel);
      async function consent(action, prior) {
        if (!csrf || panel.hidden || consentButton.disabled) return;
        const current = generation; consentButton.disabled = true;
        for (const button of consentPanel.querySelectorAll("button")) button.disabled = true;
        try {
          const payload = {campaignId:row.campaignId,action};
          if (action !== "REVIEW") Object.assign(payload,{scopeSha256:prior.scopeSha256,noticeVersion:prior.noticeVersion,
            expectedReceiptId:prior.receiptId,requestKey:crypto.randomUUID()});
          const response = await fetch(endpoint+"/creative-provider-consent",{method:"POST",credentials:"same-origin",cache:"no-store",
            headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},body:JSON.stringify(payload)});
          if (current !== generation) return;
          if (!response.ok) throw Error("Prepared recipe unavailable or changed. No provider call was requested.");
          const result = await response.json();
          if (result.campaignId !== row.campaignId || result.provider !== "OpenAI" || result.model !== "gpt-image-2.5-flare"
              || result.noticeVersion !== "alpha-creative-provider-consent-v1" || !/^[0-9a-f]{64}$/.test(result.scopeSha256)
              || typeof result.generationRecipe !== "string" || result.dispatchAuthorized !== false
              || result.billingAuthorization !== "NOT_GRANTED" || result.providerCall !== "NOT_REQUESTED" || result.release !== "HELD"
              || !["NOT_COLLECTED","GRANTED","REVOKED"].includes(result.consentStatus)) throw Error("Unexpected consent response.");
          consentPanel.replaceChildren();
          const title = document.createElement("h4"); title.textContent = "AI image recipe — " + result.consentStatus; consentPanel.appendChild(title);
          const recipe = document.createElement("p"); recipe.textContent = result.generationRecipe; consentPanel.appendChild(recipe);
          const notice = document.createElement("p"); notice.textContent = "Optional: permit ALPHA to send the displayed recipe to OpenAI for one image-generation request using "+result.model+". Do not approve if it includes information you do not want shared. Approval does not authorize a payment or start generation. You can withdraw before future processing; withdrawal cannot undo processing already performed."; consentPanel.appendChild(notice);
          const approve = document.createElement("button"); approve.type="button"; approve.textContent="Allow this recipe to be sent to OpenAI";
          const revoke = document.createElement("button"); revoke.type="button"; revoke.textContent="Withdraw AI provider consent";
          if (result.consentStatus === "GRANTED") consentPanel.appendChild(revoke); else consentPanel.appendChild(approve);
          approve.addEventListener("click",()=>consent("GRANT",result)); revoke.addEventListener("click",()=>consent("REVOKE",result));
          status.textContent = "Recipe consent: " + result.consentStatus + ". No image generated and no charge authorized.";
        } catch(e) { if (current === generation) {consentPanel.replaceChildren();status.textContent=e.message;} }
        finally { if(current===generation) consentButton.disabled=false; }
      }
      consentButton.addEventListener("click",()=>consent("REVIEW"));
      list.appendChild(card);
    }
  }
  async function get(path) {
    return fetch(path,{credentials:"same-origin",cache:"no-store",headers:{"Accept":"application/json"}});
  }
  async function load() {
    const current = generation;
    try {
      const session = await get("/api/auth/session");
      if (!session.ok) return;
      const data = await session.json();
      if (!data.isAuthenticated || typeof data.csrfToken !== "string") return;
      const response = await get(endpoint);
      if (!response.ok || current !== generation) return;
      const rows = await response.json();
      if (!Array.isArray(rows) || current !== generation) return;
      csrf = data.csrfToken; render(rows); panel.hidden = false; save.disabled = busy;
    } catch { /* fail closed */ }
  }
  form.addEventListener("submit", async event => {
    event.preventDefault();
    if (busy || !csrf || panel.hidden) return;
    const n = name.value.trim(), b = brief.value.trim();
    if (!n || !b) return;
    if (!retry || retry.name !== n || retry.brief !== b)
      retry = {requestKey:crypto.randomUUID(),name:n,brief:b};
    const current = generation; busy = true; save.disabled = true;
    status.textContent = "Saving draft…";
    try {
      const response = await fetch(endpoint,{method:"POST",credentials:"same-origin",cache:"no-store",
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},body:JSON.stringify(retry)});
      if (current !== generation) return;
      if (!response.ok) {status.textContent = "Draft not accepted. Refresh account status if your access changed. Retry the same draft if needed.";return;}
      const result = await response.json();
      if (result.campaignId !== retry.requestKey || result.status !== "DRAFT") throw new Error("Unexpected receipt");
      retry = null; form.reset(); status.textContent = "Draft saved. No campaign launched and no charges incurred.";
      await load();
    } catch { if (current === generation) status.textContent = "Unable to confirm save. Retry the unchanged draft."; }
    finally { if (current === generation) {busy = false; save.disabled = !csrf;} }
  });
  document.addEventListener("alpha-profile-cleared",clear);
  document.addEventListener("alpha-profile-approved",load);
  clear();
  if (!el("profile-panel").hidden) load();
})();