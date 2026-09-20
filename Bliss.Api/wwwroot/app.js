const state = {
  apiBase: localStorage.getItem("bliss-api-base") || "",
  matches: [],
  creators: [],
  advertisers: [],
  opportunities: [],
  content: [],
  runs: [],
  ingestions: [],
  formations: [],
  ruleVersions: [],
  reviews: [],
  reviewQueue: [],
  matchFilter: "ALL",
  matchSearch: ""
};

const $ = (selector, root = document) => root.querySelector(selector);
const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];

document.addEventListener("DOMContentLoaded", () => {
  bindNavigation();
  bindActions();
  generateIdempotencyKey();
  generateFormationIdempotencyKey();
  generateReviewIdempotencyKey();
  loadDashboard();
});

function api(path, options = {}) {
  const base = state.apiBase.replace(/\/$/, "");
  return fetch(`${base}${path}`, {
    ...options,
    headers: { Accept: "application/json", ...(options.headers || {}) }
  }).then(async response => {
    const body = response.status === 204 ? null : await response.json().catch(() => null);
    if (!response.ok) {
      const message = body?.error || `Request failed with HTTP ${response.status}`;
      throw new Error(message);
    }
    return body;
  });
}

async function loadDashboard() {
  setConnection("loading");
  $("#refresh-button").classList.add("spinning");
  try {
    const [matches, creators, advertisers, opportunities, content, runs, ingestions, formations, ruleVersions, reviews, reviewQueue] = await Promise.all([
      api("/api/bliss/matches"),
      api("/api/creators"),
      api("/api/advertisers"),
      api("/api/advertiser-opportunities"),
      api("/api/content-items"),
      api("/api/match-evaluation-runs"),
      api("/api/creator-ingestions"),
      api("/api/match-formation-runs"),
      api("/api/rule-versions"),
      api("/api/match-review-decisions"),
      api("/api/match-reviews/queue")
    ]);
    const ruleDetails = await Promise.all(ruleVersions.map(rule =>
      api(`/api/rule-versions/${rule.id}`).catch(() => rule)
    ));
    Object.assign(state, { matches, creators, advertisers, opportunities, content, runs, ingestions, formations, ruleVersions: ruleDetails, reviews, reviewQueue });
    renderAll();
    setConnection("online");
  } catch (error) {
    setConnection("offline", error.message);
    renderUnavailable(error.message);
    toast(error.message, true);
  } finally {
    $("#refresh-button").classList.remove("spinning");
  }
}

function renderAll() {
  renderOverview();
  renderMatches();
  renderCreators();
  renderAdvertisers();
  renderInventory();
  renderAudit();
  renderIngestions();
  renderFormations();
  renderReviews();
  $("#nav-match-count").textContent = state.matches.length;
  $("#nav-ingest-count").textContent = state.ingestions.length;
  $("#nav-formation-count").textContent = state.formations.length;
  $("#nav-review-count").textContent = state.reviewQueue.length;
}

function renderOverview() {
  const approved = state.matches.filter(match => match.status === "APPROVED").length;
  const approvalRate = state.matches.length ? Math.round((approved / state.matches.length) * 100) : 0;
  const countries = new Set(state.creators.map(x => x.countryCode).filter(Boolean));
  $("#stat-matches").textContent = state.matches.length;
  $("#stat-approved").textContent = approved;
  $("#stat-creators").textContent = state.creators.length;
  $("#stat-runs").textContent = state.runs.length;
  $("#stat-countries").textContent = `Across ${countries.size || "—"} markets`;
  $("#approval-trend").textContent = `${approvalRate}% RATE`;
  $("#hero-score").textContent = `${approvalRate}%`;

  const statuses = [
    ["APPROVED", "Approved", "#57c995"],
    ["REVIEW_REQUIRED", "Review required", "#e5a54f"],
    ["INELIGIBLE", "Ineligible", "#d7655d"],
    ["CREATED", "Not evaluated", "#87938e"]
  ];
  $("#status-chart").innerHTML = statuses.map(([status, label, color]) => {
    const count = state.matches.filter(match => match.status === status).length;
    const width = state.matches.length ? (count / state.matches.length) * 100 : 0;
    return `<div class="status-row">
      <label><span class="status-dot" style="background:${color}"></span>${label}</label>
      <div class="status-bar"><span style="width:${width}%;background:${color}"></span></div>
      <strong>${count}</strong>
    </div>`;
  }).join("");

  const recent = [...state.runs]
    .sort((a, b) => new Date(b.completedAt || b.startedAt) - new Date(a.completedAt || a.startedAt))
    .slice(0, 4);
  $("#recent-runs").innerHTML = recent.length ? recent.map(run => {
    const creator = creatorById(run.creatorId);
    return `<button class="activity-item text-button" data-run-id="${run.id}">
      <span class="activity-icon ${statusClass(run.matchStatus)}">${statusSymbol(run.matchStatus)}</span>
      <span><strong>${escapeHtml(creator?.name || "Unknown creator")}</strong><small>${friendlyStatus(run.matchStatus)} · score ${formatScore(run.overallScore)}</small></span>
      <span class="activity-time">${relativeTime(run.completedAt || run.startedAt)}</span>
    </button>`;
  }).join("") : emptyState("No evaluations recorded yet.");
}

