"use strict";
const form = document.querySelector("#intake");
const fieldset = document.querySelector("#fields");
const statusNode = document.querySelector("#status");
const names = ["brandName", "niche", "market", "language", "colors", "font", "goals", "restrictions"];
let session, workspace, latest, dirty = false, busy = false, stale = false;
const retryKeys = new Map();
function message(text, error = false) { statusNode.textContent = text; statusNode.dataset.error = String(error); }
function values() { return Object.fromEntries(names.map(name => [name, form.elements[name].value.trim()])); }
function sync() {
  fieldset.disabled = busy || !workspace;
  document.querySelector("#open").disabled = busy;
  document.querySelector("#advertiser").disabled = busy;
  document.querySelector("#save").disabled = stale;
  document.querySelector("#confirm").disabled = busy || stale || dirty || latest?.status !== "DRAFT"
    || !document.querySelector("#consent").checked
    || ["brandName", "niche", "market", "language", "goals"].some(name => !values()[name]);
}
async function api(path, body) {
  const response = await fetch(path, { credentials: "same-origin", method: body ? "POST" : "GET",
    headers: { Accept: "application/json", ...(body ? { "Content-Type": "application/json", "X-CSRF-TOKEN": session?.csrfToken || "" } : {}) },
    ...(body ? { body: JSON.stringify(body) } : {}) });
  const data = await response.json().catch(() => null);
  if (!response.ok) {
    const error = new Error(data?.error || `Request failed (${response.status}). Your entered text has been kept.`);
    error.status = response.status; throw error;
  }
  return data;
}
async function action(run) {
  if (busy) return;
  busy = true; sync();
  try { await run(); }
  catch (error) {
    if (error.status === 409) { stale = true; message(`${error.message} Your text is still here; reload when ready to review the saved direction.`, true); }
    else message(error.message, true);
  }
  finally { busy = false; sync(); }
}
function path() { return `/api/wedding-planner/workspaces/${workspace.workspaceId}/brand-dna`; }
function keyFor(operation, payload) {
  const signature = JSON.stringify([workspace?.workspaceId, operation, payload]);
  if (!retryKeys.has(signature)) retryKeys.set(signature, crypto.randomUUID());
  return retryKeys.get(signature);
}
async function load() {
  const history = await api(path());
  latest = history.at(-1);
  for (const name of names) form.elements[name].value = latest?.[name] || "";
  dirty = false; stale = false; document.querySelector("#consent").checked = false;
  document.querySelector("#version").textContent = latest ? `Version ${latest.version} · ${latest.status}` : "No saved direction yet.";
  const container = document.querySelector("#history"); container.replaceChildren();
  for (const revision of [...history].reverse()) {
    const details = document.createElement("details"), summary = document.createElement("summary"), list = document.createElement("dl");
    summary.textContent = `Version ${revision.version} · ${revision.status} · ${new Date(revision.createdAt).toLocaleString()}`;
    details.append(summary, list);
    for (const name of names) {
      const term = document.createElement("dt"), value = document.createElement("dd");
      term.textContent = name.replace(/[A-Z]/g, letter => ` ${letter.toLowerCase()}`);
      value.textContent = revision[name] || "Not supplied"; list.append(term, value);
    }
    container.append(details);
  }
  message(latest?.status === "CONFIRMED" ? "Brand direction confirmed. Future changes start a new draft." : "Workspace loaded. Save your draft whenever you’re ready.");
}
document.querySelector("#open").addEventListener("click", () => action(async () => {
  if (dirty && !window.confirm("Discard unsaved changes and open the selected workspace?")) return;
  const advertiserId = session.advertiserId || document.querySelector("#advertiser").value;
  if (!advertiserId) throw new Error("Select an advertiser first.");
  const payload = { advertiserId, sourceSystem: "BRAND_INTAKE" };
  const opened = await api("/api/wedding-planner/workspaces", { ...payload, idempotencyKey: keyFor("open", payload) });
  workspace = opened; latest = null; dirty = false; stale = false;
  for (const name of names) form.elements[name].value = "";
  document.querySelector("#history").replaceChildren();
  document.querySelector("#workspace-name").textContent = workspace.advertiserName;
  try { await load(); } catch (error) { workspace = null; throw error; }
}));
form.addEventListener("input", event => {
  if (names.includes(event.target.name)) { dirty = true; document.querySelector("#consent").checked = false; }
  sync();
});
form.addEventListener("submit", event => { event.preventDefault(); action(async () => {
  if (stale) throw new Error("Reload the latest saved direction before saving.");
  const payload = { fields: values(), expectedVersion: latest?.version || 0 };
  await api(path(), { ...payload, idempotencyKey: keyFor("save", payload) });
  dirty = false;
  await load(); message("Draft saved. You can return to this workspace later.");
}); });
document.querySelector("#reload").addEventListener("click", () => action(async () => {
  if (dirty && !window.confirm("Discard unsaved changes and reload the saved direction?")) return;
  await load();
}));
document.querySelector("#confirm").addEventListener("click", () => action(async () => {
  if (dirty || stale || latest?.status !== "DRAFT" || !document.querySelector("#consent").checked)
    throw new Error("Save and review your draft before confirming.");
  const payload = { expectedVersion: latest.version };
  await api(`${path()}/confirm`, { ...payload, idempotencyKey: keyFor("confirm", payload) });
  await load();
}));
window.addEventListener("beforeunload", event => { if (dirty) { event.preventDefault(); event.returnValue = ""; } });
action(async () => {
  session = await api("/api/auth/session");
  if (!session.accessAllowed) {
    document.querySelector("#login").hidden = false; message("Sign in to open your private planning workspace."); return;
  }
  if (!session.canWrite && !session.advertiserId) { message("This account cannot edit advertiser brand intake."); return; }
  if (!session.advertiserId) {
    const advertisers = await api("/api/advertisers"), select = document.querySelector("#advertiser");
    for (const advertiser of advertisers) { const option = document.createElement("option"); option.value = advertiser.id; option.textContent = advertiser.name; select.append(option); }
    document.querySelector("#advertiser-label").hidden = false;
  }
  document.querySelector("#workspace-panel").hidden = false;
  message("Open your workspace to start or resume your brand direction.");
});
