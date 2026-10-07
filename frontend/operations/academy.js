function lines(values) {
  return (values || []).map(item => `<li>${escapeHtml(item)}</li>`).join("");
}

function lessonCard(item) {
  return `<article class="panel">
    <div class="panel-header"><div><p class="eyebrow">${escapeHtml(item.status)}</p><h3>${escapeHtml(item.brandName)}</h3></div></div>
    <img src="${escapeHtml(item.imagePath)}" alt="${escapeHtml(item.brandName)} curriculum image" style="max-width:100%;height:auto">
    <p>${escapeHtml(item.headline)}</p>
    <p>${escapeHtml(item.notice || "")}</p>
    <p>Score ${item.score === null || item.score === undefined ? "unrecorded" : item.score}. Model calls ${item.modelCalls}. Campaign ready ${item.campaignReady ? "Yes" : "No"}. Delivery ${escapeHtml(item.delivery)}.</p>
    <h4>Defects</h4>
    <ul>${lines(item.defects) || "<li>None recorded.</li>"}</ul>
    <h4>Preserve</h4>
    <ul>${lines(item.preserve) || "<li>None recorded.</li>"}</ul>
    <h4>Repair</h4>
    <ul>${lines(item.repair) || "<li>None recorded.</li>"}</ul>
  </article>`;
}

function productionBriefCard(item) {
  return `<article>
    <p class="eyebrow">${escapeHtml(item.status)}</p>
    <h4>${escapeHtml(item.brandName)} · ${escapeHtml(item.market)} · ${escapeHtml(item.language)}</h4>
    <p>Quality DNA ${escapeHtml(item.qualityDnaVersion || "unrecorded")}. Teacher ${escapeHtml(item.teacherKey || "None on file")}. Advertiser palette kept: ${escapeHtml(item.palette)}. Font ${escapeHtml(item.fontFamily)}.</p>
    <p>Headline: ${escapeHtml(item.headline)} · CTA: ${escapeHtml(item.cta)}</p>
    <p>Requirements: ${escapeHtml(item.requirements)}</p>
    <p>Casting: ${escapeHtml(item.casting.status)} — ${escapeHtml(item.casting.direction)}</p>
    <h4>Four layers</h4>
    <ul>${(item.layers || []).map(layer => `<li><strong>${escapeHtml(layer.name)}</strong> ${escapeHtml(layer.content)}</li>`).join("")}</ul>
    <h4>Retrieved references</h4>
    <p>${escapeHtml(item.retrieval?.status || "unrecorded")}. ${escapeHtml(item.retrieval?.notice || "")}</p>
    <ul>${(item.retrieval?.selected || []).map(reference => `<li>${escapeHtml(reference.referenceId)} — ${escapeHtml(reference.reason)}</li>`).join("") || "<li>No ACTIVE reference was attached.</li>"}</ul>
    <h4>Production recipe</h4>
    <p>${escapeHtml(item.generationRecipe)}</p>
    <p>${escapeHtml(item.notice)}</p>
    <p>Can generate ${item.canGenerate ? "Yes" : "No"}. Model calls ${item.modelCalls}. Campaign ready ${item.campaignReady ? "Yes" : "No"}. Delivery ${escapeHtml(item.delivery)}.</p>
    <h4>Replace creative content</h4><ul>${lines(item.replaceCreative)}</ul>
  </article>`;
}

function parityCard(item) {
  return `<article>
    <p class="eyebrow">${escapeHtml(item.status)}</p>
    <p>${escapeHtml(item.notice)}</p>
    <h4>Defects</h4><ul>${lines(item.defects) || "<li>None recorded.</li>"}</ul>
    <h4>Repair</h4><ul>${lines(item.repair) || "<li>None recorded.</li>"}</ul>
    <p>Eligible to continue ${item.eligibleToContinue ? "Yes" : "No"}. Campaign ready ${item.campaignReady ? "Yes" : "No"}. Model calls ${item.modelCalls}. Delivery ${escapeHtml(item.delivery)}.</p>
  </article>`;
}