function renderMatches() {
  const term = state.matchSearch.toLowerCase();
  const matches = state.matches.filter(match => {
    const creator = creatorById(match.creatorId);
    const opportunity = opportunityById(match.advertiserOpportunityId);
    const matchesFilter = state.matchFilter === "ALL" || match.status === state.matchFilter;
    const haystack = `${match.id} ${creator?.name || ""} ${opportunity?.name || ""}`.toLowerCase();
    return matchesFilter && haystack.includes(term);
  });

  $("#match-list").innerHTML = matches.length ? matches.map(match => {
    const creator = creatorById(match.creatorId);
    const opportunity = opportunityById(match.advertiserOpportunityId);
    const initials = getInitials(creator?.name || "Unknown");
    const canEvaluate = state.ruleVersions.some(rule => rule.id === match.ruleVersionId && rule.documentJson);
    return `<article class="match-row">
      <div class="entity-name">
        <span class="mini-avatar">${initials}</span>
        <span><strong>${escapeHtml(creator?.name || "Unknown creator")}</strong><small>${escapeHtml(creator?.countryCode || "Unknown market")} · ${escapeHtml(creator?.primaryLanguage || "Unknown language")}</small></span>
      </div>
      <div class="match-cell opportunity-cell"><strong>${escapeHtml(opportunity?.name || "Unknown opportunity")}</strong><small>${escapeHtml(opportunity?.category || "Uncategorized")}</small></div>
      <span class="status-badge ${statusClass(match.status)}">${friendlyStatus(match.status)}</span>
      <div class="score-wrap">
        <span class="score-ring" style="--score:${(match.overallScore || 0) * 100}"><span>${formatScore(match.overallScore)}</span></span>
        <div class="match-cell"><strong>Overall</strong><small>${formatPercent(match.confidenceScore)} confidence</small></div>
      </div>
      <div class="row-actions">
        ${canEvaluate ? `<button class="small-button evaluate" data-evaluate="${match.id}">Replay</button>` : ""}
        <button class="small-button" data-match-id="${match.id}">Inspect</button>
      </div>
    </article>`;
  }).join("") : emptyState("No matches fit the selected filters.");
}

function renderCreators() {
  $("#creator-grid").innerHTML = state.creators.length ? state.creators.map(creator => {
    const matchCount = state.matches.filter(match => match.creatorId === creator.id).length;
    return `<article class="entity-card">
      <div class="entity-card-head"><span class="mini-avatar">${getInitials(creator.name)}</span><span class="country-tag">${escapeHtml(creator.countryCode || "UNKNOWN")}</span></div>
      <h3>${escapeHtml(creator.name)}</h3>
      <p>${escapeHtml(creator.primaryLanguage || "Language unknown")}</p>
      <div class="entity-metrics">
        <div><span>Audience</span><strong>${formatNumber(creator.audienceSize)}</strong></div>
        <div><span>Matches</span><strong>${matchCount}</strong></div>
        <div><span>Female</span><strong>${formatNullablePercent(creator.femalePercentage)}</strong></div>
        <div><span>Male</span><strong>${formatNullablePercent(creator.malePercentage)}</strong></div>
      </div>
    </article>`;
  }).join("") : emptyState("No creators found.");
}

