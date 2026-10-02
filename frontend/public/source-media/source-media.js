const meter = document.querySelector("#meter");
const rows = document.querySelector("#rows");
const notice = document.querySelector("#notice");
const discovery = document.querySelector("#discovery");
const evidenceNotice = document.querySelector("#evidenceNotice");
const evidenceProspect = document.querySelector("#evidenceProspect");
const evidenceRole = document.querySelector("#evidenceRole");
const prospects = document.querySelector("#prospects");
const nicheSelect = document.querySelector("#niche");
const marketSelect = document.querySelector("#market");
let latestQualified = null;
let latestFile = null;

function cell(text) {
  const item = document.createElement("td");
  item.textContent = text;
  return item;
}

function render(library) {
  meter.replaceChildren();
  [
    ["Daily benchmark", library.dailyTarget],
    ["Qualified unique", library.qualifiedUnique],
    ["Duplicates", library.duplicates],
    ["Replacement", library.replacementRequired],
    ["Daily remaining", library.dailyRemaining],
    ["Weekly benchmark", library.weeklyTarget],
    ["Fuel", library.fuelStatus]
  ].forEach(([label, value]) => {
    const article = document.createElement("article");
    const strong = document.createElement("strong");
    strong.textContent = value;
    if (label === "Fuel") strong.className = value === "HEALTHY" ? "ok" : "warn";
    const span = document.createElement("span");
    span.textContent = label;
    article.append(strong, span);
    meter.append(article);
  });
  rows.replaceChildren();
  latestQualified = null;
  library.clips.forEach(clip => {
    if (clip.status === "QUALIFIED" && !latestQualified) latestQualified = clip.id;
    const row = document.createElement("tr");
    row.append(
      cell(clip.originalFileName),
      cell(clip.market + ", " + clip.country),
      cell(String(clip.durationSeconds)),
      cell(clip.status),
      cell(String(clip.quotaCredit)),
      cell(clip.provenance)
    );
    rows.append(row);
  });
  prospects.replaceChildren();
  const selectedProspect = evidenceProspect.value;
  evidenceProspect.replaceChildren();
  library.demonstrations.forEach(item => {
    const row = document.createElement("tr");
    const action = document.createElement("td");
    if (item.opportunityScore === 100 && item.slug !== "abc-pharmacy") {
      const produce = document.createElement("button");
      produce.type = "button";
      produce.textContent = item.conceptCount > 0 ? "Open demonstration" : "Produce one demonstration";
      produce.addEventListener("click", () => produceProspect(item));
      action.append(produce);
    }
    row.append(
      cell(item.businessName),
      cell(item.niche || ""),
      cell(item.market),
      cell(String(item.opportunityScore ?? "")),
      cell(item.prospectState || ""),
      cell(item.decisionMakerStatus || ""),
      action
    );
    prospects.append(row);
    if (item.opportunityScore === 100) {
      const option = document.createElement("option");
      option.value = item.slug;
      option.textContent = item.businessName + " · " + item.niche;
      option.selected = item.slug === selectedProspect;
      option.dataset.niche = item.niche;
      evidenceProspect.append(option);
    }
  });
  if (!selectedProspect && evidenceProspect.options.length > 0) {
    evidenceProspect.selectedIndex = evidenceProspect.options.length - 1;
  }
  loadRoles();
}

async function load() {
  const response = await fetch("/api/demonstrations/library");
  render(await response.json());
}

async function postJson(url, body) {
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });
  const payload = await response.json();
  if (!response.ok) throw new Error(payload.error || "Request failed");
  return payload;
}

document.querySelector("#create").addEventListener("click", async () => {
  notice.textContent = "Creating an original 15-second studio slice…";
  try {
    const clip = await postJson("/api/demonstrations/source-media/studio-slice", {
      market: "Panama City",
      country: "Panama",
      seconds: 15
    });
    latestFile = clip;
    notice.textContent = clip.status + " · quota credit " + clip.quotaCredit;
    await load();
  } catch (error) {
    notice.textContent = error.message;
  }
});

document.querySelector("#again").addEventListener("click", async () => {
  notice.textContent = "Submitting another original slice with the same picture…";
  try {
    const clip = await postJson("/api/demonstrations/source-media/studio-slice", {
      market: "Panama City",
      country: "Panama",
      seconds: 15
    });
    notice.textContent = clip.originalFileName + " → " + clip.status + " · quota credit " + clip.quotaCredit;
    await load();
  } catch (error) {
    notice.textContent = error.message;
  }
});