function acceptanceVoyageCard(item) {
  const report = item?.report || item?.voyage?.report || item;
  if (!report) return "<p>No stored voyage report is available.</p>";
  const refs = report.referenceIntelligence?.activeReferences || [];
  const trace = report.trace || [];
  return `<article>
    <p class="eyebrow">${escapeHtml(report.status)} · ${escapeHtml(report.amendmentStatus)}</p>
    <h4>${escapeHtml(report.campaignBrief?.advertiserName)} · ${escapeHtml(report.campaignBrief?.city)}, ${escapeHtml(report.campaignBrief?.market)}</h4>
    <p>Niche ${escapeHtml(report.campaignBrief?.niche)}. Objective ${escapeHtml(report.campaignBrief?.objective)}. Inventory ${escapeHtml(report.campaignBrief?.inventoryProductId)}.</p>
    <h4>Reference intelligence</h4>
    <p>${escapeHtml(report.referenceIntelligence?.status)}. Registered ${report.referenceIntelligence?.registered ?? 0}; uploaded ${report.referenceIntelligence?.uploaded ?? 0}; candidate ${report.referenceIntelligence?.candidate ?? 0}; ACTIVE ${report.referenceIntelligence?.active ?? 0}; ACTIVE with LEARN ${report.referenceIntelligence?.activeWithLearn ?? 0}; ACTIVE with DO NOT COPY ${report.referenceIntelligence?.activeWithDoNotCopy ?? 0}. Provenance ${escapeHtml(report.referenceIntelligence?.provenanceStatus)}.</p>
    <p>${escapeHtml(report.referenceIntelligence?.notice)}</p>
    <ul>${refs.map(reference => `<li><strong>${escapeHtml(reference.referenceId)}</strong> · ${escapeHtml(reference.niche)} · ${escapeHtml(reference.lifecycle)}<br>Strengths ${escapeHtml(reference.qualityStrengths)}<br>LEARN ${escapeHtml(reference.learn)}<br>DO NOT COPY ${escapeHtml(reference.doNotCopy)}<br>Quality DNA ${escapeHtml(reference.qualityDnaVersion)} · provenance ${escapeHtml(reference.provenance)}</li>`).join("") || "<li>No ACTIVE reference was available. Candidate files were not attached.</li>"}</ul>
    <h4>Automatic retrieval</h4>
    <p>${escapeHtml(report.productionBrief?.retrieval?.status)}. ${escapeHtml(report.productionBrief?.retrieval?.notice)}</p>
    <ul>${(report.productionBrief?.retrieval?.selected || []).map(reference => `<li>${escapeHtml(reference.referenceId)} — ${escapeHtml(reference.reason)}<br>LEARN ${escapeHtml(reference.learn)}<br>DO NOT COPY ${escapeHtml(reference.doNotCopy)}</li>`).join("") || "<li>No reference selected.</li>"}</ul>
    <p>Global Quality DNA ${escapeHtml(report.productionBrief?.qualityDnaVersion)}: ${escapeHtml((report.globalQualityDna || []).join(", "))}.</p>
    <h4>Creative and provider evidence</h4>
    <p>Brand DNA ${escapeHtml(report.brandDna?.status)} — ${escapeHtml(report.brandDna?.notice)}</p>
    <p>Provider ${escapeHtml(report.providerJob?.provider)}. Model ${escapeHtml(report.providerJob?.model)}. Job ${escapeHtml(report.providerJob?.jobId)}. Attempts ${report.providerJob?.attempts ?? 0}. Cost ${escapeHtml(report.providerJob?.costStatus)}. ${escapeHtml(report.providerJob?.notice)}</p>
    <p>Finished creative ${escapeHtml(report.finishedCreative?.status)} — ${escapeHtml(report.finishedCreative?.notice)}</p>
    <h4>Adaptation and QA</h4>
    <p>Inventory ${escapeHtml(report.inventoryPreflight?.status)}. Slots ${(report.inventoryPreflight?.slots || []).map(slot => escapeHtml(slot.slotId)).join(", ") || "none"}. Prototype scaled ${report.inventoryPreflight?.scaledFromPrototype ? "Yes" : "No"}.</p>
    <p>Originality ${escapeHtml(report.originality?.status)}. Quality QA ${escapeHtml(report.qualityQa?.status)}. Inventory QA ${escapeHtml(report.inventoryQa?.status)}. Brand DNA compliance ${escapeHtml(report.brandDnaCompliance?.status)}. Do-not-copy compliance ${escapeHtml(report.doNotCopyCompliance?.status)}. Human review ${escapeHtml(report.humanReview?.status)}.</p>
    <h4>Workflow trace</h4>
    <ol>${trace.map(step => `<li><strong>${escapeHtml(step.step)} · ${escapeHtml(step.status)}</strong><br>${escapeHtml(step.evidence)}</li>`).join("")}</ol>
    <h4>Rejection, regression, provenance, and stored quality</h4>
    <p>Rejections ${(report.rejections || []).map(item => escapeHtml(item.code + " on " + item.subjectId)).join(", ") || "none"}. A rejection is not a positive reference.</p>
    <p>Regression ${escapeHtml(report.regression?.status)}. Passed ${report.regression?.passed ? "Yes" : "No"}. Briefs ${(report.regression?.cases || []).length}. ${escapeHtml(report.regression?.notice || "")}</p>
    <p>Reference assets sent to a provider: ${report.referenceAssetsSentToProvider ? "Yes" : "No"}. Stored quality ${(report.storedQuality || []).map(item => escapeHtml(item.referenceId + " " + item.status + ", model calls " + item.modelCalls)).join("; ") || "none"}.</p>
    <h4>Open blockers</h4>
    <ul>${lines(report.blockers)}</ul>
    <p>${escapeHtml(report.notice)}</p>
    <p>Model calls ${report.modelCalls}. Campaign ready ${report.campaignReady ? "Yes" : "No"}. Delivery ${escapeHtml(report.delivery)}.</p>
  </article>`;
}

