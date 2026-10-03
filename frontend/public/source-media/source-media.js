const meter = document.querySelector("#meter");
const rows = document.querySelector("#rows");
const notice = document.querySelector("#notice");
const discovery = document.querySelector("#discovery");
const factoryNotice = document.querySelector("#factoryNotice");
const factoryExceptions = document.querySelector("#factoryExceptions");
const evidenceNotice = document.querySelector("#evidenceNotice");
const evidenceProspect = document.querySelector("#evidenceProspect");
const evidenceRole = document.querySelector("#evidenceRole");
const roadNotice = document.querySelector("#roadNotice");
const roadProspect = document.querySelector("#roadProspect");
const prospects = document.querySelector("#prospects");
const eventNotice = document.querySelector("#eventNotice");
const eventProspect = document.querySelector("#eventProspect");
const eventList = document.querySelector("#eventList");
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
  const selectedRoad = roadProspect.value;
  const selectedEvent = eventProspect.value;
  evidenceProspect.replaceChildren();
  roadProspect.replaceChildren();
  eventProspect.replaceChildren();
  library.demonstrations.forEach(item => {
    const row = document.createElement("tr");
    const action = document.createElement("td");
    if (item.opportunityScore === 100 && item.slug !== "abc-pharmacy") {
      const produce = document.createElement("button");
      produce.type = "button";
      produce.textContent = item.conceptCount > 0 ? "Open demonstration" : "Produce one demonstration";
      produce.addEventListener("click", () => produceProspect(item));
      action.append(produce);
    } else if (item.prospectState === "PRESERVED") {
      action.textContent = "No demonstration";
    }
    row.append(
      cell(item.businessName),
      cell(item.niche || ""),
      cell(item.market),
      cell(String(item.opportunityScore ?? "")),
      cell(item.prospectState || ""),
      cell(item.decisionMakerStatus || ""),
      cell(item.suppressed ? "Suppressed" : String(item.roadCount || 0)),
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
    const roadOption = document.createElement("option");
    roadOption.value = item.slug;
    roadOption.textContent = item.businessName + " · " + (item.prospectState || "recorded");
    roadOption.selected = item.slug === selectedRoad;
    roadProspect.append(roadOption);
    const eventOption = document.createElement("option");
    eventOption.value = item.slug;
    eventOption.textContent = item.businessName + " · " + (item.prospectState || "recorded");
    eventOption.selected = item.slug === selectedEvent;
    eventProspect.append(eventOption);
  });
  if (!selectedProspect && evidenceProspect.options.length > 0) {
    evidenceProspect.selectedIndex = evidenceProspect.options.length - 1;
  }
  if (!selectedEvent && eventProspect.options.length > 0) {
    const mesa = [...eventProspect.options].find(option => option.value === "mesa-norte");
    if (mesa) mesa.selected = true;
    else eventProspect.selectedIndex = eventProspect.options.length - 1;
  }
  loadRoles();
  loadEvents();
  loadDeliveryRoads();
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
    const recipe = page.concepts && page.concepts[0];
    discovery.textContent = page.businessName + " demonstration ready. Recipe "
      + (recipe && recipe.recipeVersion ? recipe.recipeVersion : "unversioned")
      + " QA " + (recipe && recipe.qaStatus ? recipe.qaStatus : "unrecorded")
      + ". The flattened composite remains. Delivery is " + page.delivery + ".";
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
    const market = marketSelect.value === "__outside__"
      ? document.querySelector("#outsideMarket").value
      : marketSelect.value;
    const page = await postJson("/api/demonstrations/discover", {
      niche: nicheSelect.value,
      market,
      businessName: document.querySelector("#businessName").value,
      publicSourceUrl: document.querySelector("#sourceUrl").value
    });
    const preserved = page.prospectState === "PRESERVED";
    discovery.textContent = page.businessName + " scored " + page.opportunityScore
      + ". State " + page.prospectState + "."
      + (preserved
        ? " No demonstration was manufactured."
        : " Buying roles: " + page.buyingRoles.join(", ") + ".")
      + " Decision maker " + page.decisionMakerStatus + ". Delivery " + page.delivery + ".";
    discovery.scrollIntoView({ block: "center" });
    await load();
  } catch (error) {
    discovery.textContent = error.message;
    discovery.scrollIntoView({ block: "center" });
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
  const other = document.createElement("option");
  other.value = "other";
  other.textContent = "Other local business";
  nicheSelect.append(other);
  catalog.markets.forEach(market => {
    const option = document.createElement("option");
    option.value = market.city;
    option.textContent = market.city + ", " + market.country;
    if (market.city === "Panama City") option.selected = true;
    marketSelect.append(option);
  });
  const outside = document.createElement("option");
  outside.value = "__outside__";
  outside.textContent = "Outside the initial markets";
  marketSelect.append(outside);
}

