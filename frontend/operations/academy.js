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
    <p>Teacher ${escapeHtml(item.teacherKey || "None on file")}. Palette ${escapeHtml(item.palette)}. Font ${escapeHtml(item.fontFamily)}.</p>
    <p>Headline: ${escapeHtml(item.headline)} · CTA: ${escapeHtml(item.cta)}</p>
    <p>Requirements: ${escapeHtml(item.requirements)}</p>
    <p>Casting: ${escapeHtml(item.casting.status)} — ${escapeHtml(item.casting.direction)}</p>
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

async function loadAcademy() {
  const board = await api("/api/operations/academy");
  $("#academy-notice").textContent = board.notice;
  $("#academy-delivery").textContent = `Delivery ${board.delivery}. Model calls ${board.modelCalls}. Campaign ready ${board.campaignReady ? "Yes" : "No"}.`;
  const lessons = board.lessons || [];
  const prototypes = lessons.filter(item => item.role !== "CANDIDATE");
  $("#academy-prototypes").innerHTML = prototypes.length
    ? prototypes.map(lessonCard).join("")
    : emptyState("No quality anchor is stored. The curriculum was not invented.");
  $("#academy-lessons").innerHTML = lessons.filter(item => item.role === "CANDIDATE").length
    ? lessons.filter(item => item.role === "CANDIDATE").map(lessonCard).join("")
    : emptyState("No candidate is stored.");
  $("#academy-parity-gate").innerHTML = `<ul>${lines(board.referenceParityGate)}</ul>`;
  const dna = board.dna || [];
  $("#academy-dna-table").innerHTML = dna.length
    ? `<table><thead><tr><th>Brand</th><th>Family</th><th>Teacher</th><th>On file</th><th>Distinct</th><th>Hero</th><th>Delivery</th></tr></thead><tbody>${dna.map(item =>
        `<tr><td>${escapeHtml(item.brandName)}</td><td>${escapeHtml(item.family)}</td><td>${escapeHtml(item.teacherKey || "None")}</td><td>${item.teacherOnFile ? "Yes" : "No"}</td><td>${item.distinct ? "Yes" : "No"}</td><td>${escapeHtml(item.hero)}</td><td>${escapeHtml(item.delivery)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No Creative DNA is stored.");
  return board;
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
  bind("academy-generate", () => prepareProductionBrief(true));
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
