(() => {
  "use strict";
  const el = id => document.getElementById(id);
  const panel = el("draft-panel"), form = el("draft-form"), list = el("draft-list");
  const status = el("draft-status"), save = el("draft-save");
  const name = el("draft-name"), brief = el("draft-brief");
  const endpoint = "/api/advertisers/my-campaign-drafts";
  let generation = 0, csrf = null, busy = false, retry = null;
  function clear() {
    generation++; csrf = null; busy = false; retry = null;
    panel.hidden = true; form.reset(); list.replaceChildren(); status.textContent = "";
    save.disabled = true;
  }
  function render(rows) {
    list.replaceChildren();
    if (!rows.length) { list.textContent = "No campaign drafts yet."; return; }
    for (const row of rows) {
      const card = document.createElement("article");
      for (const [tag, text] of [["h3",row.name],["p",row.brief],["p","Status: "+row.status]]) {
        const node = document.createElement(tag); node.textContent = text; card.appendChild(node);
      }
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