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

async function loadAcademy() {
  const board = await api("/api/operations/academy");
  $("#academy-notice").textContent = board.notice;
  $("#academy-delivery").textContent = `Delivery ${board.delivery}. Model calls ${board.modelCalls}. Campaign ready ${board.campaignReady ? "Yes" : "No"}.`;
  const lessons = board.lessons || [];
  const prototype = lessons.find(item => item.lessonKey === "maison-fleur");
  $("#academy-prototype").innerHTML = prototype
    ? lessonCard(prototype)
    : emptyState("No prototype is stored. The curriculum was not invented.");
  $("#academy-lessons").innerHTML = lessons.filter(item => item.role === "CANDIDATE").length
    ? lessons.filter(item => item.role === "CANDIDATE").map(lessonCard).join("")
    : emptyState("No candidate is stored.");
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

  bind("academy-store", () => postAcademy("/api/operations/academy/curriculum", { curriculumKey: "patisserie-curriculum-1" }));
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