async function loadAcceptanceVoyages() {
  const board = await api("/api/operations/academy/acceptance-voyages");
  const latest = (board.voyages || [])[0];
  const target = document.getElementById("academy-voyage-result");
  if (target) target.innerHTML = latest
    ? acceptanceVoyageCard(latest)
    : "<p>No acceptance voyage is stored. The amendment remains OPEN.</p>";
  return board;
}

async function runAcceptanceVoyage() {
  const result = await api("/api/operations/academy/acceptance-voyages", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      voyageKey: "one-voyage-panama-pharmacy-working",
      advertiserName: "Harborlight Pharmacy",
      city: "Panama City",
      market: "Panama",
      niche: "pharmacy",
      objective: "Introduce prescription pickup to local customers",
      inventoryProductId: "ARE-P01"
    })
  });
  const target = document.getElementById("academy-voyage-result");
  if (target) {
    target.innerHTML = acceptanceVoyageCard(result.voyage);
    target.scrollIntoView({ block: "start" });
  }
}

function generationCard(item) {
  return `<article class="panel">
    <div class="panel-header"><div><p class="eyebrow">${escapeHtml(item.status)}</p><h3>${escapeHtml(item.brandName)}</h3></div></div>
    <img src="${escapeHtml(item.imagePath)}" alt="${escapeHtml(item.brandName)} generated advertisement pending review" style="max-width:100%;height:auto">
    <p>Family ${escapeHtml(item.family)}. Teacher ${escapeHtml(item.teacherKey)}.</p>
    <p>${escapeHtml(item.notice)}</p>
    <p>Model calls ${item.modelCalls}. Campaign ready ${item.campaignReady ? "Yes" : "No"}. Delivery ${escapeHtml(item.delivery)}.</p>
  </article>`;
}

async function loadAcademy() {
  try {
    return await loadAcademyBoard();
  } finally {
    await loadHarborlightReview();
  }
}

