"use strict";
const $ = selector => document.querySelector(selector);
let session, workspace, brand, references = [], history, busy = false, stale = false;
let composerObjectUrl = null;
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
  $("#demo-generate").disabled = busy || !session?.canWrite;
  $("#composer-render").disabled = busy || stale || !history?.brief || (!session?.canWrite && !session?.advertiserId);
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
  clearComposerPreview();
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
// Original brand-owned candidate, generated in the operator's browser from a
// confirmed saved brief. No image API, teacher artwork, or auto-upload.
function clearComposerPreview() {
  if (composerObjectUrl) URL.revokeObjectURL(composerObjectUrl);
  composerObjectUrl = null;
  $("#composer-preview").hidden = true;
  $("#composer-preview").removeAttribute("src");
  $("#composer-download").hidden = true;
  $("#composer-download").removeAttribute("href");
}
async function renderBrandTemplatePng() {
  const status = $("#composer-status");
  try {
    if (!history?.brief || (!session?.canWrite && !session?.advertiserId)) {
      throw new Error("Open an authorized workspace and its saved production brief first.");
    }
    if (!window.AlphaTemplateComposer) throw new Error("Original ad composer is not available.");
    const brief = history.brief;
    const dna = JSON.parse(brief.brandSnapshotJson);
    if (!dna.brandName || !brief.headline || !brief.cta) {
      throw new Error("A confirmed brand, headline, and call-to-action are required.");
    }
    status.textContent = "Rendering original branded artwork locally…";
    const blob = await window.AlphaTemplateComposer.render({
      brandName: dna.brandName,
      headline: brief.headline,
      cta: brief.cta,
      colors: dna.colors,
      market: dna.market,
      format: $("#composer-format").value
    });
    if (blob.type !== "image/png" || blob.size > 1024 * 1024) {
      throw new Error("The rendered PNG exceeds the 1 MiB candidate image limit.");
    }
    // The server CSP deliberately allows data: images but not blob: images.
    // Keep the downloadable object URL separate from the image preview;
    // do not weaken the security policy or inject remote content.
    const imagePreview = await new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => typeof reader.result === "string"
        ? resolve(reader.result) : reject(new Error("Unable to preview the PNG."));
      reader.onerror = () => reject(new Error("Unable to preview the PNG."));
      reader.readAsDataURL(blob);
    });
    clearComposerPreview();
    composerObjectUrl = URL.createObjectURL(blob);
    const name = "alpha-original-" + $("#composer-format").value + ".png";
    $("#composer-preview").src = imagePreview;
    $("#composer-preview").hidden = false;
    $("#composer-download").href = composerObjectUrl;
    $("#composer-download").download = name;
    $("#composer-download").hidden = false;
    // Prepare the ordinary candidate-upload form, but never auto-submit it.
    let prepared = false;
    if (typeof DataTransfer === "function") {
      const transfer = new DataTransfer();
      transfer.items.add(new File([blob], name, { type: "image/png" }));
      $("#image").files = transfer.files;
      prepared = $("#image").files.length === 1;
    }
    if (prepared && !$("#candidate-label").value.trim()) {
      $("#candidate-label").value = "Original template draft — " + dna.brandName.slice(0, 100);
    }
    status.textContent = prepared
      ? "Original PNG rendered locally. Review, then use Save candidate revision for private human review. Nothing was uploaded or sent."
      : "Original PNG rendered locally. Download it and select the PNG in the candidate form to save for review. Nothing was uploaded or sent.";
    tell("Zero-credit original PNG candidate prepared. No campaign or delivery authorization.");
  } catch (error) {
    status.textContent = "Artwork not produced: " + error.message;
    throw error;
  }
}
$("#composer-render").addEventListener("click", () => action(renderBrandTemplatePng));

// No provider calls: this action uses only the existing source-media, overlay-1,
 // and factory QA endpoints. It never invokes /api/operations/academy/generate.