marketSelect.addEventListener("change", () => {
  const outside = marketSelect.value === "__outside__";
  document.querySelector("#outsideMarketLabel").hidden = !outside;
  document.querySelector("#outsideMarket").required = outside;
});

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

document.querySelector("#roads").addEventListener("submit", async event => {
  event.preventDefault();
  if (!roadProspect.value) {
    roadNotice.textContent = "Record a prospect before recording a road.";
    roadNotice.scrollIntoView({ block: "center" });
    return;
  }
  roadNotice.textContent = "Recording the public road…";
  try {
    const page = await postJson("/api/demonstrations/" + roadProspect.value + "/contact-roads", {
      kind: document.querySelector("#roadKind").value,
      value: document.querySelector("#roadValue").value,
      sourceUrl: document.querySelector("#roadSource").value
    });
    const latest = page.contactRoads[page.contactRoads.length - 1];
    roadNotice.textContent = page.businessName + " roads " + page.contactRoads.length
      + ". State " + latest.state
      + ". Outreach eligible " + page.outreachEligible
      + ". Delivery " + page.delivery
      + ". A public road is not permission to send.";
    roadNotice.scrollIntoView({ block: "center" });
    await load();
  } catch (error) {
    roadNotice.textContent = error.message;
    roadNotice.scrollIntoView({ block: "center" });
  }
});

roadProspect.addEventListener("change", loadDeliveryRoads);

async function loadDeliveryRoads() {
  const select = document.querySelector("#deliveryRoad");
  if (!select) return;
  const previous = select.value;
  select.replaceChildren();
  if (!roadProspect.value) return;
  const response = await fetch("/api/demonstrations/" + roadProspect.value);
  if (!response.ok) return;
  const page = await response.json();
  (page.contactRoads || []).forEach(road => {
    const option = document.createElement("option");
    option.value = road.id;
    option.textContent = road.kind + " · " + road.value;
    option.selected = road.id === previous;
    select.append(option);
  });
}

document.querySelector("#delivery").addEventListener("submit", async event => {
  event.preventDefault();
  const deliveryNotice = document.querySelector("#deliveryNotice");
  if (!roadProspect.value || !document.querySelector("#deliveryRoad").value) {
    deliveryNotice.textContent = "A delivery decision needs a stored contact road.";
    deliveryNotice.scrollIntoView({ block: "center" });
    return;
  }
  deliveryNotice.textContent = "Applying the preview policy…";
  try {
    const page = await postJson("/api/demonstrations/" + roadProspect.value + "/delivery", {
      roadId: document.querySelector("#deliveryRoad").value,
      policy: document.querySelector("#deliveryPolicy").value,
      adapter: document.querySelector("#deliveryAdapter").value,
      authorization: document.querySelector("#deliveryAuthorization").value
    });
    deliveryNotice.textContent = page.businessName + ". " + page.deliveryNotice
      + " Outreach eligible " + page.outreachEligible
      + ". Delivery " + page.delivery + ".";
    deliveryNotice.scrollIntoView({ block: "center" });
    await load();
  } catch (error) {
    deliveryNotice.textContent = error.message;
    deliveryNotice.scrollIntoView({ block: "center" });
  }
});