function renderAdvertisers() {
  $("#advertiser-grid").innerHTML = state.advertisers.length ? state.advertisers.map(advertiser => {
    const opportunityCount = state.opportunities.filter(opportunity =>
      opportunity.advertiserProgramId && opportunity.name
    ).length;
    return `<article class="entity-card">
      <div class="entity-card-head"><span class="mini-avatar">${getInitials(advertiser.name)}</span><span class="country-tag">${escapeHtml(advertiser.countryCode || "GLOBAL")}</span></div>
      <h3>${escapeHtml(advertiser.name)}</h3>
      <p>${advertiser.website ? escapeHtml(new URL(advertiser.website).hostname) : "No public website"}</p>
      <div class="entity-metrics">
        <div><span>Market</span><strong>${escapeHtml(advertiser.countryCode || "Global")}</strong></div>
        <div><span>Directory</span><strong>Active</strong></div>
      </div>
    </article>`;
  }).join("") : emptyState("No advertisers found.");
}

async function renderInventory() {
  const details = await Promise.all(state.content.map(item =>
    api(`/api/content-items/${item.id}`).catch(() => ({ ...item, adInventorySlots: [] }))
  ));
  $("#inventory-list").innerHTML = details.length ? details.map(item => {
    const creator = creatorById(item.creatorId);
    return `<article class="inventory-card">
      <div class="inventory-art">${contentSymbol(item.contentType)}</div>
      <div class="inventory-content">
        <span class="country-tag">${escapeHtml(item.contentType)}</span>
        <h3>${escapeHtml(item.title)}</h3>
        <p>${escapeHtml(creator?.name || "Unknown creator")} · ${item.adInventorySlots?.length || 0} slots</p>
        <div class="slot-row">${(item.adInventorySlots || []).map(slot => `<span class="slot-tag">${friendlyStatus(slot.slotType)}</span>`).join("") || `<span class="slot-tag">NO SLOTS</span>`}</div>
      </div>
    </article>`;
  }).join("") : emptyState("No content inventory found.");
}

function renderAudit() {
  const runs = [...state.runs].sort((a, b) => new Date(b.completedAt || b.startedAt) - new Date(a.completedAt || a.startedAt));
  $("#audit-table").innerHTML = runs.length ? runs.map(run => `
    <tr>
      <td><code>${shortId(run.id)}</code></td>
      <td><code>${shortId(run.blissMatchId)}</code></td>
      <td><span class="status-badge ${statusClass(run.matchStatus)}">${friendlyStatus(run.matchStatus)}</span></td>
      <td><strong>${formatScore(run.overallScore)}</strong></td>
      <td><code>${escapeHtml(run.algorithmVersion)}</code></td>
      <td>${formatDate(run.completedAt || run.startedAt)}</td>
      <td><button class="small-button" data-run-id="${run.id}">Inspect</button></td>
    </tr>`).join("") : `<tr><td colspan="7">${emptyState("No evaluation history found.")}</td></tr>`;
}

function renderIngestions() {
  const runs = [...state.ingestions]
    .sort((a, b) => new Date(b.completedAt) - new Date(a.completedAt))
    .slice(0, 8);

  $("#ingestion-list").innerHTML = runs.length ? runs.map(run => {
    const creator = creatorById(run.creatorId);
    return `<button class="activity-item ingest-run" data-ingest-run-id="${run.id}">
      <span class="activity-icon">↓</span>
      <span><strong>${escapeHtml(creator?.name || run.identityKey)}</strong><small>${escapeHtml(run.sourceSystem)} · ${friendlyStatus(run.outcome)}</small></span>
      <span class="activity-time">${relativeTime(run.completedAt)}</span>
    </button>`;
  }).join("") : emptyState("No creator observations ingested yet.");
}

function renderFormations() {
  $("#formation-creator").innerHTML = state.creators
    .map(creator => `<option value="${creator.id}">${escapeHtml(creator.name)} · ${escapeHtml(creator.countryCode || "Unknown market")}</option>`)
    .join("");
  $("#formation-opportunity").innerHTML = state.opportunities
    .filter(opportunity => opportunity.status === "ACTIVE")
    .map(opportunity => `<option value="${opportunity.id}">${escapeHtml(opportunity.name)} · ${escapeHtml(opportunity.category || "Uncategorized")}</option>`)
    .join("");
  $("#formation-rule").innerHTML = state.ruleVersions
    .filter(rule => rule.isActive)
    .map(rule => `<option value="${rule.id}" ${rule.documentJson ? "" : "disabled"}>${escapeHtml(rule.version)} · ${escapeHtml(rule.name)}${rule.documentJson ? "" : " (no evaluator document)"}</option>`)
    .join("");

  const runs = [...state.formations]
    .sort((a, b) => new Date(b.completedAt) - new Date(a.completedAt))
    .slice(0, 8);
  $("#formation-list").innerHTML = runs.length ? runs.map(run => {
    const creator = creatorById(run.creatorId);
    const opportunity = opportunityById(run.advertiserOpportunityId);
    return `<button class="activity-item ingest-run" data-formation-run-id="${run.id}">
      <span class="activity-icon">＋</span>
      <span><strong>${escapeHtml(creator?.name || shortId(run.creatorId))}</strong><small>${escapeHtml(opportunity?.name || shortId(run.advertiserOpportunityId))} · ${run.evaluateOnCreate ? "Evaluated" : "Created"}</small></span>
      <span class="activity-time">${relativeTime(run.completedAt)}</span>
    </button>`;
  }).join("") : emptyState("No controlled match formations yet.");
}

