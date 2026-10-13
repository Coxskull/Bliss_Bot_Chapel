"use strict";
const $ = selector => document.querySelector(selector);
let session, workspace, permissions, handoffs = [], history, busy = false;
const keys = new Map();
function key(operation, payload) { const signature = JSON.stringify([workspace, operation, payload]); if (!keys.has(signature)) keys.set(signature, crypto.randomUUID()); return keys.get(signature); }
function tell(text, error = false) { $("#status").textContent = text; $("#status").dataset.error = String(error); }
function path() { return `/api/wedding-planner/workspaces/${workspace}/measurement`; }
function text(parent, tag, value) { const node = document.createElement(tag); node.textContent = value; parent.append(node); return node; }
function choices(id, rows, value, label) { const select = $(id), old = select.value; select.replaceChildren(); for (const row of rows) { const option = document.createElement("option"); option.value = value(row); option.textContent = label(row); select.append(option); } if ([...select.options].some(x => x.value === old)) select.value = old; }
function sync() {
  $("#open").disabled = busy || !$("#workspace").value; $("#workspace").disabled = busy; $("#refresh").disabled = busy || !workspace;
  $("#observation-fields").disabled = busy || !$("#handoff").value || !$("#kind").value;
  $("#proposal-fields").disabled = busy || !permissions?.canWrite || !$("#observation").value;
  $("#review-fields").disabled = busy || !permissions?.canReview || !$("#proposal").value;
  $("#readiness").disabled = busy; $("#handoff").disabled = busy;
  $("#external-fields").hidden = $("#kind").value !== "EXTERNAL_REPORT";
  $("#readiness-section").hidden = !permissions?.canWrite;
}
async function api(url, body) {
  const response = await fetch(url, { credentials: "same-origin", method: body ? "POST" : "GET", headers: { Accept: "application/json",
    ...(body ? { "Content-Type": "application/json", "X-CSRF-TOKEN": session?.csrfToken || "" } : {}) }, ...(body ? { body: JSON.stringify(body) } : {}) });
  const data = await response.json().catch(() => null); if (!response.ok) throw new Error(data?.error || `Request failed (${response.status}). Your input is kept.`); return data;
}
async function action(run) { if (busy) return; busy = true; sync(); try { await run(); } catch (error) { tell(error.message, true); } finally { busy = false; sync(); } }
function selectedHandoff() { return handoffs.find(x => x.id === $("#handoff").value); }
function render() {
  const handoff = selectedHandoff(), lineage = $("#lineage"); lineage.replaceChildren();
  if (handoff) { const snapshot = JSON.parse(handoff.snapshotJson); text(lineage, "p", `${handoff.status} · ${handoff.delivery}\nAsset: ${snapshot.imageSha256}\nBrand DNA: ${snapshot.brandDnaId}\nAccepted quote: ${snapshot.lineAmount} ${snapshot.currencyCode}`); }
  const observations = history.observations.filter(x => x.handoffId === handoff?.id), ids = new Set(observations.map(x => x.id));
  const proposals = history.proposals.filter(x => ids.has(x.observationId));
  choices("#observation", observations, x => x.id, x => `${x.kind} · ${x.sourceLabel}`); choices("#proposal", proposals, x => x.id, x => x.hypothesis);
  const view = $("#observations"); view.replaceChildren();
  for (const row of observations) {
    text(view, "h3", `${row.kind} · ${row.evidenceStatus}`); text(view, "p", `${row.sourceLabel} · ${row.observedAt}\n${row.evidence}`);
    if (row.kind === "EXTERNAL_REPORT") text(view, "p", `Reported impressions: ${row.impressions ?? "unknown"}; clicks: ${row.clicks ?? "unknown"}; conversions: ${row.conversions ?? "unknown"}.`);
    const detail = document.createElement("details"); view.append(detail); text(detail, "summary", "Frozen creative and pricing lineage"); text(detail, "pre", row.lineageSnapshotJson);
  }
  const research = $("#proposals"); research.replaceChildren();
  for (const proposal of proposals) { const reviews = history.reviews.filter(x => x.proposalId === proposal.id).sort((a, b) => a.version - b.version); text(research, "h3", `${reviews.at(-1)?.decision || proposal.status} · ${proposal.hypothesis}`); text(research, "p", `${proposal.experiment}\nSuccess: ${proposal.successCriterion}`); for (const review of reviews) text(research, "p", `Review ${review.version}: ${review.decision} · ${review.actorLabel}\n${review.rationale}`); }
  sync();
}
async function refresh() {
  permissions = await api(path()); history = permissions.history;
  handoffs = await api(`/api/wedding-planner/workspaces/${workspace}/handoff/plans`);
  choices("#handoff", handoffs, x => x.id, x => `${x.status} · ${x.id}`);
  const kinds = permissions.canWrite ? ["LOCAL_VALIDATION", "ADVERTISER_FEEDBACK", "EXTERNAL_REPORT"] : permissions.canGiveFeedback ? ["ADVERTISER_FEEDBACK"] : [];
  choices("#kind", kinds, x => x, x => x);
  $("#identity").textContent = permissions.developmentIdentity ? "Local Development workflow: hosted research reviews require separate authenticated principals." : "Reports and reviews are tied to your authenticated authority.";
  render();
}
function date(id) { const value = $(id).value; if (!value || Number.isNaN(new Date(value).getTime())) throw new Error("Enter a valid observation/reporting time."); return new Date(value).toISOString(); }
function count(id) { const value = $(id).value; if (value === "") return null; const number = Number(value); if (!Number.isSafeInteger(number) || number < 0) throw new Error("Counts must be nonnegative whole numbers within the browser's exact integer range."); return number; }
$("#open").addEventListener("click", () => action(async () => { workspace = $("#workspace").value; $("#handoff").replaceChildren(); $("#readiness-results").replaceChildren(); await refresh(); tell("Measurement workspace loaded."); }));
$("#refresh").addEventListener("click", () => action(async () => { await refresh(); tell("Saved observation and research history refreshed."); }));
$("#handoff").addEventListener("change", render); $("#kind").addEventListener("change", sync);
$("#observation-form").addEventListener("submit", event => { event.preventDefault(); action(async () => {
  const external = $("#kind").value === "EXTERNAL_REPORT";
  const payload = { handoffId: $("#handoff").value, kind: $("#kind").value, sourceLabel: $("#source").value.trim(), evidence: $("#evidence").value.trim(), observedAt: date("#observed"),
    windowStart: external ? date("#window-start") : null, windowEnd: external ? date("#window-end") : null, impressions: external ? count("#impressions") : null, clicks: external ? count("#clicks") : null,
    conversions: external ? count("#conversions") : null, acknowledgeUnverifiedReport: external && $("#report-consent").checked };
  await api(path() + "/observations", { ...payload, idempotencyKey: key("observation", payload) }); await refresh(); tell("Sourced observation saved. Reported metrics remain unverified.");
}); });
$("#proposal-form").addEventListener("submit", event => { event.preventDefault(); action(async () => {
  const payload = { observationId: $("#observation").value, hypothesis: $("#hypothesis").value.trim(), experiment: $("#experiment").value.trim(), successCriterion: $("#criterion").value.trim(), applyToProduction: false };
  await api(path() + "/proposals", { ...payload, idempotencyKey: key("proposal", payload) }); await refresh(); tell("Research proposal saved for independent human review.");
}); });
$("#review-form").addEventListener("submit", event => { event.preventDefault(); action(async () => {
  const proposal = $("#proposal").value, latest = history.reviews.filter(x => x.proposalId === proposal).sort((a, b) => b.version - a.version)[0];
  const payload = { expectedVersion: latest?.version || 0, decision: $("#decision").value, rationale: $("#rationale").value.trim(), applyToProduction: false };
  await api(`${path()}/proposals/${proposal}/reviews`, { ...payload, idempotencyKey: key(`review-${proposal}`, payload) }); await refresh(); tell("Research review saved. Production matching, pricing and delivery were not changed.");
}); });
$("#readiness").addEventListener("click", () => action(async () => {
  const report = await api("/api/operations/campaign-readiness"), view = $("#readiness-results"); view.replaceChildren();
  text(view, "p", `${report.environment} · hosted acceptance: ${report.hostedAcceptanceClaimed ? "claimed" : "not validated"}`);
  for (const check of report.checks) { text(view, "h3", `${check.area} · ${check.status}`); text(view, "p", check.evidence); }
  tell("Operating readiness checked. Configuration is not hosted acceptance.");
}));
action(async () => {
  session = await api("/api/auth/session"); if (!session.accessAllowed) { $("#login").hidden = false; tell("Sign in to your private measurement workspace."); return; }
  const workspaces = await api("/api/wedding-planner/workspaces"); choices("#workspace", workspaces, x => x.workspaceId, x => x.advertiserName);
  const local = new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16); $("#observed").value = local;
  tell(workspaces.length ? "Choose a workspace to inspect handoff evidence and research." : "Open an advertiser workspace in Brand intake first.");
});