async function runZeroCreditDemonstration() {
  const status = $("#demo-status"), evidence = $("#demo-evidence");
  evidence.hidden = true;
  evidence.replaceChildren();
  if (!session?.canWrite) throw new Error("An authorized operator session is required.");
  let produced = false;
  try {
    status.textContent = "Finding a qualified original ALPHA studio slice…";
    let library = await api("/api/demonstrations/library");
    const reusable = item => item.status === "QUALIFIED"
      && item.market === "Panama City" && item.country === "Panama"
      && item.provenance === "Original Alpha studio slice. Not a copied podcast.";
    let clip = (library.clips || []).find(reusable);
    if (!clip) {
      status.textContent = "Creating an original synthetic source slice (no paid provider)…";
      const created = await api("/api/demonstrations/source-media/studio-slice",
        { market: "Panama City", country: "Panama", seconds: 15 });
      if (reusable(created)) clip = created;
      else {
        library = await api("/api/demonstrations/library");
        clip = (library.clips || []).find(reusable);
      }
    }
    if (!clip) throw new Error("No qualified original source slice is available. Production was not attempted.");

    status.textContent = "Assembling the fictional overlay and QR recipe…";
    const page = await api("/api/demonstrations/abc-pharmacy/produce",
      { sourceClipId: clip.id, conceptCount: 1 });
    produced = true;
    const concept = page.concepts?.[0];
    const qr = concept?.qrDestination ? new URL(concept.qrDestination, location.origin) : null;
    if (page.slug !== "abc-pharmacy" || page.delivery !== "NOT_SENT"
      || page.campaignReady !== false || page.aiCalls !== 0
      || !page.disclosure?.includes("has not sponsored")
      || concept?.recipeVersion !== "overlay-1" || concept.qaStatus !== "PASSED"
      || concept.servedPicture !== "PLAYER"
      || concept.sourceClipId !== clip.id
      || qr?.origin !== location.origin || qr.pathname !== "/demonstrations/abc-pharmacy"
      || !concept.sourceUrl || !concept.qrUrl) {
      throw new Error("The advertisement failed its no-credit, non-delivery, disclosure, or QR contract.");
    }
    status.textContent = "Checking rendered source, QR image, and demonstration destination…";
    const [source, qrImage, destination] = await Promise.all([
      fetch(concept.sourceUrl, { credentials: "same-origin" }),
      fetch(concept.qrUrl, { credentials: "same-origin" }),
      fetch("/demonstrations/abc-pharmacy", { credentials: "same-origin" })
    ]);
    if (!source.ok || !qrImage.ok || !destination.ok
      || !source.headers.get("content-type")?.startsWith("video/mp4")
      || !qrImage.headers.get("content-type")?.startsWith("image/png")
      || !destination.headers.get("content-type")?.startsWith("text/html")) {
      throw new Error("The source, QR, or destination was not served successfully.");
    }
    status.textContent = "Saving the existing factory QA receipt…";
    const batch = await api("/api/demonstrations/factory-batch", {});
    if (batch.aiCalls !== 0 || batch.delivery !== "NOT_SENT" || !batch.id) {
      throw new Error("Factory QA did not return the required unsent, zero-model receipt.");
    }
    const facts = [
      "Mode: DEMONSTRATION / NO AI CREDITS",
      "Fixture: ABC Pharmacy — fictional and not sponsored",
      "Recipe: overlay-1 · QA PASSED · PLAYER (no permanent per-prospect video)",
      "Source: original studio slice · SHA-256 " + clip.sha256,
      "QR destination: " + qr.href,
      "Model calls: 0 · CampaignReady: false · Delivery: NOT_SENT",
      "Factory receipt: " + batch.id + " · " + batch.status
        + " · " + batch.exceptionCount + " wider-library exception(s)"
    ];
    for (const line of facts) text(evidence, "p", line);
    const link = document.createElement("a");
    link.href = "/demonstrations/abc-pharmacy";
    link.textContent = "Open fictional advertisement preview";
    evidence.append(link);
    evidence.hidden = false;
    status.textContent = batch.exceptionCount
      ? "Fictional advertisement produced and checked; wider factory batch still has exceptions. Review before acceptance."
      : "Fictional advertisement produced. Source, QR, page, zero-model QA, and non-delivery verified.";
    tell("Zero-credit demonstration evidence is available below. Commercial campaign release remains blocked.");
  } catch (error) {
    status.textContent = (produced
      ? "A demonstration was produced, but final acceptance is incomplete: "
      : "Demonstration not produced or not verified: ") + error.message;
    throw error;
  }
}
$("#demo-generate").addEventListener("click", () => action(runZeroCreditDemonstration));

action(async () => {
  session = await api("/api/auth/session");
  if (!session.accessAllowed) { $("#login").hidden = false; tell("Sign in to open your private production workspace."); return; }
  $("#review-form").hidden = !session.canReview;
  const workspaces = await api("/api/wedding-planner/workspaces");
  for (const item of workspaces) { const option = document.createElement("option"); option.value = item.workspaceId; option.textContent = item.advertiserName; $("#workspace").append(option); }
  tell(workspaces.length ? "Choose your workspace to prepare or resume a brief." : "Open a workspace and confirm your brand direction in Brand intake first.");
});