async function submitFormation(form) {
  const submit = $("#formation-submit");
  const formData = new FormData(form);
  const payload = {
    sourceSystem: String(formData.get("sourceSystem") || "").trim(),
    idempotencyKey: String(formData.get("idempotencyKey") || "").trim(),
    creatorId: String(formData.get("creatorId") || ""),
    advertiserOpportunityId: String(formData.get("advertiserOpportunityId") || ""),
    ruleVersionId: String(formData.get("ruleVersionId") || ""),
    evaluateOnCreate: formData.get("evaluateOnCreate") === "on"
  };

  submit.disabled = true;
  submit.innerHTML = "Forming…";
  try {
    const result = await api("/api/bliss/matches", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    toast(`${friendlyStatus(result.matchStatus)} · match ${result.isReplay ? "replayed" : "formed"}`);
    generateFormationIdempotencyKey();
    await loadDashboard();
    openMatch(result.blissMatchId);
  } catch (error) {
    toast(error.message, true);
  } finally {
    submit.disabled = false;
    submit.innerHTML = `Form match <span>→</span>`;
  }
}

async function openFormationRun(runId) {
  openDrawer("Match formation", `<div class="activity-skeleton"></div><div class="activity-skeleton"></div>`);
  try {
    const run = await api(`/api/match-formation-runs/${runId}`);
    const creator = creatorById(run.creatorId);
    const opportunity = opportunityById(run.advertiserOpportunityId);
    $("#drawer-title").textContent = creator?.name || shortId(run.creatorId);
    $("#drawer-body").innerHTML = `
      <div class="detail-hero">
        <div class="detail-hero-top"><span class="status-badge status-completed">${run.evaluateOnCreate ? "Evaluated" : "Created"}</span><span class="detail-score">＋</span></div>
        <h3>${escapeHtml(opportunity?.name || shortId(run.advertiserOpportunityId))}</h3>
        <p>${escapeHtml(run.sourceSystem)} · ${formatDate(run.completedAt)}</p>
      </div>
      <section class="detail-section"><h4>Match certificate</h4><div class="check-item"><div><strong>${shortId(run.blissMatchId)}</strong><small>Explicit creator + opportunity + rule version</small></div><button class="small-button" data-match-id="${run.blissMatchId}">Inspect</button></div></section>
      <section class="detail-section"><h4>Input snapshot</h4><pre class="json-block">${prettyJson(run.inputSnapshot)}</pre></section>`;
  } catch (error) {
    $("#drawer-body").innerHTML = emptyState(error.message);
  }
}

function renderReviews() {
  $("#review-match").innerHTML = (state.reviewQueue || []).map(item =>
    `<option value="${item.blissMatchId}">${escapeHtml(item.creatorName)} · ${escapeHtml(item.opportunityName)} · ${formatScore(item.overallScore)}</option>`
  ).join("") || `<option value="">No REVIEW_REQUIRED matches</option>`;

  const decisions = [...(state.reviews || [])]
    .sort((a, b) => new Date(b.completedAt) - new Date(a.completedAt))
    .slice(0, 8);
  $("#review-list").innerHTML = decisions.length ? decisions.map(decision => {
    const creator = creatorById(decision.creatorId);
    return `<button class="activity-item ingest-run" data-review-id="${decision.id}">
      <span class="activity-icon">☑</span>
      <span><strong>${escapeHtml(creator?.name || shortId(decision.creatorId))}</strong><small>${escapeHtml(decision.reviewerLabel)} · ${friendlyStatus(decision.decision)}</small></span>
      <span class="activity-time">${relativeTime(decision.completedAt)}</span>
    </button>`;
  }).join("") : emptyState("No human review decisions yet.");
}

async function submitReview(form) {
  const submit = $("#review-submit");
  const formData = new FormData(form);
  const payload = {
    sourceSystem: String(formData.get("sourceSystem") || "").trim(),
    idempotencyKey: String(formData.get("idempotencyKey") || "").trim(),
    blissMatchId: String(formData.get("blissMatchId") || ""),
    reviewerLabel: String(formData.get("reviewerLabel") || "").trim(),
    decision: String(formData.get("decision") || "").trim(),
    rationale: String(formData.get("rationale") || "").trim()
  };

  submit.disabled = true;
  submit.innerHTML = "Recording…";
  try {
    const result = await api("/api/match-review-decisions", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    toast(`${friendlyStatus(result.decision)} · ${friendlyStatus(result.resultingMatchStatus)}${result.isReplay ? " (replay)" : ""}`);
    generateReviewIdempotencyKey();
    form.elements.rationale.value = "";
    await loadDashboard();
    openMatch(result.blissMatchId);
  } catch (error) {
    toast(error.message, true);
  } finally {
    submit.disabled = false;
    submit.innerHTML = `Record decision <span>→</span>`;
  }
}

async function openReviewDecision(id) {
  openDrawer("Review decision", `<div class="activity-skeleton"></div><div class="activity-skeleton"></div>`);
  try {
    const decision = await api(`/api/match-review-decisions/${id}`);
    const creator = creatorById(decision.creatorId);
    $("#drawer-title").textContent = creator?.name || shortId(decision.creatorId);
    $("#drawer-body").innerHTML = `
      <div class="detail-hero">
        <div class="detail-hero-top"><span class="status-badge ${statusClass(decision.resultingMatchStatus)}">${friendlyStatus(decision.decision)}</span><span class="detail-score">☑</span></div>
        <h3>${escapeHtml(decision.reviewerLabel)}</h3>
        <p>${escapeHtml(decision.sourceSystem)} · ${formatDate(decision.completedAt)}</p>
      </div>
      <section class="detail-section"><h4>Resulting status</h4><div class="check-item"><div><strong>${friendlyStatus(decision.resultingMatchStatus)}</strong><small>${escapeHtml(decision.rationale)}</small></div><button class="small-button" data-match-id="${decision.blissMatchId}">Inspect match</button></div></section>
      <section class="detail-section"><h4>Input snapshot</h4><pre class="json-block">${prettyJson(decision.inputSnapshot)}</pre></section>`;
  } catch (error) {
    $("#drawer-body").innerHTML = emptyState(error.message);
  }
}

async function submitIngestion(form) {
  const submit = $("#ingest-submit");
  const formData = new FormData(form);
  const value = name => String(formData.get(name) || "").trim();
  const nullableNumber = name => value(name) === "" ? null : Number(value(name));
  const payload = {
    sourceSystem: value("sourceSystem"),
    idempotencyKey: value("idempotencyKey"),
    platform: value("platform"),
    externalProfileId: value("externalProfileId"),
    creatorName: value("creatorName"),
    profileUrl: value("profileUrl") || null,
    countryCode: value("countryCode") || null,
    primaryLanguage: value("primaryLanguage") || null,
    audienceSize: nullableNumber("audienceSize"),
    followers: nullableNumber("followers"),
    femalePercentage: null,
    malePercentage: null,
    primaryAgeRange: null,
    primaryGeography: null,
    engagementLevel: null,
    sourceUrl: value("sourceUrl") || null,
    confidenceLevel: value("confidenceLevel") || "UNKNOWN",
    collectedAt: new Date().toISOString()
  };

  submit.disabled = true;
  submit.innerHTML = "Ingesting…";
  try {
    const result = await api("/api/creator-ingestions", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    toast(`${result.outcome} · ${result.identityKey}${result.isReplay ? " (idempotent replay)" : ""}`);
    form.reset();
    form.elements.sourceSystem.value = "DASHBOARD_MANUAL";
    generateIdempotencyKey();
    await loadDashboard();
  } catch (error) {
    toast(error.message, true);
  } finally {
    submit.disabled = false;
    submit.innerHTML = `Ingest observation <span>→</span>`;
  }
}

async function openIngestionRun(runId) {
  openDrawer("Creator ingestion", `<div class="activity-skeleton"></div><div class="activity-skeleton"></div>`);
  try {
    const run = await api(`/api/creator-ingestions/${runId}`);
    const creator = creatorById(run.creatorId);
    $("#drawer-title").textContent = creator?.name || shortId(run.creatorId);
    $("#drawer-body").innerHTML = `
      <div class="detail-hero">
        <div class="detail-hero-top"><span class="status-badge status-completed">${friendlyStatus(run.outcome)}</span><span class="detail-score">↓</span></div>
        <h3>${escapeHtml(run.identityKey)}</h3>
        <p>${escapeHtml(run.sourceSystem)} · ${formatDate(run.completedAt)}</p>
      </div>
      <section class="detail-section"><h4>Canonical identity</h4><div class="check-item"><div><strong>${escapeHtml(run.identityKey)}</strong><small>Provider platform + external profile ID</small></div><span class="status-badge status-approved">Resolved</span></div></section>
      <section class="detail-section"><h4>Input snapshot</h4><pre class="json-block">${prettyJson(run.inputSnapshot)}</pre></section>`;
  } catch (error) {
    $("#drawer-body").innerHTML = emptyState(error.message);
  }
}

async function openMatch(matchId) {
  openDrawer("Loading match…", `<div class="activity-skeleton"></div><div class="activity-skeleton"></div>`);
  try {
    const [match, runs] = await Promise.all([
      api(`/api/bliss/matches/${matchId}`),
      api(`/api/bliss/matches/${matchId}/evaluation-runs`)
    ]);
    $("#drawer-title").textContent = match.creator.name;
    $("#drawer-body").innerHTML = `
      <div class="detail-hero">
        <div class="detail-hero-top"><span class="status-badge ${statusClass(match.status)}">${friendlyStatus(match.status)}</span><span class="detail-score">${formatScore(match.overallScore)}</span></div>
        <h3>${escapeHtml(match.advertiserOpportunity.name)}</h3>
        <p>${escapeHtml(match.advertiserOpportunity.advertiserProgram.advertiser.name)} · Rule ${escapeHtml(match.ruleVersion.version)}</p>
      </div>
      <section class="detail-section">
        <h4>Eligibility checks</h4>
        <div class="check-list">${match.eligibilityChecks.length ? match.eligibilityChecks.map(check => `
          <div class="check-item"><div><strong>${friendlyStatus(check.checkType)}</strong><small>${escapeHtml(check.explanation || check.reasonCode || "No explanation")}</small></div><span class="status-badge ${statusClass(check.result)}">${friendlyStatus(check.result)}</span></div>
        `).join("") : emptyState("This rule version has not been evaluated.")}</div>
      </section>
      <section class="detail-section">
        <h4>Score components</h4>
        <div class="check-list">${match.scoreComponents.length ? match.scoreComponents.map(score => `
          <div class="check-item"><div><strong>${friendlyStatus(score.componentName)}</strong><small>${escapeHtml(score.explanation || "")}</small></div><strong>${formatScore(score.score)} × ${formatScore(score.weight)}</strong></div>
        `).join("") : emptyState("No score components recorded.")}</div>
      </section>
      <section class="detail-section">
        <h4>Evaluation history · ${runs.length}</h4>
        ${runs.length ? runs.map(run => `<button class="history-item text-button" data-run-id="${run.id}"><span><strong>${shortId(run.id)}</strong><small>${formatDate(run.completedAt || run.startedAt)}</small></span><span class="status-badge ${statusClass(run.matchStatus)}">${friendlyStatus(run.matchStatus)}</span></button>`).join("") : emptyState("No historical runs for this match.")}
      </section>`;
  } catch (error) {
    $("#drawer-body").innerHTML = emptyState(error.message);
  }
}

async function openRun(runId) {
  openDrawer("Evaluation run", `<div class="activity-skeleton"></div><div class="activity-skeleton"></div>`);
  try {
    const run = await api(`/api/match-evaluation-runs/${runId}`);
    $("#drawer-title").textContent = shortId(run.id);
    $("#drawer-body").innerHTML = `
      <div class="detail-hero">
        <div class="detail-hero-top"><span class="status-badge ${statusClass(run.matchStatus)}">${friendlyStatus(run.matchStatus)}</span><span class="detail-score">${formatScore(run.overallScore)}</span></div>
        <h3>${escapeHtml(run.algorithmVersion)}</h3>
        <p>Completed ${formatDate(run.completedAt || run.startedAt)} · confidence ${formatPercent(run.confidenceScore)}</p>
      </div>
      <section class="detail-section"><h4>Input snapshot</h4><pre class="json-block">${prettyJson(run.inputSnapshot)}</pre></section>
      <section class="detail-section"><h4>Output snapshot</h4><pre class="json-block">${prettyJson(run.outputSnapshot)}</pre></section>`;
  } catch (error) {
    $("#drawer-body").innerHTML = emptyState(error.message);
  }
}

async function evaluateMatch(matchId, button) {
  const original = button.textContent;
  button.disabled = true;
  button.textContent = "Running…";
  try {
    const result = await api(`/api/bliss/matches/${matchId}/evaluate-rules`, { method: "POST" });
    toast(`${friendlyStatus(result.status)} · evaluation appended to audit`);
    await loadDashboard();
    openMatch(matchId);
  } catch (error) {
    toast(error.message, true);
  } finally {
    button.disabled = false;
    button.textContent = original;
  }
}

function bindNavigation() {
  $$(".nav-item[data-view]").forEach(button => button.addEventListener("click", () => switchView(button.dataset.view)));
  $$("[data-go]").forEach(button => button.addEventListener("click", () => switchView(button.dataset.go)));
  $("#mobile-menu").addEventListener("click", () => $(".sidebar").classList.toggle("open"));
}

function switchView(view) {
  $$(".view").forEach(element => element.classList.toggle("active", element.id === `view-${view}`));
  $$(".nav-item[data-view]").forEach(button => button.classList.toggle("active", button.dataset.view === view));
  const titles = { overview: greeting(), matches: "Match certificates", formation: "Controlled match formation", review: "Human review queue", creators: "Creator directory", ingest: "Creator ingestion", advertisers: "Partner directory", inventory: "Content inventory", audit: "Evaluation ledger" };
  $("#page-title").textContent = titles[view] || "Bliss Chapel";
  $(".sidebar").classList.remove("open");
  window.scrollTo({ top: 0, behavior: "smooth" });
}

function bindActions() {
  $("#refresh-button").addEventListener("click", loadDashboard);
  $("#ingest-form").addEventListener("submit", event => {
    event.preventDefault();
    submitIngestion(event.currentTarget);
  });
  $("#formation-form").addEventListener("submit", event => {
    event.preventDefault();
    submitFormation(event.currentTarget);
  });
  $("#review-form").addEventListener("submit", event => {
    event.preventDefault();
    submitReview(event.currentTarget);
  });
  $("#regenerate-key").addEventListener("click", generateIdempotencyKey);
  $("#regenerate-formation-key").addEventListener("click", generateFormationIdempotencyKey);
  $("#regenerate-review-key").addEventListener("click", generateReviewIdempotencyKey);
  $("#drawer-close").addEventListener("click", closeDrawer);
  $("#drawer-backdrop").addEventListener("click", closeDrawer);
  $("#settings-button").addEventListener("click", () => {
    $("#api-url").value = state.apiBase;
    $("#settings-dialog").showModal();
  });
  $("#save-settings").addEventListener("click", event => {
    event.preventDefault();
    state.apiBase = $("#api-url").value.trim().replace(/\/$/, "");
    localStorage.setItem("bliss-api-base", state.apiBase);
    $("#settings-dialog").close();
    loadDashboard();
  });
  $("#match-search").addEventListener("input", event => {
    state.matchSearch = event.target.value;
    renderMatches();
  });
  $("#match-filters").addEventListener("click", event => {
    const button = event.target.closest("[data-status]");
    if (!button) return;
    state.matchFilter = button.dataset.status;
    $$("#match-filters .filter-chip").forEach(chip => chip.classList.toggle("active", chip === button));
    renderMatches();
  });
  document.addEventListener("click", event => {
    const matchButton = event.target.closest("[data-match-id]");
    const runButton = event.target.closest("[data-run-id]");
    const ingestRunButton = event.target.closest("[data-ingest-run-id]");
    const formationRunButton = event.target.closest("[data-formation-run-id]");
    const reviewButton = event.target.closest("[data-review-id]");
    const evaluateButton = event.target.closest("[data-evaluate]");
    if (matchButton) openMatch(matchButton.dataset.matchId);
    if (runButton) openRun(runButton.dataset.runId);
    if (ingestRunButton) openIngestionRun(ingestRunButton.dataset.ingestRunId);
    if (formationRunButton) openFormationRun(formationRunButton.dataset.formationRunId);
    if (reviewButton) openReviewDecision(reviewButton.dataset.reviewId);
    if (evaluateButton) evaluateMatch(evaluateButton.dataset.evaluate, evaluateButton);
  });
  document.addEventListener("keydown", event => {
    if (event.key === "Escape") closeDrawer();
  });
}

function openDrawer(title, body) {
  $("#drawer-title").textContent = title;
  $("#drawer-body").innerHTML = body;
  $("#detail-drawer").classList.add("open");
  $("#detail-drawer").setAttribute("aria-hidden", "false");
  $("#drawer-backdrop").classList.add("open");
}
function closeDrawer() {
  $("#detail-drawer").classList.remove("open");
  $("#detail-drawer").setAttribute("aria-hidden", "true");
  $("#drawer-backdrop").classList.remove("open");
}
function setConnection(status, detail) {
  const dot = $("#connection-dot");
  dot.className = `pulse-dot ${status === "loading" ? "" : status}`;
  $("#connection-label").textContent = status === "online" ? "API connected" : status === "offline" ? "API unavailable" : "Connecting";
  $("#connection-detail").textContent = detail || (state.apiBase ? new URL(state.apiBase).host : "Same-origin backend");
}
function renderUnavailable(message) {
  const content = emptyState(`Could not load backend data: ${message}`);
  ["match-list", "creator-grid", "advertiser-grid", "inventory-list", "recent-runs", "status-chart", "ingestion-list", "formation-list", "review-list"].forEach(id => $(`#${id}`).innerHTML = content);
  $("#audit-table").innerHTML = `<tr><td colspan="7">${content}</td></tr>`;
}
function toast(message, isError = false) {
  const element = $("#toast");
  element.textContent = message;
  element.className = `toast show${isError ? " error" : ""}`;
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => element.classList.remove("show"), 3300);
}

function creatorById(id) { return state.creators.find(item => item.id === id); }
function opportunityById(id) { return state.opportunities.find(item => item.id === id); }
function statusClass(status = "CREATED") { return `status-${status.toLowerCase()}`; }
function statusSymbol(status) { return status === "APPROVED" ? "✓" : status === "INELIGIBLE" ? "×" : status === "REVIEW_REQUIRED" ? "!" : "◇"; }
function friendlyStatus(value = "UNKNOWN") { return value.replaceAll("_", " ").toLowerCase().replace(/\b\w/g, char => char.toUpperCase()); }
function formatScore(value) { return value == null ? "—" : Number(value).toFixed(2); }
function formatPercent(value) { return value == null ? "—" : `${Math.round(Number(value) * 100)}%`; }
function formatNullablePercent(value) { return value == null ? "Unknown" : `${Number(value).toFixed(0)}%`; }
function formatNumber(value) { return value == null ? "Unknown" : new Intl.NumberFormat("en", { notation: "compact" }).format(value); }
function formatDate(value) { return value ? new Intl.DateTimeFormat("en", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value)) : "—"; }
function relativeTime(value) {
  if (!value) return "—";
  const seconds = Math.round((new Date(value) - new Date()) / 1000);
  const ranges = [[86400, "day"], [3600, "hour"], [60, "minute"]];
  for (const [unit, name] of ranges) if (Math.abs(seconds) >= unit) return new Intl.RelativeTimeFormat("en", { numeric: "auto" }).format(Math.round(seconds / unit), name);
  return "just now";
}
function shortId(id = "") { return `${id.slice(0, 8)}…${id.slice(-4)}`; }
function getInitials(value) { return value.split(/\s+/).slice(0, 2).map(part => part[0]).join("").toUpperCase(); }
function contentSymbol(type) { return type === "VIDEO" ? "▶" : type === "PODCAST" ? "◉" : "▦"; }
function greeting() {
  const hour = new Date().getHours();
  return hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
}
function generateIdempotencyKey() {
  const field = $("#ingest-idempotency");
  if (field) field.value = `manual-${crypto.randomUUID()}`;
}
function generateFormationIdempotencyKey() {
  const field = $("#formation-idempotency");
  if (field) field.value = `manual-${crypto.randomUUID()}`;
}
function generateReviewIdempotencyKey() {
  const field = $("#review-idempotency");
  if (field) field.value = `manual-${crypto.randomUUID()}`;
}
function prettyJson(value) {
  try { return escapeHtml(JSON.stringify(JSON.parse(value), null, 2)); }
  catch { return escapeHtml(value || "No snapshot"); }
}
function emptyState(message) { return `<div class="empty-state">${escapeHtml(message)}</div>`; }
function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" })[char]);
}