async function loadAcademyBoard() {
  const board = await api("/api/operations/academy");
  $("#academy-notice").textContent = board.notice;
  $("#academy-delivery").textContent = `Delivery ${board.delivery}. Model calls ${board.modelCalls}. Campaign ready ${board.campaignReady ? "Yes" : "No"}.`;
  const lessons = board.lessons || [];
  const creator = board.adCreator || {};
  $("#academy-creator-status").innerHTML = `<p class="eyebrow">${escapeHtml(creator.status || "UNKNOWN")}</p>
    <p>${escapeHtml(creator.notice || "Ad creator status is unavailable.")}</p>`;
  const generateButton = document.getElementById("academy-generate");
  if (generateButton) {
    generateButton.disabled = !creator.configured;
    generateButton.title = creator.configured ? "" : "Provider endpoint and secret-store token required";
  }
  const generations = board.generations || [];
  $("#academy-generations").innerHTML = generations.length
    ? generations.map(generationCard).join("")
    : emptyState("No generated draft is stored.");
  const prototypes = lessons.filter(item => item.role !== "CANDIDATE");
  $("#academy-prototypes").innerHTML = prototypes.length
    ? prototypes.map(lessonCard).join("")
    : emptyState("No quality anchor is stored. The curriculum was not invented.");
  $("#academy-lessons").innerHTML = lessons.filter(item => item.role === "CANDIDATE").length
    ? lessons.filter(item => item.role === "CANDIDATE").map(lessonCard).join("")
    : emptyState("No candidate is stored.");
  $("#academy-parity-gate").innerHTML = `<ul>${lines(board.referenceParityGate)}</ul>`;
  const roster = board.nicheRoster || [];
  const next = board.nextNiche;
  $("#academy-roster-next").textContent = next
    ? `Next open niche is #${next.number} ${next.name}. Created this run: #10 Motorcycle / Moped Dealership. Niches 1-9 were not repeated. Created ${board.createdCount}. Not created ${board.notCreatedCount}. Delivery NOT_SENT.`
    : "The roster is not loaded.";
  $("#academy-roster").innerHTML = roster.length
    ? `<table><thead><tr><th>#</th><th>Niche</th><th>Status</th><th>Anchor</th><th>Teacher</th></tr></thead><tbody>${roster.map(item =>
        `<tr><td>${item.number}</td><td>${escapeHtml(item.name)}</td><td>${escapeHtml(item.status)}</td><td>${escapeHtml(item.anchorKind)}</td><td>${escapeHtml(item.teacherKey || "None")}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No niche roster is stored.");
  const select = document.getElementById("academy-client-niche");
  if (select && roster.length) {
    const current = select.value;
    select.innerHTML = roster.map(item =>
      `<option value="${escapeHtml(item.key)}">#${item.number} ${escapeHtml(item.name)} — ${escapeHtml(item.status)}</option>`).join("")
      + `<option value="patisserie">Patisserie curriculum — maison-fleur, not niche 16</option>`;
    select.value = [...select.options].some(option => option.value === current) ? current : "motorcycle";
  }
  const dna = board.dna || [];
  $("#academy-dna-table").innerHTML = dna.length
    ? `<table><thead><tr><th>Brand</th><th>Family</th><th>Teacher</th><th>On file</th><th>Distinct</th><th>Hero</th><th>Delivery</th></tr></thead><tbody>${dna.map(item =>
        `<tr><td>${escapeHtml(item.brandName)}</td><td>${escapeHtml(item.family)}</td><td>${escapeHtml(item.teacherKey || "None")}</td><td>${item.teacherOnFile ? "Yes" : "No"}</td><td>${item.distinct ? "Yes" : "No"}</td><td>${escapeHtml(item.hero)}</td><td>${escapeHtml(item.delivery)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No Creative DNA is stored.");
  await loadAcceptanceVoyages();
  return board;
}

async function loadHarborlightReview() {
  const sheet = await api("/api/operations/academy/harborlight-review");
  const target = document.getElementById("harborlight-review");
  if (!target || !sheet) return;
  const rows = (items, valueName) => (items || []).map(item =>
    `<tr><td>${escapeHtml(item.name)}</td><td>${escapeHtml(item[valueName])}</td></tr>`).join("");
  target.innerHTML = `<article>
    <p class="eyebrow">${escapeHtml(sheet.attemptId)} · ${escapeHtml(sheet.status)} · ${escapeHtml(sheet.qualityDnaVersion)}</p>
    <p>${escapeHtml(sheet.notice)}</p>
    <p>Human review ${escapeHtml(sheet.humanReview)}. Campaign ready ${sheet.campaignReady ? "Yes" : "No"}. Delivery ${escapeHtml(sheet.delivery)}. Regression ${escapeHtml(sheet.regression)}.</p>
    <h4>Quality attributes</h4>
    <table><thead><tr><th>Attribute</th><th>Grade</th></tr></thead><tbody>${rows(sheet.attributes, "grade")}</tbody></table>
    <h4>Copy checks</h4>
    <table><thead><tr><th>Check</th><th>Status</th></tr></thead><tbody>${rows(sheet.copyChecks, "status")}</tbody></table>
    <h4>Open questions</h4>
    <table><thead><tr><th>Question</th><th>Status</th></tr></thead><tbody>${rows(sheet.questions, "status")}</tbody></table>
  </article>`;
}

async function postAcademy(path, body) {
  const result = await api(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });
  $("#academy-result").textContent = result.notice || "Delivery remains NOT_SENT.";
  await loadAcademy();
}