document.querySelector("#suppress").addEventListener("submit", async event => {
  event.preventDefault();
  if (!roadProspect.value) {
    roadNotice.textContent = "Record a prospect before suppression.";
    roadNotice.scrollIntoView({ block: "center" });
    return;
  }
  roadNotice.textContent = "Recording suppression…";
  try {
    const page = await postJson("/api/demonstrations/" + roadProspect.value + "/suppression", {
      reason: document.querySelector("#suppressionReason").value
    });
    roadNotice.textContent = page.businessName + " is suppressed. " + page.suppressionReason
      + ". Outreach eligible " + page.outreachEligible
      + ". Delivery " + page.delivery + ".";
    roadNotice.scrollIntoView({ block: "center" });
    await load();
  } catch (error) {
    roadNotice.textContent = error.message;
    roadNotice.scrollIntoView({ block: "center" });
  }
});

async function loadEvents() {
  const slug = eventProspect.value;
  eventList.replaceChildren();
  if (!slug) return;
  const response = await fetch("/api/demonstrations/" + slug);
  if (!response.ok) return;
  const page = await response.json();
  (page.events || []).forEach(item => {
    const line = document.createElement("li");
    line.textContent = item.kind + ". " + item.observation + " AI calls " + item.aiCalls + ". Delivery " + item.delivery + ".";
    eventList.append(line);
  });
}

eventProspect.addEventListener("change", () => {
  eventNotice.textContent = "";
  loadEvents();
});

document.querySelector("#events").addEventListener("submit", async event => {
  event.preventDefault();
  const slug = eventProspect.value;
  if (!slug) {
    eventNotice.textContent = "Record a prospect before recording an event.";
    return;
  }
  eventNotice.textContent = "Recording the event…";
  try {
    const page = await postJson("/api/demonstrations/" + slug + "/events", {
      kind: document.querySelector("#eventKind").value
    });
    eventNotice.textContent = page.kind + ". " + page.observation
      + " AI calls " + page.aiCalls + ". Delivery " + page.delivery + ".";
    eventList.replaceChildren();
    (page.events || []).forEach(item => {
      const line = document.createElement("li");
      line.textContent = item.kind + ". " + item.observation + " AI calls " + item.aiCalls + ". Delivery NOT_SENT.";
      eventList.append(line);
    });
    eventNotice.scrollIntoView({ block: "center" });
  } catch (error) {
    eventNotice.textContent = error.message;
    eventNotice.scrollIntoView({ block: "center" });
    await loadEvents();
  }
});

document.querySelector("#factory").addEventListener("click", async () => {
  factoryNotice.textContent = "Running factory QA…";
  factoryExceptions.replaceChildren();
  try {
    const batch = await postJson("/api/demonstrations/factory-batch", {});
    factoryNotice.textContent = "Status " + batch.status
      + ". Prospects " + batch.prospectCount
      + ". Preserved " + batch.preservedCount
      + ". Suppressed " + batch.suppressedCount
      + ". Demonstrations " + batch.demonstrationCount
      + ". Concepts " + batch.conceptCount
      + ". Checks passed " + batch.checksPassed
      + ". Exceptions " + batch.exceptionCount
      + ". AI calls " + batch.aiCalls
      + ". " + batch.cost
      + " Elapsed " + batch.elapsedMilliseconds + " ms. Delivery " + batch.delivery + ".";
    (batch.exceptions || []).forEach(item => {
      const line = document.createElement("li");
      line.textContent = item;
      factoryExceptions.append(line);
    });
    factoryNotice.scrollIntoView({ block: "center" });
  } catch (error) {
    factoryNotice.textContent = error.message;
    factoryNotice.scrollIntoView({ block: "center" });
  }
});

loadCatalog().then(load);