async function produceProspect(item) {
  if (item.conceptCount > 0) {
    location.href = item.outreachUrl;
    return;
  }
  discovery.textContent = "Compositing one overlay onto a qualified slice…";
  try {
    const page = await postJson("/api/demonstrations/" + item.slug + "/produce", {
      sourceClipId: latestQualified
    });
    discovery.textContent = page.businessName + " demonstration ready. Delivery is " + page.delivery + ". Decision maker " + page.decisionMakerStatus + ".";
    const open = document.createElement("a");
    open.href = page.subject ? "/outreach/" + page.slug : item.outreachUrl;
    open.href = "/outreach/" + page.slug;
    open.textContent = " Open the private message";
    discovery.append(open);
    await load();
  } catch (error) {
    discovery.textContent = error.message;
  }
}

document.querySelector("#discover").addEventListener("submit", async event => {
  event.preventDefault();
  discovery.textContent = "Scoring the prospect…";
  try {
    const page = await postJson("/api/demonstrations/discover", {
      niche: nicheSelect.value,
      market: marketSelect.value,
      businessName: document.querySelector("#businessName").value,
      publicSourceUrl: document.querySelector("#sourceUrl").value
    });
    discovery.textContent = page.businessName + " scored " + page.opportunityScore
      + ". Buying roles: " + page.buyingRoles.join(", ")
      + ". Decision maker " + page.decisionMakerStatus + ". Delivery " + page.delivery + ".";
    await load();
  } catch (error) {
    discovery.textContent = error.message;
  }
});

document.querySelector("#produce").addEventListener("click", async () => {
  notice.textContent = "Compositing overlays, QR codes, and disclosure…";
  try {
    const page = await postJson("/api/demonstrations/abc-pharmacy/produce", {
      sourceClipId: latestQualified,
      conceptCount: 4
    });
    notice.textContent = "Demonstration ready. Delivery is " + page.delivery + ".";
    const open = document.createElement("a");
    open.href = "/outreach/" + page.slug;
    open.textContent = " Open the private message";
    notice.append(open);
  } catch (error) {
    notice.textContent = error.message;
  }
});

async function loadCatalog() {
  const response = await fetch("/api/demonstrations/catalog");
  const catalog = await response.json();
  catalog.niches.forEach(niche => {
    const option = document.createElement("option");
    option.value = niche;
    option.textContent = niche;
    if (niche === "restaurant") option.selected = true;
    nicheSelect.append(option);
  });
  catalog.markets.forEach(market => {
    const option = document.createElement("option");
    option.value = market.city;
    option.textContent = market.city + ", " + market.country;
    if (market.city === "Panama City") option.selected = true;
    marketSelect.append(option);
  });
}

async function loadRoles() {
  const selected = evidenceProspect.selectedOptions[0];
  evidenceRole.replaceChildren();
  if (!selected) return;
  const response = await fetch("/api/demonstrations/buying-roles?niche=" + encodeURIComponent(selected.dataset.niche || ""));
  const payload = await response.json();
  payload.roles.forEach(role => {
    const option = document.createElement("option");
    option.value = role;
    option.textContent = role;
    evidenceRole.append(option);
  });
}

evidenceProspect.addEventListener("change", loadRoles);

document.querySelector("#evidence").addEventListener("submit", async event => {
  event.preventDefault();
  if (!evidenceProspect.value) {
    evidenceNotice.textContent = "Score a prospect before recording a person.";
    return;
  }
  evidenceNotice.textContent = "Checking the public evidence…";
  try {
    const page = await postJson("/api/demonstrations/" + evidenceProspect.value + "/decision-maker", {
      personName: document.querySelector("#personName").value,
      role: evidenceRole.value,
      evidenceKind: document.querySelector("#evidenceKind").value,
      evidenceUrl: document.querySelector("#evidenceUrl").value,
      corroboratingKind: document.querySelector("#corroboratingKind").value,
      corroboratingUrl: document.querySelector("#corroboratingUrl").value,
      contactKind: document.querySelector("#contactKind").value,
      contactValue: document.querySelector("#contactValue").value,
      contactSourceUrl: document.querySelector("#contactSourceUrl").value
    });
    evidenceNotice.textContent = page.businessName + " confidence " + page.decisionMakerStatus
      + " · " + page.contactTier + " · freshness " + page.freshness
      + " · delivery " + page.delivery + ".";
    evidenceNotice.scrollIntoView({ block: "center" });
    await load();
  } catch (error) {
    evidenceNotice.textContent = error.message;
    evidenceNotice.scrollIntoView({ block: "center" });
  }
});

loadCatalog().then(load);
