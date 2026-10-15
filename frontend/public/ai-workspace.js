"use strict";
const $ = selector => document.querySelector(selector);
let session, workspace, readiness, runs = [], busy = false;
function tell(text, error = false) { $("#status").textContent = text; $("#status").dataset.error = String(error); }
function writable() { return session?.canWrite || session?.advertiserId; }
function sync() {
  $("#open").disabled = busy || !$("#workspace").value; $("#workspace").disabled = busy;
  $("#refresh").disabled = busy || !workspace; $("#new-session").disabled = busy || !workspace || !writable();
  $("#fresh-key").disabled = busy || !workspace || !writable();
  $("#text-fields").disabled = busy || !writable() || !readiness?.textReady || !$("#session").value || !$("#consent").checked;
  $("#image-fields").disabled = busy || !writable() || !readiness?.imageReady || !$("#brief").value || !$("#consent").checked;
  for (const button of document.querySelectorAll("#runs button")) button.disabled = busy || !writable();
}
async function api(path, body) {
  const response = await fetch(path, { credentials: "same-origin", method: body ? "POST" : "GET", headers: { Accept: "application/json",
    ...(body ? { "Content-Type": "application/json", "X-CSRF-TOKEN": session?.csrfToken || "" } : {}) }, ...(body ? { body: JSON.stringify(body) } : {}) });
  const data = await response.json().catch(() => null);
  if (!response.ok) throw new Error(data?.error || `Request failed (${response.status}). Your input has been kept.`);
  return data;
}
function path() { return `/api/wedding-planner/workspaces/${workspace}/ai`; }
async function key(operation, payload) {
  const intentKey = `bliss-ai-intent-${workspace}`;
  let intent = sessionStorage.getItem(intentKey);
  if (!intent) { intent = crypto.randomUUID(); sessionStorage.setItem(intentKey, intent); }
  const hash = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(JSON.stringify([workspace, intent, operation, payload])));
  const storageKey = "bliss-ai-key-" + [...new Uint8Array(hash)].map(value => value.toString(16).padStart(2, "0")).join("");
  let stored = sessionStorage.getItem(storageKey);
  if (!stored) { stored = crypto.randomUUID(); sessionStorage.setItem(storageKey, stored); }
  return stored;
}
async function action(run) {
  if (busy) return; busy = true; sync();
  try { await run(); } catch (error) { tell(error.message, true); } finally { busy = false; sync(); }
}
function text(parent, tag, value) { const element = document.createElement(tag); element.textContent = value; parent.append(element); return element; }
function previousChoices() {
  const selected = $("#previous").value; $("#previous").replaceChildren();
  const none = document.createElement("option"); none.value = ""; none.textContent = "None"; $("#previous").append(none);
  for (const run of runs.filter(x => x.kind === "TEXT" && x.sessionId === $("#session").value && x.status === "SUCCEEDED_PENDING_REVIEW")) {
    const option = document.createElement("option"); option.value = run.id; option.textContent = run.outputText.slice(0, 80); $("#previous").append(option);
  }
  if ([...$("#previous").options].some(x => x.value === selected)) $("#previous").value = selected;
}
async function refresh() {
  const selectedWorkspace = workspace;
  const [ready, loaded] = await Promise.all([api(`${path()}/readiness`), api(`${path()}/runs`)]);
  if (selectedWorkspace !== workspace) return;
  readiness = ready; runs = loaded;
  $("#readiness").textContent = ready.textReady || ready.imageReady
    ? `${ready.provider}: text ${ready.textReady ? "ready" : "unavailable"}; images ${ready.imageReady ? "ready" : "unavailable"}. Reserved today: $${ready.reservedTodayUsd}; total: $${ready.reservedTotalUsd}. Reservations are not billing receipts.`
    : "AI calls are disabled or configuration/budget needs attention. Existing run history remains available. No live provider is being simulated.";
  const container = $("#runs"); container.replaceChildren();
  for (const run of runs) {
    const article = document.createElement("article"); container.append(article);
    text(article, "h3", `${run.kind === "TEXT" ? "Planning suggestion" : "Creative image"} · ${run.status}`);
    text(article, "p", `${run.provider} · ${run.model} · ${new Date(run.createdAt).toLocaleString()}\nReserved: $${run.reservedUsd}; actual cost: ${run.actualUsd == null ? "unrecorded" : `$${run.actualUsd} (${run.costStatus})`}.`);
    if (run.outputText) text(article, "p", run.outputText);
    if (run.failureCode) text(article, "p", `Outcome: ${run.failureCode}. A failed or interrupted call may still incur provider charges; its reservation is retained.`);
    if (run.kind === "IMAGE" && run.status === "SUCCEEDED_PENDING_REVIEW") {
      const image = document.createElement("img"); image.src = `${path()}/runs/${run.id}/image`; image.alt = "AI-generated draft pending human review"; article.append(image);
      if (run.attachedCandidateId) text(article, "p", "Attached to Creative Academy as a candidate; instructor assessment is separate.");
      else { const button = text(article, "button", "Attach draft for human assessment"); button.type = "button"; button.addEventListener("click", () => action(async () => {
        const history = await api(`/api/wedding-planner/workspaces/${workspace}/production/briefs/${run.briefId}`);
        await api(`${path()}/runs/${run.id}/attach-candidate`, { expectedVersion: history.candidates.at(-1)?.version || 0, label: `AI draft ${run.id.slice(0, 8)}` });
        await refresh(); tell("Draft attached as an unreviewed candidate. Continue in Creative Academy.");
      })); }
    }
    if (["QUEUED", "RUNNING"].includes(run.status)) {
      const button = text(article, "button", "Cancel run"); button.type = "button";
      button.addEventListener("click", () => action(async () => { await api(`${path()}/runs/${run.id}/cancel`, {}); await refresh(); tell("Cancellation recorded. Provider billing may continue if processing already began."); }));
    }
    text(article, "p", "AI output is pending human review. It does not approve or activate a campaign.");
  }
  previousChoices(); sync();
}
$("#open").addEventListener("click", () => action(async () => {
  workspace = $("#workspace").value; readiness = null; runs = []; $("#runs").replaceChildren(); $("#consent").checked = false;
  const [sessions, briefs] = await Promise.all([api(`/api/wedding-planner/workspaces/${workspace}/sessions`), api(`/api/wedding-planner/workspaces/${workspace}/production/briefs`)]);
  for (const [selector, items, idField, labelField] of [["#session", sessions, "sessionId", "createdAt"], ["#brief", briefs, "id", "headline"]]) {
    $(selector).replaceChildren(); for (const item of items) { const option = document.createElement("option"); option.value = item[idField]; option.textContent = labelField === "createdAt" ? `Session ${new Date(item.createdAt).toLocaleString()}` : item[labelField]; $(selector).append(option); }
  }
  await refresh(); tell("Workspace loaded. AI requests require configured providers, available budget, and your data-sharing consent.");
}));
$("#new-session").addEventListener("click", () => action(async () => {
  const created = await api(`/api/wedding-planner/workspaces/${workspace}/sessions`, { sourceSystem: "CONTROLLED_AI_UI", idempotencyKey: crypto.randomUUID() });
  const option = document.createElement("option"); option.value = created.sessionId; option.textContent = `Session ${new Date(created.createdAt).toLocaleString()}`; $("#session").append(option); $("#session").value = created.sessionId;
  previousChoices(); tell("Planning session created. Messages and AI drafts stay in your workspace.");
}));
$("#refresh").addEventListener("click", () => action(refresh));
$("#fresh-key").addEventListener("click", () => { sessionStorage.setItem(`bliss-ai-intent-${workspace}`, crypto.randomUUID()); $("#consent").checked = false; sync(); tell("Fresh request prepared. Review the context and consent again before submitting; another reservation will be used."); });
$("#session").addEventListener("change", () => { previousChoices(); sync(); }); $("#brief").addEventListener("change", sync); $("#consent").addEventListener("change", sync);
$("#text-form").addEventListener("submit", event => { event.preventDefault(); action(async () => {
  const payload = { kind: "TEXT", sessionId: $("#session").value, briefId: null, previousRunId: $("#previous").value || null,
    prompt: $("#prompt").value.trim(), consentToShare: $("#consent").checked };
  await api(`${path()}/runs`, { ...payload, idempotencyKey: await key("text", payload) }); await refresh(); tell("Suggestion queued. Your selected context will be sent to the configured provider.");
}); });
$("#image-form").addEventListener("submit", event => { event.preventDefault(); action(async () => {
  const payload = { kind: "IMAGE", sessionId: null, briefId: $("#brief").value, previousRunId: null, prompt: "", consentToShare: $("#consent").checked };
  await api(`${path()}/runs`, { ...payload, idempotencyKey: await key("image", payload) }); await refresh(); tell("Image draft queued. Only the saved production recipe will be sent.");
}); });
setInterval(() => { if (workspace && !busy && runs.some(x => ["QUEUED", "RUNNING", "CANCEL_REQUESTED"].includes(x.status))) action(refresh); }, 1000);
action(async () => {
  session = await api("/api/auth/session"); if (!session.accessAllowed) { $("#login").hidden = false; tell("Sign in to your private AI workspace."); return; }
  const workspaces = await api("/api/wedding-planner/workspaces");
  for (const item of workspaces) { const option = document.createElement("option"); option.value = item.workspaceId; option.textContent = item.advertiserName; $("#workspace").append(option); }
  tell(workspaces.length ? "Choose a workspace to review AI readiness and run history." : "Open a workspace in Brand intake first.");
});