async function prepareProductionBrief(callModel = false) {
  const value = id => document.getElementById(id)?.value || "";
  try {
    const result = await api("/api/operations/academy/production-brief", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        family: value("academy-client-niche"),
        brandName: value("academy-client-brand"),
        palette: value("academy-client-palette"),
        fontFamily: value("academy-client-font"),
        headline: value("academy-client-headline"),
        cta: value("academy-client-cta"),
        market: value("academy-client-market"),
        language: value("academy-client-language"),
        requirements: value("academy-client-requirements"),
        marketResearchComplete: document.getElementById("academy-client-researched")?.checked || false,
        approvedPeopleProvided: document.getElementById("academy-client-people")?.checked || false,
        callModel
      })
    });
    $("#academy-production-result").innerHTML = productionBriefCard(result);
    $("#academy-result").textContent = result.notice;
  } catch (error) {
    $("#academy-production-result").innerHTML = `<article><p class="eyebrow">GENERATION BLOCKED</p><p>${escapeHtml(error.message)}</p><p>Campaign ready No. Model calls 0. Delivery NOT_SENT.</p></article>`;
    $("#academy-result").textContent = error.message;
  }
}

function productionRequest() {
  const value = id => document.getElementById(id)?.value || "";
  return {
    family: value("academy-client-niche"),
    brandName: value("academy-client-brand"),
    palette: value("academy-client-palette"),
    fontFamily: value("academy-client-font"),
    headline: value("academy-client-headline"),
    cta: value("academy-client-cta"),
    market: value("academy-client-market"),
    language: value("academy-client-language"),
    requirements: value("academy-client-requirements"),
    marketResearchComplete: document.getElementById("academy-client-researched")?.checked || false,
    approvedPeopleProvided: document.getElementById("academy-client-people")?.checked || false
  };
}

async function generateAdvertisement() {
  const request = productionRequest();
  request.requestKey = `creative-${Date.now()}-${crypto.randomUUID()}`;
  try {
    const result = await api("/api/operations/academy/generate", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    });
    $("#academy-production-result").innerHTML = generationCard(result);
    $("#academy-result").textContent = result.notice;
    await loadAcademy();
  } catch (error) {
    $("#academy-production-result").innerHTML = `<article><p class="eyebrow">GENERATION BLOCKED</p><p>${escapeHtml(error.message)}</p><p>Campaign ready No. Delivery NOT_SENT.</p></article>`;
    $("#academy-result").textContent = error.message;
  }
}

async function evaluateParity(qualityParity) {
  const result = await api("/api/operations/academy/parity", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      customizationCompliance: true,
      qualityParity,
      originality: true,
      geographicAuthenticity: true
    })
  });
  $("#academy-parity-result").innerHTML = parityCard(result);
  $("#academy-result").textContent = result.notice;
}

document.addEventListener("DOMContentLoaded", () => {
  const bind = (id, action) => {
    const button = document.getElementById(id);
    if (!button) return;
    button.addEventListener("click", async () => {
      button.disabled = true;
      try {
        await action();
      } catch (error) {
        $("#academy-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        button.disabled = false;
      }
    });
  };

  bind("academy-store", () => postAcademy("/api/operations/academy/curriculum", { curriculumKey: "creative-academy-2" }));
  bind("academy-production", () => prepareProductionBrief(false));
  bind("academy-generate", generateAdvertisement);
  bind("academy-voyage", runAcceptanceVoyage);
  bind("academy-drift", () => evaluateParity(false));
  bind("academy-both-pass", () => evaluateParity(true));
  bind("academy-dna", () => postAcademy("/api/operations/academy/dna", {
    dnaKey: "atelier-cendre-1",
    family: "patisserie",
    brandName: "Atelier Cendre",
    hero: "Burnt honey mille-feuille",
    palette: "Ink and apricot",
    cta: "Reserve your table",
    personality: "Quiet coastal luxury"
  }));
  bind("academy-clone", () => postAcademy("/api/operations/academy/dna", {
    dnaKey: "maison-clone-1",
    family: "patisserie",
    brandName: "Maison Fleur",
    hero: "Cake",
    palette: "Gold",
    cta: "Reserve your table",
    personality: "Luxury"
  }));
  bind("academy-dental", () => postAcademy("/api/operations/academy/dna", {
    dnaKey: "north-clinic-1",
    family: "dental",
    brandName: "North Clinic",
    hero: "A calm consultation",
    palette: "Stone and sage",
    cta: "Book a visit",
    personality: "Precise and warm"
  }));
  bind("academy-visual", () => postAcademy("/api/operations/academy/visual", {
    lessonKey: "belmonte",
    visualBenchmarkMet: true
  }));
  bind("academy-campaign", () => postAcademy("/api/operations/academy/visual", {
    lessonKey: "belmonte",
    visualBenchmarkMet: true,
    campaignReady: true
  }));
});
