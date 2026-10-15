"use strict";
const $ = selector => document.querySelector(selector);
let session, workspace, permissions, options, production, approvals = [], busy = false;
const keys = new Map();
function tell(text, error = false) { $("#status").textContent = text; $("#status").dataset.error = String(error); }
function key(name, payload) { const signature = JSON.stringify([workspace, name, payload]); if (!keys.has(signature)) keys.set(signature, crypto.randomUUID()); return keys.get(signature); }
function path() { return `/api/wedding-planner/workspaces/${workspace}/handoff`; }
function sync() {
  $("#open").disabled = busy || !$("#workspace").value; $("#workspace").disabled = busy;
  $("#resume").disabled = busy || !$("#brief").value; $("#refresh").disabled = busy || !workspace;
  $("#approval-fields").disabled = busy || !$("#candidate").value || !$("#stage").value;
  $("#campaign-fields").disabled = busy || !permissions?.canPlan || !$("#match").value;
  $("#plan-fields").disabled = busy || !permissions?.canPlan || !$("#candidate").value || !$("#quote").value || !$("#campaign").value;
  for (const id of ["#brief", "#candidate", "#match", "#slot", "#campaign", "#quote"]) $(id).disabled = busy;
  $("#fit-checks").hidden = $("#stage").value !== "INVENTORY_FIT";
}
async function api(url, body) {
  const response = await fetch(url, { credentials: "same-origin", method: body ? "POST" : "GET", headers: { Accept: "application/json",
    ...(body ? { "Content-Type": "application/json", "X-CSRF-TOKEN": session?.csrfToken || "" } : {}) }, ...(body ? { body: JSON.stringify(body) } : {}) });
  const data = await response.json().catch(() => null); if (!response.ok) throw new Error(data?.error || `Request failed (${response.status}). Your input is kept.`); return data;
}
async function action(run) { if (busy) return; busy = true; sync(); try { await run(); } catch (error) { tell(error.message, true); } finally { busy = false; sync(); } }
function text(parent, tag, value) { const node = document.createElement(tag); node.textContent = value; parent.append(node); return node; }
function choices(id, rows, value, label) { const select = $(id), previous = select.value; select.replaceChildren(); for (const row of rows) { const option = document.createElement("option"); option.value = value(row); option.textContent = label(row); select.append(option); } if ([...select.options].some(x => x.value === previous)) select.value = previous; }
function selectedCandidate() { return production?.candidates.find(x => x.id === $("#candidate").value); }
function latestReview() { return production?.reviews.filter(x => x.candidateId === $("#candidate").value).sort((a, b) => b.version - a.version)[0]; }
function latest(scope) { return approvals.filter(x => x.scopeKey === scope).sort((a, b) => b.version - a.version)[0]; }
function inventoryChoices() {
  const match = options?.matches.find(x => x.id === $("#match").value);
  choices("#slot", options?.slots.filter(x => x.creatorId === match?.creatorId) || [], x => x.id, x => `${x.slotType} · ${x.contentTitle}`);
  choices("#campaign", options?.campaigns.filter(x => x.advertiserOpportunityId === match?.advertiserOpportunityId) || [], x => x.id, x => x.name);
  quoteChoices();
}
function quoteChoices() {
  choices("#quote", options?.quoteLines.filter(x => x.blissMatchId === $("#match").value && x.adInventorySlotId === $("#slot").value) || [], x => x.id, x => `${x.description} · ${x.lineAmount} ${x.currencyCode}`);
  const line = options?.quoteLines.find(x => x.id === $("#quote").value);
  $("#price").textContent = line ? `Accepted Economics amount: ${line.lineAmount} ${line.currencyCode} for quantity ${line.quantity}.` : "No accepted quote for this exact match and slot. Prepare, approve, and accept a quote in Economics first.";
  sync();
}
async function loadApprovals() {
  const id = $("#candidate").value; approvals = id ? await api(`${path()}/candidates/${id}/approvals`) : [];
  const view = $("#approvals"); view.replaceChildren();
  for (const row of approvals) { text(view, "h3", `${row.stage} · ${row.decision} · version ${row.version}`); text(view, "p", `${row.actorLabel}: ${row.rationale}${row.adInventorySlotId ? `\nSlot: ${row.adInventorySlotId}; dimensions: ${row.imageWidth} × ${row.imageHeight}` : ""}`); }
  const candidate = selectedCandidate(), review = latestReview(), creative = $("#creative"); creative.replaceChildren();
  if (candidate) { text(creative, "h3", candidate.label); text(creative, "p", `Candidate revision ${candidate.version}; latest instructor assessment: ${review?.decision || "unrecorded"}.`); const image = document.createElement("img"); image.src = `/api/wedding-planner/workspaces/${workspace}/production/briefs/${production.brief.id}/candidates/${candidate.id}/image`; image.alt = candidate.label; image.style.maxWidth = "100%"; image.style.maxHeight = "400px"; creative.append(image); }
}
async function loadBrief() {
  production = $("#brief").value ? await api(`/api/wedding-planner/workspaces/${workspace}/production/briefs/${$("#brief").value}`) : null;
  choices("#candidate", [...(production?.candidates || [])].reverse(), x => x.id, x => `Revision ${x.version} · ${x.label}`);
  await loadApprovals();
}
async function refresh() {
  permissions = await api(`${path()}/options`); options = permissions.options;
  $("#identity").textContent = permissions.developmentIdentity ? "Local Development identity: fictional approval proof. Hosted advertiser and QA approvals require separate authenticated principals." : "Each approval is tied to your authenticated identity and authority.";
  const stages = []; if (permissions.canAdvertiserApprove) stages.push("ADVERTISER"); if (permissions.canAlphaQa) stages.push("ALPHA_QA", "INVENTORY_FIT");
  choices("#stage", stages, x => x, x => x);
  choices("#match", options.matches, x => x.id, x => `${x.creatorName} · ${x.opportunityName}`); inventoryChoices();
  const briefs = await api(`/api/wedding-planner/workspaces/${workspace}/production/briefs`);
  choices("#brief", briefs, x => x.id, x => x.headline); await loadBrief();
  const plans = await api(`${path()}/plans`), view = $("#plans"); view.replaceChildren();
  for (const plan of plans) { const snapshot = JSON.parse(plan.snapshotJson); text(view, "h3", `${plan.status} · ${plan.delivery}`); text(view, "p", `Economics quote: ${snapshot.lineAmount} ${snapshot.currencyCode}. Asset: ${snapshot.imageSha256}. Placement: ${plan.campaignPlacementId}.`); const details = document.createElement("details"); view.append(details); text(details, "summary", "Approval and Economics evidence"); text(details, "pre", JSON.stringify(snapshot, null, 2)).style.whiteSpace = "pre-wrap"; }
}
$("#open").addEventListener("click", () => action(async () => { workspace = $("#workspace").value; production = null; approvals = []; $("#brief").replaceChildren(); await refresh(); tell("Handoff workspace loaded. Review the latest evidence before acting."); }));
$("#refresh").addEventListener("click", () => action(async () => { await refresh(); tell("Saved approvals and planning history refreshed."); }));
$("#resume").addEventListener("click", () => action(loadBrief)); $("#candidate").addEventListener("change", () => action(loadApprovals));
$("#stage").addEventListener("change", sync); $("#match").addEventListener("change", inventoryChoices); $("#slot").addEventListener("change", quoteChoices); $("#quote").addEventListener("change", quoteChoices);
$("#approval-form").addEventListener("submit", event => { event.preventDefault(); action(async () => {
  const stage = $("#stage").value, slot = stage === "INVENTORY_FIT" ? $("#slot").value : null;
  if (stage === "INVENTORY_FIT" && !slot) throw new Error("Choose compatible image-overlay inventory first.");
  const scope = stage === "INVENTORY_FIT" ? `FIT:${slot}` : stage;
  const payload = { stage, decision: $("#decision").value, reviewId: latestReview()?.id || null, adInventorySlotId: slot,
    expectedVersion: latest(scope)?.version || 0, rationale: $("#rationale").value.trim(), dimensionsVerified: $("#dimensions").checked,
    formatVerified: $("#format").checked, creatorSafeAreaVerified: $("#safe-area").checked };
  await api(`${path()}/candidates/${$("#candidate").value}/approvals`, { ...payload, idempotencyKey: key(`approve-${$("#candidate").value}`, payload) });
  await loadApprovals(); tell("Candidate-specific human decision saved. No campaign was activated.");
}); });
$("#campaign-form").addEventListener("submit", event => { event.preventDefault(); action(async () => {
  const payload = { blissMatchId: $("#match").value, name: $("#campaign-name").value.trim() };
  const result = await api(`${path()}/campaigns`, { ...payload, campaignId: key("campaign", payload) });
  await refresh(); $("#campaign").value = result.campaign.id; tell("Draft campaign created for this advertiser's approved opportunity.");
}); });
$("#plan-form").addEventListener("submit", event => { event.preventDefault(); action(async () => {
  if (!$("#plan-consent").checked) throw new Error("Review and confirm the planning intent first.");
  const advertiser = latest("ADVERTISER"), qa = latest("ALPHA_QA"), fit = latest(`FIT:${$("#slot").value}`);
  if (![advertiser, qa, fit].every(x => x?.decision === "APPROVE")) throw new Error("Latest advertiser approval, Alpha QA, and inventory fit must all be approved.");
  const payload = { candidateId: $("#candidate").value, reviewId: latestReview()?.id, advertiserApprovalId: advertiser.id, alphaQaApprovalId: qa.id,
    inventoryFitId: fit.id, blissMatchId: $("#match").value, campaignId: $("#campaign").value, adInventorySlotId: $("#slot").value, quoteLineItemId: $("#quote").value };
  await api(`${path()}/plans`, { ...payload, idempotencyKey: key("plan", payload) }); await refresh(); $("#plan-consent").checked = false; tell("Creative handoff recorded as PLANNED. Inventory was not reserved and nothing was sent.");
}); });
action(async () => {
  session = await api("/api/auth/session"); if (!session.accessAllowed) { $("#login").hidden = false; tell("Sign in to your private campaign planning workspace."); return; }
  const workspaces = await api("/api/wedding-planner/workspaces"); choices("#workspace", workspaces, x => x.workspaceId, x => x.advertiserName);
  tell(workspaces.length ? "Choose a workspace to review creative and planning authority." : "Open a workspace in Brand intake first.");
});
