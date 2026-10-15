"use strict";
const $ = selector => document.querySelector(selector);
let session, workspace, brand, references = [], history, busy = false, stale = false;
const keys = new Map();
function tell(text, error = false) { $("#status").textContent = text; $("#status").dataset.error = String(error); }
function sync() {
  $("#open").disabled = busy || !$("#workspace").value;
  $("#workspace").disabled = busy;
  $("#brief-fields").disabled = busy || stale || !workspace || brand?.status !== "CONFIRMED" || !references.length;
  $("#candidate-fields").disabled = busy || stale || !history || (!session?.canWrite && !session?.advertiserId);
  $("#review-fields").disabled = busy || stale || !history?.candidates.length || !session?.canReview;
  $("#resume").disabled = busy || !$("#brief").value;
  $("#reload").disabled = busy || !workspace;
  $("#brief").disabled = busy;
}
function path() { return `/api/wedding-planner/workspaces/${workspace}/production`; }
function key(operation, payload) {
  const signature = JSON.stringify([workspace, operation, payload]);
  if (!keys.has(signature)) keys.set(signature, crypto.randomUUID());
  return keys.get(signature);
}
async function api(url, body) {
  const response = await fetch(url, { credentials: "same-origin", method: body ? "POST" : "GET",
    headers: { Accept: "application/json", ...(body ? { "Content-Type": "application/json", "X-CSRF-TOKEN": session?.csrfToken || "" } : {}) },
    ...(body ? { body: JSON.stringify(body) } : {}) });
  const data = await response.json().catch(() => null);
  if (!response.ok) { const error = new Error(data?.error || `Request failed (${response.status}).`); error.status = response.status; throw error; }
  return data;
}
async function action(run) {
  if (busy) return; busy = true; sync();
  try { await run(); } catch (error) { if (error.status === 409) stale = true; tell(`${error.message}${stale ? " Your input is kept. Refresh saved history before retrying." : ""}`, true); }
  finally { busy = false; sync(); }
}
function text(parent, tag, value, className) { const node = document.createElement(tag); node.textContent = value; if (className) node.className = className; parent.append(node); return node; }
function referenceNote() { const ref = references.find(x => x.nicheKey === $("#family").value); $("#reference-note").textContent = ref ? `Reference ${ref.referenceId}. Learn: ${ref.learn}. Do not copy: ${ref.doNotCopy}.` : "No eligible reference is available."; }
async function loadBrief(id) {
  const loaded = await api(`${path()}/briefs/${id}`); history = loaded;
  const brief = history.brief, dna = JSON.parse(brief.brandSnapshotJson), production = JSON.parse(brief.productionBriefJson), reference = JSON.parse(brief.referenceSnapshotJson);
  const panel = $("#brief-details"); panel.replaceChildren();
  text(panel, "h3", `${dna.brandName} · ${brief.headline}`);
  text(panel, "p", `Brand DNA version ${dna.version} · Reference ${brief.referenceId} · Quality DNA ${production.qualityDnaVersion}`);
  text(panel, "p", `Palette: ${dna.colors || "Not supplied"}. Font: ${dna.font || "Not supplied"}. Market: ${dna.market}. Language: ${dna.language}.`);
  const provenance = document.createElement("details"); panel.append(provenance); text(provenance, "summary", "Production recipe and provenance");
  text(provenance, "pre", production.generationRecipe);
  text(provenance, "p", `Reference provenance: ${reference.provenanceSource || "Unrecorded"}; approved by ${reference.approvingAuthority || "Unrecorded"} on ${reference.approvedOn || "Unrecorded"}.`);
  text(provenance, "p", `Reference SHA-256: ${brief.referenceSha256}\nRecipe SHA-256: ${brief.recipeSha256}`);
  const container = $("#history"); container.replaceChildren();
  for (const candidate of [...history.candidates].reverse()) {
    const article = document.createElement("article"); container.append(article);
    text(article, "h3", `Candidate ${candidate.version} · ${candidate.label}`);
    const image = document.createElement("img"); image.alt = candidate.label; image.loading = "lazy";
    image.src = `${path()}/briefs/${id}/candidates/${candidate.id}/image`; article.append(image);
    text(article, "p", candidate.notes || "No production notes supplied.");
    text(article, "p", `Image SHA-256: ${candidate.imageSha256}`);
    const reviews = history.reviews.filter(x => x.candidateId === candidate.id).sort((a, b) => a.version - b.version);
    if (!reviews.length) text(article, "p", "Awaiting human instructor assessment.");
    for (const review of reviews) {
      const box = document.createElement("div"); box.className = "review"; article.append(box);
      text(box, "strong", `Assessment ${review.version} · ${review.decision} · ${review.actorLabel}`);
      text(box, "p", `Customization: ${review.customization}; quality parity: ${review.qualityParity}; originality: ${review.originality}; geographic authenticity: ${review.geographicAuthenticity}. Critical defect: ${review.criticalDefect ? "Yes" : "No"}.`);
      text(box, "p", `${review.rationale}\nPreserve: ${review.preserve || "Not supplied"}\nRepair: ${review.repair || "Not supplied"}`);
    }
  }
  tell("Brief resumed. Candidate and assessment histories are saved; campaign approval remains separate.");
}
async function refresh(preferred) {
  const brands = await api(`/api/wedding-planner/workspaces/${workspace}/brand-dna`);
  brand = brands.at(-1);
  $("#brand").textContent = brand ? `${brand.brandName || "Unnamed brand"} · Brand DNA version ${brand.version} · ${brand.status}` : "Save and confirm your brand direction first.";
  const briefs = await api(`${path()}/briefs`), select = $("#brief");
  const id = preferred || select.value;
  select.replaceChildren();
  for (const brief of briefs) { const option = document.createElement("option"); option.value = brief.id; option.textContent = `${brief.headline} · ${new Date(brief.createdAt).toLocaleString()}`; select.append(option); }
  if (briefs.some(x => x.id === id)) select.value = id;
  if (select.value) await loadBrief(select.value);
  else { history = null; $("#brief-details").replaceChildren(); $("#history").replaceChildren(); tell("Workspace loaded. Prepare a brief from your confirmed brand direction."); }
  stale = false;
}
$("#open").addEventListener("click", () => action(async () => {
  workspace = $("#workspace").value; history = null; brand = null; stale = false;
  $("#brief").replaceChildren(); $("#brief-details").replaceChildren(); $("#history").replaceChildren();
  references = await api(`${path()}/references`); $("#family").replaceChildren();
  for (const reference of references) { const option = document.createElement("option"); option.value = reference.nicheKey; option.textContent = `${reference.nicheName} · ${reference.referenceId}`; $("#family").append(option); }
  referenceNote(); await refresh();
}));
$("#family").addEventListener("change", referenceNote);
$("#resume").addEventListener("click", () => action(async () => { await loadBrief($("#brief").value); stale = false; }));
$("#reload").addEventListener("click", () => action(() => refresh()));
$("#brief-form").addEventListener("submit", event => { event.preventDefault(); action(async () => {
  const payload = { brandDnaId: brand.id, family: $("#family").value, headline: $("#headline").value.trim(), cta: $("#cta").value.trim() };
  const result = await api(`${path()}/briefs`, { ...payload, idempotencyKey: key("brief", payload) });
  await refresh(result.brief.id); tell("Production brief saved with brand and reference provenance.");
}); });
$("#candidate-form").addEventListener("submit", event => { event.preventDefault(); action(async () => {
  const file = $("#image").files[0]; if (!file || file.size > 1024 * 1024) throw new Error("Choose a PNG image of at most 1 MiB.");
  const data = await new Promise((resolve, reject) => { const reader = new FileReader(); reader.onload = () => resolve(reader.result.split(",")[1]); reader.onerror = () => reject(new Error("Image could not be read.")); reader.readAsDataURL(file); });
  const payload = { expectedVersion: history.candidates.at(-1)?.version || 0, label: $("#candidate-label").value.trim(), notes: $("#notes").value.trim(), imageBase64: data };
  const id = history.brief.id;
  await api(`${path()}/briefs/${id}/candidates`, { ...payload, idempotencyKey: key(`candidate-${id}`, payload) });
  await loadBrief(id); $("#candidate-form").reset(); tell("Candidate revision saved. It is awaiting a human assessment.");
}); });
$("#review-form").addEventListener("submit", event => { event.preventDefault(); action(async () => {
  const candidate = history.candidates.at(-1), prior = history.reviews.filter(x => x.candidateId === candidate.id).sort((a, b) => a.version - b.version).at(-1);
  const payload = { expectedVersion: prior?.version || 0, decision: $("#decision").value, criticalDefect: $("#critical").checked,
    rationale: $("#rationale").value.trim(), preserve: $("#preserve").value.trim(), repair: $("#repair").value.trim() };
  for (const name of ["customization", "qualityParity", "originality", "geographicAuthenticity"]) payload[name] = $("#review-form").elements[name].value;
  await api(`${path()}/briefs/${history.brief.id}/candidates/${candidate.id}/reviews`, { ...payload, idempotencyKey: key(`review-${candidate.id}`, payload) });
  await loadBrief(history.brief.id); $("#review-form").reset(); tell("Human assessment saved. Campaign approval remains separate.");
}); });
action(async () => {
  session = await api("/api/auth/session");
  if (!session.accessAllowed) { $("#login").hidden = false; tell("Sign in to open your private production workspace."); return; }
  $("#review-form").hidden = !session.canReview;
  const workspaces = await api("/api/wedding-planner/workspaces");
  for (const item of workspaces) { const option = document.createElement("option"); option.value = item.workspaceId; option.textContent = item.advertiserName; $("#workspace").append(option); }
  tell(workspaces.length ? "Choose your workspace to prepare or resume a brief." : "Open a workspace and confirm your brand direction in Brand intake first.");
});
