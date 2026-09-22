const state = {
  apiBase: localStorage.getItem("bliss-api-base") || "",
  operator: localStorage.getItem("bliss-operator-label") || "",
  session: {
    authenticationEnabled: false,
    accessAllowed: true,
    isAuthenticated: false,
    displayName: null,
    roles: [],
    canWrite: true,
    canReview: true,
    csrfToken: null
  },
  loaded: false,
  matches: [], creators: [], advertisers: [], programs: [], opportunities: [],
  content: [], contentDetails: [], campaigns: [], ruleVersions: [],
  runs: [], ingestions: [], formations: [], reviews: [], placements: [],
  reviewQueue: [], placementQueue: [], provenances: [],
  weddingPlannerWorkspaces: [], weddingPlannerSessions: [], weddingPlannerMessages: [],
  weddingPlannerAgentRuns: [], weddingPlannerBrandDna: null,
  selectedWeddingPlannerWorkspace: null, selectedWeddingPlannerSession: null,
  affiliateNetworks: [], networkAccesses: [], programAccesses: [],
  matchFilter: "ALL", matchSearch: "", creatorSearch: "", auditSearch: "",
  partnerTab: "advertisers", inventoryTab: "content", auditTab: "evaluations",
  selectedReview: null, selectedPlacement: null,
  runtime: null,
  lastRequestId: null
};

const $ = (selector, root = document) => root.querySelector(selector);
const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];
let drawerReturnFocus = null;

document.addEventListener("DOMContentLoaded", async () => {
  bindNavigation();
  bindActions();
  generateReviewIdempotencyKey();
  generatePlacementIdempotencyKey();
  generateWeddingPlannerKeys();
  route();
  await loadSession();
  syncOperatorUi();
  if (state.session.accessAllowed) {
    loadDashboard();
  } else {
    showAuthenticationGate();
  }
});

async function api(path, options = {}) {
  const base = state.apiBase.replace(/\/$/, "");
  const method = String(options.method || "GET").toUpperCase();
  const headers = { Accept: "application/json", ...(options.headers || {}) };
  if (!["GET", "HEAD", "OPTIONS", "TRACE"].includes(method) && state.session.csrfToken) {
    headers["X-CSRF-TOKEN"] = state.session.csrfToken;
  }
  const response = await fetch(`${base}${path}`, {
    ...options,
    headers,
    credentials: "same-origin"
  });
  const requestId = response.headers.get("X-Request-Id");
  if (requestId) state.lastRequestId = requestId;
  const body = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    const retryAfter = response.headers.get("Retry-After");
    const suffix = requestId ? ` [${requestId.slice(0, 8)}…]` : "";
    const retry = response.status === 429 && retryAfter ? ` Retry after ${retryAfter}s.` : "";
    const error = new Error((body?.error || `Request failed with HTTP ${response.status}`) + retry + suffix);
    error.status = response.status;
    throw error;
  }
  return body;
}

async function loadSession() {
  try {
    state.session = await api("/api/auth/session");
    if (state.session.authenticationEnabled) {
      state.apiBase = "";
      localStorage.removeItem("bliss-api-base");
    }
  } catch (error) {
    state.session = {
      authenticationEnabled: true,
      accessAllowed: false,
      isAuthenticated: false,
      displayName: null,
      roles: [],
      canWrite: false,
      canReview: false,
      csrfToken: null
    };
    setConnection("offline", error.message);
  }
}

async function loadDashboard() {
  setConnection("loading");
  $("#refresh-button").classList.add("spinning");
  try {
    const values = await Promise.all([
      api("/api/bliss/matches"), api("/api/creators"), api("/api/advertisers"),
      api("/api/advertiser-programs"), api("/api/advertiser-opportunities"),
      api("/api/content-items"), api("/api/campaigns"), api("/api/rule-versions"),
      api("/api/match-evaluation-runs"), api("/api/creator-ingestions"),
      api("/api/match-formation-runs"), api("/api/match-review-decisions"),
      api("/api/campaign-placement-runs"), api("/api/match-reviews/queue"),
      api("/api/campaign-bindings/queue"), api("/api/data-provenances"),
      api("/api/affiliate-networks"), api("/api/network-accesses"),
      api("/api/program-accesses"), api("/api/runtime/status").catch(() => null),
      api("/api/wedding-planner/workspaces").catch(() => [])
    ]);
    const [
      matches, creators, advertisers, programs, opportunities, content, campaigns,
      ruleVersions, runs, ingestions, formations, reviews, placements, reviewQueue,
      placementQueue, provenances, affiliateNetworks, networkAccesses, programAccesses,
      runtime, weddingPlannerWorkspaces
    ] = values;
    const [contentDetails, ruleDetails] = await Promise.all([
      Promise.all(content.map(item => api(`/api/content-items/${item.id}`).catch(() => ({ ...item, adInventorySlots: [] })))),
      Promise.all(ruleVersions.map(rule => api(`/api/rule-versions/${rule.id}`).catch(() => rule)))
    ]);
    Object.assign(state, {
      matches, creators, advertisers, programs, opportunities, content, contentDetails,
      campaigns, ruleVersions: ruleDetails, runs, ingestions, formations, reviews,
      placements, reviewQueue, placementQueue, provenances, affiliateNetworks,
      networkAccesses, programAccesses, runtime, weddingPlannerWorkspaces, loaded: true
    });
    state.selectedReview = reviewQueue.some(x => x.blissMatchId === state.selectedReview)
      ? state.selectedReview : reviewQueue[0]?.blissMatchId || null;
    state.selectedPlacement = placementQueue.some(x => x.blissMatchId === state.selectedPlacement)
      ? state.selectedPlacement : placementQueue[0]?.blissMatchId || null;
    renderAll();
    route();
    setConnection("online");
  } catch (error) {
    if (error.status === 401 && state.session.authenticationEnabled) {
      state.session.accessAllowed = false;
      state.session.isAuthenticated = false;
      syncOperatorUi();
      showAuthenticationGate();
      return;
    }
    try {
      state.runtime = await api("/api/runtime/status");
    } catch {
      /* Status remains empty when the runtime document is also unavailable. */
    }
    setConnection("offline", error.message);
    renderUnavailable(error.message);
    renderRuntimeStatus();
    toast(error.message, true);
  } finally {
    $("#refresh-button").classList.remove("spinning");
  }
}

function renderAll() {
  renderOverview();
  renderMatches();
  renderCreators();
  renderReviews();
  renderPlacements();
  renderPartners();
  renderInventory();
  renderAudit();
  renderRuntimeStatus();
  renderWeddingPlanner();
  $("#nav-match-count").textContent = state.matches.length;
  $("#nav-review-count").textContent = state.reviewQueue.length;
  $("#nav-placement-count").textContent = state.placementQueue.length;
  const weddingCount = $("#nav-wedding-planner-count");
  if (weddingCount) weddingCount.textContent = state.weddingPlannerWorkspaces.length;
  applyPermissions();
}

function renderOverview() {
  const unevaluated = state.matches.filter(x => x.status === "CREATED").length;
  const countries = new Set(state.creators.map(x => x.countryCode).filter(Boolean));
  $("#stat-matches").textContent = state.matches.length;
  $("#stat-review").textContent = state.reviewQueue.length;
  $("#stat-placement").textContent = state.placementQueue.length;
  $("#stat-creators").textContent = state.creators.length;
  $("#stat-unevaluated").textContent = `${unevaluated} awaiting evaluation`;
  $("#stat-countries").textContent = `Across ${countries.size || "—"} markets`;

  const statuses = [
    ["APPROVED", "Approved", "#49b982"], ["REVIEW_REQUIRED", "Review required", "#d9ad4e"],
    ["INELIGIBLE", "Ineligible", "#c95d57"], ["CREATED", "Not evaluated", "#87938e"]
  ];
  $("#status-chart").innerHTML = statuses.map(([status, label, color]) => {
    const count = state.matches.filter(x => x.status === status).length;
    const width = state.matches.length ? (count / state.matches.length) * 100 : 0;
    return `<div class="status-row"><label><span class="status-dot" style="background:${color}"></span>${label}</label><div class="status-bar"><span style="width:${width}%;background:${color}"></span></div><strong>${count}</strong></div>`;
  }).join("");

  const activities = [
    ...state.runs.map(x => ({ type: "evaluation", id: x.id, creatorId: x.creatorId, title: "Evaluation completed", detail: `${friendlyStatus(x.matchStatus)} · score ${formatScore(x.overallScore)}`, at: x.completedAt || x.startedAt, symbol: "◇" })),
    ...state.reviews.map(x => ({ type: "review", id: x.id, creatorId: x.creatorId, title: "Review recorded", detail: `${friendlyStatus(x.decision)} · ${x.reviewerLabel}`, at: x.completedAt, symbol: "☑" })),
    ...state.placements.map(x => ({ type: "placement", id: x.id, creatorId: x.creatorId, title: "Placement planned", detail: campaignById(x.campaignId)?.name || shortId(x.campaignId), at: x.completedAt, symbol: "▣" })),
    ...state.ingestions.map(x => ({ type: "ingestion", id: x.id, creatorId: x.creatorId, title: "Creator observation", detail: `${friendlyStatus(x.outcome)} · ${x.sourceSystem}`, at: x.completedAt, symbol: "↓" })),
    ...state.formations.map(x => ({ type: "formation", id: x.id, creatorId: x.creatorId, title: "Match formed", detail: opportunityById(x.advertiserOpportunityId)?.name || shortId(x.advertiserOpportunityId), at: x.completedAt, symbol: "＋" }))
  ].sort((a, b) => new Date(b.at) - new Date(a.at)).slice(0, 5);
  $("#recent-activity").innerHTML = activities.length ? activities.map(item => `
    <button class="activity-item" data-audit-type="${item.type}" data-audit-id="${item.id}">
      <span class="activity-icon">${item.symbol}</span>
      <span><strong>${escapeHtml(item.title)}</strong><small>${escapeHtml(creatorById(item.creatorId)?.name || "System record")} · ${escapeHtml(item.detail)}</small></span>
      <span class="activity-time">${relativeTime(item.at)}</span>
    </button>`).join("") : emptyState("No operational activity recorded yet.");

  $("#queue-summary").innerHTML = `
    <a class="queue-summary-card" href="#/matches"><span><strong>Evaluate certificates</strong><small>Created matches waiting for a deterministic run</small></span><span>${unevaluated}</span></a>
    <a class="queue-summary-card" href="#/review"><span><strong>Review decisions</strong><small>Human judgement required</small></span><span>${state.reviewQueue.length}</span></a>
    <a class="queue-summary-card" href="#/placement"><span><strong>Plan placements</strong><small>Approved matches ready for inventory</small></span><span>${state.placementQueue.length}</span></a>`;
}

function renderMatches() {
  const term = state.matchSearch.toLowerCase();
  const matches = state.matches
    .filter(match => {
      const creator = creatorById(match.creatorId);
      const opportunity = opportunityById(match.advertiserOpportunityId);
      return (state.matchFilter === "ALL" || match.status === state.matchFilter)
        && `${match.id} ${creator?.name || ""} ${opportunity?.name || ""}`.toLowerCase().includes(term);
    })
    .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
  $("#match-list").innerHTML = matches.length ? matches.map(match => {
    const creator = creatorById(match.creatorId);
    const opportunity = opportunityById(match.advertiserOpportunityId);
    const rule = ruleById(match.ruleVersionId);
    return `<article class="match-row">
      <div class="entity-name"><span class="mini-avatar">${getInitials(creator?.name || "?")}</span><span><strong>${escapeHtml(creator?.name || "Unknown creator")}</strong><small>${escapeHtml(creator?.countryCode || "Unknown market")} · ${escapeHtml(creator?.primaryLanguage || "Unknown language")}</small></span></div>
      <div class="match-cell opportunity-cell"><strong>${escapeHtml(opportunity?.name || "Unknown opportunity")}</strong><small>${escapeHtml(opportunity?.category || "Uncategorized")}</small></div>
      <span class="status-badge ${statusClass(match.status)}">${friendlyStatus(match.status)}</span>
      <div class="score-wrap"><span class="score-ring" style="--score:${(match.overallScore || 0) * 100}"><span>${formatScore(match.overallScore)}</span></span><div class="match-cell"><strong>Overall</strong><small>${formatPercent(match.confidenceScore)} confidence</small></div></div>
      <div class="row-actions">${rule?.documentJson ? `<button class="small-button primary" data-evaluate="${match.id}">Evaluate</button>` : ""}<button class="small-button" data-match-id="${match.id}">Open</button></div>
    </article>`;
  }).join("") : emptyState("No matches fit the selected filters.");
}

function renderCreators() {
  const term = state.creatorSearch.toLowerCase();
  const creators = state.creators.filter(x => `${x.name} ${x.countryCode || ""} ${x.primaryLanguage || ""}`.toLowerCase().includes(term));
  $("#creator-grid").innerHTML = creators.length ? creators.map(creator => {
    const matchCount = state.matches.filter(x => x.creatorId === creator.id).length;
    const contentCount = state.content.filter(x => x.creatorId === creator.id).length;
    return `<button class="entity-card" data-creator-id="${creator.id}">
      <div class="entity-card-head"><span class="mini-avatar">${getInitials(creator.name)}</span><span class="country-tag">${escapeHtml(creator.countryCode || "UNKNOWN")}</span></div>
      <h3>${escapeHtml(creator.name)}</h3><p>${escapeHtml(creator.primaryLanguage || "Language unknown")}</p>
      <div class="entity-metrics"><div><span>Audience</span><strong>${formatNumber(creator.audienceSize)}</strong></div><div><span>Matches</span><strong>${matchCount}</strong></div><div><span>Content</span><strong>${contentCount}</strong></div><div><span>Audience split</span><strong>${formatNullablePercent(creator.femalePercentage)} / ${formatNullablePercent(creator.malePercentage)}</strong></div></div>
    </button>`;
  }).join("") : emptyState("No creators found.");
}

function renderReviews() {
  $("#review-match").innerHTML = state.reviewQueue.length
    ? state.reviewQueue.map(x => `<option value="${x.blissMatchId}" ${x.blissMatchId === state.selectedReview ? "selected" : ""}>${escapeHtml(x.creatorName)} · ${escapeHtml(x.opportunityName)} · ${formatScore(x.overallScore)}</option>`).join("")
    : `<option value="">No certificates require review</option>`;
  $("#review-submit").disabled = !state.reviewQueue.length || !state.session.canReview;
  $("#review-queue-list").innerHTML = state.reviewQueue.length ? state.reviewQueue.map(item => `
    <article class="queue-card ${item.blissMatchId === state.selectedReview ? "selected" : ""}">
      <div class="queue-card-head"><div><h3>${escapeHtml(item.creatorName)}</h3><p>${escapeHtml(item.opportunityName)}</p></div><span class="status-badge status-review_required">Review required</span></div>
      <div class="queue-card-meta"><span>Score <strong>${formatScore(item.overallScore)}</strong></span><span>Confidence <strong>${formatPercent(item.confidenceScore)}</strong></span><span>Evaluations <strong>${item.evaluationRunCount}</strong></span></div>
      <div class="queue-card-actions"><button class="small-button" data-match-id="${item.blissMatchId}">Inspect evidence</button><button class="small-button primary" data-select-review="${item.blissMatchId}">Review this match</button></div>
    </article>`).join("") : emptyState("The review queue is clear.");

  const recent = [...state.reviews].sort((a,b) => new Date(b.completedAt)-new Date(a.completedAt)).slice(0,6);
  $("#review-list").innerHTML = recent.length ? recent.map(x => activityButton("☑", creatorById(x.creatorId)?.name || shortId(x.creatorId), `${x.reviewerLabel} · ${friendlyStatus(x.decision)}`, x.completedAt, `data-review-id="${x.id}"`)).join("") : emptyState("No human review decisions yet.");
}

function renderPlacements() {
  $("#placement-match").innerHTML = state.placementQueue.length
    ? state.placementQueue.map(x => `<option value="${x.blissMatchId}" ${x.blissMatchId === state.selectedPlacement ? "selected" : ""}>${escapeHtml(x.creatorName)} · ${escapeHtml(x.opportunityName)} · ${formatScore(x.overallScore)}</option>`).join("")
    : `<option value="">No approved unbound matches</option>`;
  $("#placement-submit").disabled = !state.placementQueue.length || !state.session.canWrite;
  $("#placement-queue-list").innerHTML = state.placementQueue.length ? state.placementQueue.map(item => `
    <article class="queue-card ${item.blissMatchId === state.selectedPlacement ? "selected" : ""}">
      <div class="queue-card-head"><div><h3>${escapeHtml(item.creatorName)}</h3><p>${escapeHtml(item.opportunityName)}</p></div><span class="status-badge status-approved">Approved</span></div>
      <div class="queue-card-meta"><span>Score <strong>${formatScore(item.overallScore)}</strong></span><span>Confidence <strong>${formatPercent(item.confidenceScore)}</strong></span><span>Content items <strong>${item.contentItemCount}</strong></span></div>
      <div class="queue-card-actions"><button class="small-button" data-match-id="${item.blissMatchId}">Inspect certificate</button><button class="small-button primary" data-select-placement="${item.blissMatchId}">Plan this match</button></div>
    </article>`).join("") : emptyState("There are no approved certificates awaiting placement.");
  updatePlacementOptions();

  const recent = [...state.placements].sort((a,b) => new Date(b.completedAt)-new Date(a.completedAt)).slice(0,6);
  $("#placement-list").innerHTML = recent.length ? recent.map(x => activityButton("▣", creatorById(x.creatorId)?.name || shortId(x.creatorId), `${campaignById(x.campaignId)?.name || shortId(x.campaignId)} · Planned`, x.completedAt, `data-placement-run-id="${x.id}"`)).join("") : emptyState("No campaign placements have been planned.");
}

function renderPartners() {
  $$("#partner-tabs .tab-button").forEach(button => {
    const active = button.dataset.partnerTab === state.partnerTab;
    button.classList.toggle("active", active);
    button.setAttribute("aria-selected", String(active));
  });
  if (state.partnerTab === "advertisers") {
    $("#partner-content").innerHTML = `<div class="entity-grid">${state.advertisers.length ? state.advertisers.map(advertiser => {
      const programIds = state.programs.filter(x => x.advertiserId === advertiser.id).map(x => x.id);
      const opportunityCount = state.opportunities.filter(x => programIds.includes(x.advertiserProgramId)).length;
      return `<button class="entity-card" data-advertiser-id="${advertiser.id}"><div class="entity-card-head"><span class="mini-avatar">${getInitials(advertiser.name)}</span><span class="country-tag">${escapeHtml(advertiser.countryCode || "GLOBAL")}</span></div><h3>${escapeHtml(advertiser.name)}</h3><p>${escapeHtml(hostname(advertiser.website) || "No public website")}</p><div class="entity-metrics"><div><span>Programs</span><strong>${programIds.length}</strong></div><div><span>Opportunities</span><strong>${opportunityCount}</strong></div></div></button>`;
    }).join("") : emptyState("No advertisers found.")}</div>`;
  } else if (state.partnerTab === "opportunities") {
    $("#partner-content").innerHTML = `<div class="opportunity-list">${state.opportunities.length ? state.opportunities.map(opportunity => {
      const program = programById(opportunity.advertiserProgramId);
      const advertiser = advertiserById(program?.advertiserId);
      return `<article class="opportunity-row"><div><strong>${escapeHtml(opportunity.name)}</strong><small>${escapeHtml(opportunity.productName || "Product not specified")}</small></div><div><strong>${escapeHtml(advertiser?.name || "Unknown advertiser")}</strong><small>${escapeHtml(program?.name || "Unknown program")}</small></div><div><strong>${escapeHtml(opportunity.category || "Uncategorized")}</strong><small>Category</small></div><span class="status-badge ${statusClass(opportunity.status)}">${friendlyStatus(opportunity.status)}</span></article>`;
    }).join("") : emptyState("No opportunities found.")}</div>`;
  } else {
    $("#partner-content").innerHTML = `<div class="access-grid">
      <section class="panel"><div class="panel-header"><div><p class="eyebrow">AFFILIATE NETWORKS</p><h3>Advertiser access</h3></div></div><div class="access-list">${state.networkAccesses.length ? state.networkAccesses.map(access => { const network = state.affiliateNetworks.find(x=>x.id===access.affiliateNetworkId); const advertiser=advertiserById(access.advertiserId); return `<div class="access-row"><span><strong>${escapeHtml(advertiser?.name || "Unknown advertiser")}</strong><small>${escapeHtml(network?.name || "Unknown network")}</small></span><span class="status-badge ${statusClass(access.status)}">${friendlyStatus(access.status)}</span></div>`; }).join("") : emptyState("No network access records.")}</div></section>
      <section class="panel"><div class="panel-header"><div><p class="eyebrow">PROGRAM ACCESS</p><h3>Controlled approvals</h3></div></div><div class="access-list">${state.programAccesses.length ? state.programAccesses.map(access => { const program=programById(access.advertiserProgramId); return `<div class="access-row"><span><strong>${escapeHtml(program?.name || "Unknown program")}</strong><small>${access.approvedAt ? `Approved ${formatDate(access.approvedAt)}` : "Approval date unavailable"}</small></span><span class="status-badge ${statusClass(access.status)}">${friendlyStatus(access.status)}</span></div>`; }).join("") : emptyState("No program access records.")}</div></section>
    </div>`;
  }
}

function renderInventory() {
  $$("#inventory-tabs .tab-button").forEach(button => {
    const active = button.dataset.inventoryTab === state.inventoryTab;
    button.classList.toggle("active", active);
    button.setAttribute("aria-selected", String(active));
  });
  if (state.inventoryTab === "content") {
    $("#inventory-content").innerHTML = `<div class="inventory-list">${state.contentDetails.length ? state.contentDetails.map(item => `
      <button class="inventory-card" data-content-id="${item.id}"><span class="inventory-art">${contentSymbol(item.contentType)}</span><span class="inventory-body"><span class="country-tag">${escapeHtml(item.contentType)}</span><h3>${escapeHtml(item.title)}</h3><p>${escapeHtml(creatorById(item.creatorId)?.name || "Unknown creator")} · ${item.adInventorySlots?.length || 0} slots</p><span class="slot-row">${(item.adInventorySlots || []).map(slot => `<span class="slot-tag">${friendlyStatus(slot.slotType)} · ${slot.isAvailable ? "Available" : "Unavailable"}</span>`).join("") || `<span class="slot-tag">NO SLOTS</span>`}</span></span></button>`).join("") : emptyState("No content inventory found.")}</div>`;
  } else {
    $("#inventory-content").innerHTML = `<div class="campaign-grid">${state.campaigns.length ? state.campaigns.map(campaign => {
      const opportunity = opportunityById(campaign.advertiserOpportunityId);
      return `<article class="campaign-card"><span class="status-badge ${statusClass(campaign.status)}">${friendlyStatus(campaign.status)}</span><h3>${escapeHtml(campaign.name)}</h3><p>${escapeHtml(opportunity?.name || "No opportunity bound")} · created ${formatDate(campaign.createdAt)}</p><button class="small-button" data-campaign-id="${campaign.id}">View placements</button></article>`;
    }).join("") : emptyState("No campaigns found.")}</div>`;
  }
}

function renderAudit() {
  $$("#audit-tabs .tab-button").forEach(button => {
    const active = button.dataset.auditTab === state.auditTab;
    button.classList.toggle("active", active);
    button.setAttribute("aria-selected", String(active));
  });
  const term = state.auditSearch.toLowerCase();
  let headers = [], rows = [];
  if (state.auditTab === "evaluations") {
    headers = ["Run","Creator","Decision","Score","Algorithm","Completed",""];
    rows = state.runs.filter(x => auditHaystack(x).includes(term)).sort(byCompleted).map(x => [code(shortId(x.id)), escapeHtml(creatorById(x.creatorId)?.name || shortId(x.creatorId)), badge(x.matchStatus), `<strong>${formatScore(x.overallScore)}</strong>`, code(x.algorithmVersion), formatDate(x.completedAt || x.startedAt), inspectButton("data-run-id",x.id)]);
  } else if (state.auditTab === "formations") {
    headers = ["Run","Creator","Opportunity","Evaluation","Outcome","Completed",""];
    rows = state.formations.filter(x => auditHaystack(x).includes(term)).sort(byCompleted).map(x => [code(shortId(x.id)), escapeHtml(creatorById(x.creatorId)?.name || shortId(x.creatorId)), escapeHtml(opportunityById(x.advertiserOpportunityId)?.name || shortId(x.advertiserOpportunityId)), x.evaluateOnCreate ? "Immediate" : "Deferred", badge(x.outcome), formatDate(x.completedAt), inspectButton("data-formation-run-id",x.id)]);
  } else if (state.auditTab === "reviews") {
    headers = ["Decision","Creator","Reviewer","Result","Source","Completed",""];
    rows = state.reviews.filter(x => auditHaystack(x).includes(term)).sort(byCompleted).map(x => [badge(x.decision), escapeHtml(creatorById(x.creatorId)?.name || shortId(x.creatorId)), escapeHtml(x.reviewerLabel), badge(x.resultingMatchStatus), code(x.sourceSystem), formatDate(x.completedAt), inspectButton("data-review-id",x.id)]);
  } else if (state.auditTab === "placements") {
    headers = ["Run","Creator","Campaign","Operator","Outcome","Completed",""];
    rows = state.placements.filter(x => auditHaystack(x).includes(term)).sort(byCompleted).map(x => [code(shortId(x.id)), escapeHtml(creatorById(x.creatorId)?.name || shortId(x.creatorId)), escapeHtml(campaignById(x.campaignId)?.name || shortId(x.campaignId)), escapeHtml(x.operatorLabel), badge(x.outcome), formatDate(x.completedAt), inspectButton("data-placement-run-id",x.id)]);
  } else if (state.auditTab === "ingestions") {
    headers = ["Run","Creator","Identity","Source","Outcome","Completed",""];
    rows = state.ingestions.filter(x => auditHaystack(x).includes(term)).sort(byCompleted).map(x => [code(shortId(x.id)), escapeHtml(creatorById(x.creatorId)?.name || shortId(x.creatorId)), code(x.identityKey), escapeHtml(x.sourceSystem), badge(x.outcome), formatDate(x.completedAt), inspectButton("data-ingest-run-id",x.id)]);
  } else {
    headers = ["Entity","Field","Source","Confidence","Collected","Notes"];
    rows = state.provenances.filter(x => auditHaystack(x).includes(term)).sort((a,b)=>new Date(b.collectedAt)-new Date(a.collectedAt)).map(x => [`${escapeHtml(friendlyStatus(x.entityType))}<br>${code(shortId(x.entityId))}`, escapeHtml(friendlyStatus(x.fieldName)), escapeHtml(x.sourceName || x.sourceType), badge(x.confidenceLevel), formatDate(x.collectedAt), escapeHtml(x.notes || "—")]);
  }
  $("#audit-content").innerHTML = rows.length ? `<table><thead><tr>${headers.map(x=>`<th>${x}</th>`).join("")}</tr></thead><tbody>${rows.map(row=>`<tr>${row.map(cell=>`<td>${cell}</td>`).join("")}</tr>`).join("")}</tbody></table>` : emptyState(`No ${state.auditTab} records found.`);
}

function healthClass(status) {
  const value = String(status || "").toLowerCase();
  if (value === "healthy") return "health-healthy";
  if (value === "degraded") return "health-degraded";
  return "health-unhealthy";
}

function renderRuntimeStatus() {
  const runtime = state.runtime;
  if (!runtime) {
    $("#runtime-health-cards").innerHTML = emptyState("Runtime status is unavailable.");
    $("#runtime-correlation").innerHTML = emptyState("No request identifier yet.");
    $("#runtime-limits").innerHTML = emptyState("Throttle policy is unavailable.");
    $("#runtime-last-verification").innerHTML = emptyState("No verification receipt yet.");
    $("#runtime-recent-verifications").innerHTML = emptyState("No verification receipts yet.");
    $("#runtime-events").innerHTML = emptyState("No operational events recorded.");
    return;
  }
  $("#runtime-health-cards").innerHTML = [
    ["Process", runtime.processStatus, "Liveness"],
    ["Database", runtime.databaseStatus, runtime.databaseDescription || "Readiness"],
    ["Authentication", runtime.authenticationEnabled ? "Enabled" : "Development open", runtime.environment],
    ["Key ring", runtime.persistentKeysConfigured ? "Configured" : "Ephemeral", "Data Protection"]
  ].map(([label, status, detail]) => `<article class="stat-card"><span class="stat-top"><span class="health-pill ${healthClass(status)}">${escapeHtml(friendlyStatus(status))}</span><span class="trend">${escapeHtml(label.toUpperCase())}</span></span><strong>${escapeHtml(friendlyStatus(status))}</strong><span>${escapeHtml(label)}</span><small>${escapeHtml(detail)}</small></article>`).join("");
  $("#runtime-correlation").innerHTML = `<p><strong>Last request</strong><span class="request-id">${escapeHtml(runtime.lastRequestId || state.lastRequestId || "None yet")}</span></p><p>Every API response includes <code>X-Request-Id</code>. Incoming identifiers are accepted only when they are valid GUIDs.</p>`;
  $("#runtime-limits").innerHTML = `<p><strong>${runtime.writeRateLimitPermitLimit} writes / ${runtime.rateLimitWindowSeconds}s</strong>Controlled POST endpoints share this quota.</p><p><strong>${runtime.authenticationRateLimitPermitLimit} login starts / ${runtime.rateLimitWindowSeconds}s</strong>OIDC challenge initiation is separately limited.</p>`;
  const last = runtime.lastVerification;
  $("#runtime-last-verification").innerHTML = last
    ? `<p><strong>${escapeHtml(last.matched ? "Matched" : "Mismatch")}</strong><span class="request-id">${escapeHtml(last.packKind || "pack")}</span></p><p><code>sha256:${escapeHtml((last.computedSha256 || "").slice(0, 12))}…</code> ${last.requestId ? `· ${escapeHtml(last.requestId.slice(0, 8))}…` : ""}</p>`
    : emptyState("No verification receipt in this process yet.");
  const history = runtime.recentVerifications || [];
  $("#runtime-recent-verifications").innerHTML = history.length ? `<table><thead><tr><th>When</th><th>Outcome</th><th>Pack</th><th>Digest</th><th>Correlation</th></tr></thead><tbody>${history.map(item => `<tr><td>${formatDate(item.verifiedAt)}</td><td>${badge(item.matched ? "MATCHED" : "MISMATCH")}</td><td>${escapeHtml(item.packKind || "pack")}</td><td><code>sha256:${escapeHtml((item.computedSha256 || "").slice(0, 12))}…</code></td><td><code>${escapeHtml(item.requestId || "—")}</code></td></tr>`).join("")}</tbody></table>` : emptyState("No verification receipts have been recorded in this process.");
  const events = runtime.recentEvents || [];
  $("#runtime-events").innerHTML = events.length ? `<table><thead><tr><th>When</th><th>Kind</th><th>Request</th><th>Status</th><th>Correlation</th><th>Detail</th></tr></thead><tbody>${events.map(event => `<tr><td>${formatDate(event.occurredAt)}</td><td>${badge(event.kind)}</td><td><code>${escapeHtml(event.method)} ${escapeHtml(event.path)}</code></td><td>${event.statusCode}</td><td><code>${escapeHtml(event.requestId)}</code></td><td>${event.detail ? `<code>${escapeHtml(event.detail)}</code>` : "—"}</td></tr>`).join("")}</tbody></table>` : emptyState("No security or throttle events have been recorded in this process.");
}

async function refreshRuntimeStatus() {
  try {
    state.runtime = await api("/api/runtime/status");
    renderRuntimeStatus();
  } catch (error) {
    toast(error.message, true);
  }
}

async function verifyWriteThrottle() {
  if (!state.session.canWrite) {
    toast("Your account does not have operator permission.", true);
    return;
  }
  const button = $("#verify-write-throttle");
  button.disabled = true;
  let limited = false;
  try {
    const attempts = (state.runtime?.writeRateLimitPermitLimit || 2) + 1;
    for (let index = 0; index < attempts; index += 1) {
      try {
        await api("/api/runtime/throttle-check", { method: "POST" });
      } catch (error) {
        toast(error.message, error.status === 429);
        if (error.status === 429) {
          limited = true;
          break;
        }
      }
    }
    await refreshRuntimeStatus();
    if (limited) toast("Write quota recorded in the operational event log.");
  } finally {
    button.disabled = false;
  }
}

async function downloadAuditPack(path, successMessage, button) {
  if (button) button.disabled = true;
  try {
    const base = state.apiBase.replace(/\/$/, "");
    const response = await fetch(`${base}${path}`, {
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    const requestId = response.headers.get("X-Request-Id");
    if (requestId) state.lastRequestId = requestId;
    if (!response.ok) {
      const body = await response.json().catch(() => null);
      const suffix = requestId ? ` [${requestId.slice(0, 8)}…]` : "";
      throw new Error((body?.error || `Request failed with HTTP ${response.status}`) + suffix);
    }
    const blob = await response.blob();
    const header = /filename\*?=(?:UTF-8'')?"?([^\";]+)"?/i.exec(response.headers.get("Content-Disposition") || "");
    const fileName = header ? decodeURIComponent(header[1]) : "bliss-export.json";
    const digest = (response.headers.get("X-Content-SHA256") || "").toLowerCase();
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
    const requestSuffix = requestId ? ` [${requestId.slice(0, 8)}…]` : "";
    const digestSuffix = digest ? ` sha256:${digest.slice(0, 12)}…` : "";
    toast(`${successMessage}${requestSuffix}${digestSuffix}.`);
    refreshRuntimeStatus();
  } catch (error) {
    toast(error.message, true);
  } finally {
    if (button) button.disabled = false;
  }
}

async function exportAuditLedger() {
  const ledger = state.auditTab === "provenance" ? "provenance" : state.auditTab;
  await downloadAuditPack(`/api/audit/export/${encodeURIComponent(ledger)}`, `Exported ${ledger} ledger`, $("#export-audit-ledger"));
}

async function exportMatchCase(id, button) {
  await downloadAuditPack(`/api/audit/export/matches/${id}`, "Exported match case file", button);
}

async function exportCreatorCase(id, button) {
  await downloadAuditPack(`/api/audit/export/creators/${id}`, "Exported creator case file", button);
}

async function exportCampaignCase(id, button) {
  await downloadAuditPack(`/api/audit/export/campaigns/${id}`, "Exported campaign case file", button);
}

function chooseAuditPackToVerify() {
  $("#verify-audit-file").value = "";
  $("#verify-audit-file").click();
}

async function verifySelectedAuditPack(event) {
  const input = event.currentTarget;
  const file = input.files && input.files[0];
  input.value = "";
  if (!file) return;
  const button = $("#verify-audit-pack");
  button.disabled = true;
  const resultEl = $("#verify-audit-result");
  try {
    if (file.size > 512000) throw new Error("Pack exceeds the 512 KB verification limit.");
    const text = await file.text();
    try {
      JSON.parse(text);
    } catch {
      throw new Error("Pack must be valid JSON.");
    }
    const result = await api("/api/audit/verify", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: text
    });
    const prefix = (result.computedSha256 || "").slice(0, 12);
    const kind = result.packKind || "pack";
    if (result.matched) {
      resultEl.hidden = false;
      resultEl.className = "verify-result ok";
      resultEl.textContent = `Verified ${kind} sha256:${prefix}…`;
      toast(`Verified ${kind} sha256:${prefix}…`);
    } else {
      resultEl.hidden = false;
      resultEl.className = "verify-result bad";
      resultEl.textContent = `Digest mismatch for ${kind} sha256:${prefix}…`;
      toast(`Digest mismatch for ${kind} sha256:${prefix}…`, true);
    }
    downloadVerificationReceipt(result);
    refreshRuntimeStatus();
  } catch (error) {
    resultEl.hidden = false;
    resultEl.className = "verify-result bad";
    resultEl.textContent = error.message;
    toast(error.message, true);
  } finally {
    button.disabled = false;
  }
}

function downloadVerificationReceipt(result) {
  const verifiedAt = result.verifiedAt || new Date().toISOString();
  const stamp = String(verifiedAt).replace(/[-:]/g, "").replace(/\.\d+Z$/, "Z").replace("T", "");
  const blob = new Blob([JSON.stringify(result, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `bliss-verify-${stamp || "receipt"}.json`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

function showWorkflow(type, prefill = {}) {
  if (!state.session.canWrite) {
    toast("Your account does not have operator permission.", true);
    return;
  }

  const dialog = $("#workflow-dialog");
  const body = $("#workflow-body");
  $("#workflow-form").dataset.workflow = type;
  if (type === "ingest") {
    $("#workflow-eyebrow").textContent = "CONTROLLED OBSERVATION";
    $("#workflow-title").textContent = "Ingest creator profile";
    body.innerHTML = `<div class="safety-note"><strong>Provider-neutral input.</strong> No provider is called and unknown metrics remain null.</div>
      <div class="form-grid">
        <label><span>Platform *</span><select name="platform" required><option value="YOUTUBE">YouTube</option><option value="PODCAST">Podcast</option><option value="TIKTOK">TikTok</option><option value="INSTAGRAM">Instagram</option><option value="TWITCH">Twitch</option><option value="OTHER">Other</option></select></label>
        <label><span>External profile ID *</span><input name="externalProfileId" placeholder="Provider-owned identifier" maxlength="256" required></label>
        <label class="span-two"><span>Display name *</span><input name="creatorName" placeholder="Creator name" maxlength="256" required></label>
        <label><span>Country code</span><input name="countryCode" placeholder="e.g. ES" maxlength="8"></label><label><span>Primary language</span><input name="primaryLanguage" placeholder="e.g. Spanish" maxlength="128"></label>
        <label><span>Audience size</span><input name="audienceSize" type="number" min="0" placeholder="Unknown"></label><label><span>Platform followers</span><input name="followers" type="number" min="0" placeholder="Unknown"></label>
        <label><span>Female audience %</span><input name="femalePercentage" type="number" min="0" max="100" step=".01" placeholder="Unknown"></label><label><span>Male audience %</span><input name="malePercentage" type="number" min="0" max="100" step=".01" placeholder="Unknown"></label>
        <label><span>Primary age range</span><input name="primaryAgeRange" placeholder="e.g. 25–34" maxlength="64"></label><label><span>Primary geography</span><input name="primaryGeography" placeholder="e.g. Western Europe" maxlength="128"></label>
        <label><span>Engagement level</span><input name="engagementLevel" placeholder="e.g. HIGH" maxlength="64"></label><label><span>Confidence</span><select name="confidenceLevel"><option>UNKNOWN</option><option>LOW</option><option>MEDIUM</option><option>HIGH</option></select></label>
        <label class="span-two"><span>Profile URL</span><input name="profileUrl" type="url" placeholder="https://creator-profile.example"></label><label class="span-two"><span>Observation source URL</span><input name="sourceUrl" type="url" placeholder="https://source.example"></label>
        <details class="advanced-fields span-two"><summary>Audit metadata</summary><div class="form-grid compact"><label><span>Source system *</span><input name="sourceSystem" value="OPERATOR_CONSOLE" maxlength="64" required></label><label><span>Idempotency key *</span><div class="input-action"><input name="idempotencyKey" value="manual-${crypto.randomUUID()}" readonly required><button type="button" data-regenerate-inline aria-label="Generate a new key">↻</button></div></label></div></details>
      </div><div class="form-actions"><small>Canonical platform identity prevents duplicates.</small><button class="primary-button" type="submit">Ingest profile <span>→</span></button></div>`;
  } else {
    $("#workflow-eyebrow").textContent = "MATCH CERTIFICATE";
    $("#workflow-title").textContent = "Form a controlled match";
    body.innerHTML = `<div class="safety-note"><strong>Explicit records only.</strong> This does not discover candidates or use AI as scoring authority.</div>
      <div class="form-grid">
        <label class="span-two"><span>Creator *</span><select name="creatorId" required>${state.creators.map(x=>`<option value="${x.id}" ${x.id===prefill.creatorId?"selected":""}>${escapeHtml(x.name)} · ${escapeHtml(x.countryCode || "Unknown market")}</option>`).join("")}</select></label>
        <label class="span-two"><span>Active opportunity *</span><select name="advertiserOpportunityId" required>${state.opportunities.filter(x=>x.status==="ACTIVE").map(x=>`<option value="${x.id}" ${x.id===prefill.opportunityId?"selected":""}>${escapeHtml(x.name)} · ${escapeHtml(x.category || "Uncategorized")}</option>`).join("")}</select></label>
        <label class="span-two"><span>Rule version *</span><select name="ruleVersionId" required>${state.ruleVersions.filter(x=>x.isActive).map(x=>`<option value="${x.id}" ${x.documentJson?"":"disabled"}>${escapeHtml(x.version)} · ${escapeHtml(x.name)}${x.documentJson?"":" (document unavailable)"}</option>`).join("")}</select></label>
        <label class="span-two checkbox-label"><input name="evaluateOnCreate" type="checkbox" checked><span>Evaluate immediately with deterministic rules</span></label>
        <details class="advanced-fields span-two"><summary>Audit metadata</summary><div class="form-grid compact"><label><span>Source system *</span><input name="sourceSystem" value="OPERATOR_CONSOLE" maxlength="64" required></label><label><span>Idempotency key *</span><div class="input-action"><input name="idempotencyKey" value="manual-${crypto.randomUUID()}" readonly required><button type="button" data-regenerate-inline aria-label="Generate a new key">↻</button></div></label></div></details>
      </div><div class="form-actions"><small>Submitting the same key safely replays the original result.</small><button class="primary-button" type="submit">Form match <span>→</span></button></div>`;
  }
  dialog.showModal();
  setTimeout(() => $("input:not([readonly]), select", body)?.focus(), 0);
}

async function submitWorkflow(form) {
  const type = form.dataset.workflow;
  const submit = $("button[type=submit]", form);
  const data = new FormData(form);
  const value = name => String(data.get(name) || "").trim();
  const nullableNumber = name => value(name) === "" ? null : Number(value(name));
  submit.disabled = true;
  const original = submit.innerHTML;
  submit.textContent = type === "ingest" ? "Ingesting…" : "Forming…";
  try {
    if (type === "ingest") {
      const result = await api("/api/creator-ingestions", { method:"POST", headers:{"Content-Type":"application/json"}, body:JSON.stringify({
        sourceSystem:value("sourceSystem"), idempotencyKey:value("idempotencyKey"), platform:value("platform"),
        externalProfileId:value("externalProfileId"), creatorName:value("creatorName"), profileUrl:value("profileUrl")||null,
        countryCode:value("countryCode")||null, primaryLanguage:value("primaryLanguage")||null,
        audienceSize:nullableNumber("audienceSize"), followers:nullableNumber("followers"),
        femalePercentage:nullableNumber("femalePercentage"), malePercentage:nullableNumber("malePercentage"),
        primaryAgeRange:value("primaryAgeRange")||null, primaryGeography:value("primaryGeography")||null,
        engagementLevel:value("engagementLevel")||null, sourceUrl:value("sourceUrl")||null,
        confidenceLevel:value("confidenceLevel")||"UNKNOWN", collectedAt:new Date().toISOString()
      })});
      $("#workflow-dialog").close();
      toast(`${friendlyStatus(result.outcome)} · ${result.identityKey}${result.isReplay ? " (replay)" : ""}`);
      await loadDashboard();
      location.hash = `#/creators/${result.creatorId}`;
    } else {
      const result = await api("/api/bliss/matches", { method:"POST", headers:{"Content-Type":"application/json"}, body:JSON.stringify({
        sourceSystem:value("sourceSystem"), idempotencyKey:value("idempotencyKey"), creatorId:value("creatorId"),
        advertiserOpportunityId:value("advertiserOpportunityId"), ruleVersionId:value("ruleVersionId"),
        evaluateOnCreate:data.get("evaluateOnCreate")==="on"
      })});
      $("#workflow-dialog").close();
      toast(`${friendlyStatus(result.matchStatus)} · match ${result.isReplay ? "replayed" : "formed"}`);
      await loadDashboard();
      location.hash = `#/matches/${result.blissMatchId}`;
    }
  } catch (error) { toast(error.message,true); }
  finally { submit.disabled=false;submit.innerHTML=original; }
}

async function submitReview(form) {
  if (!state.session.canReview) {
    toast("Your account does not have reviewer permission.", true);
    return;
  }

  const submit = $("#review-submit");
  const data = new FormData(form);
  const value = name => String(data.get(name)||"").trim();
  submit.disabled=true;submit.textContent="Recording…";
  try {
    const result=await api("/api/match-review-decisions",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({
      sourceSystem:value("sourceSystem"),idempotencyKey:value("idempotencyKey"),blissMatchId:value("blissMatchId"),
      reviewerLabel:value("reviewerLabel"),decision:value("decision"),rationale:value("rationale")
    })});
    toast(`${friendlyStatus(result.decision)} · ${friendlyStatus(result.resultingMatchStatus)}${result.isReplay?" (replay)":""}`);
    form.elements.rationale.value="";generateReviewIdempotencyKey();await loadDashboard();
    location.hash=`#/matches/${result.blissMatchId}`;
  } catch(error){toast(error.message,true);}
  finally{submit.disabled=false;submit.innerHTML=`Record decision <span>→</span>`;}
}

function updatePlacementOptions() {
  const item=state.placementQueue.find(x=>x.blissMatchId===$("#placement-match").value)||state.placementQueue.find(x=>x.blissMatchId===state.selectedPlacement)||state.placementQueue[0];
  if (!item) {
    $("#placement-campaign").innerHTML=`<option value="">No compatible campaigns</option>`;
    $("#placement-content").innerHTML=`<option value="">No creator content</option>`;
    $("#placement-slot").innerHTML=`<option value="">No inventory slots</option>`; return;
  }
  const campaigns=state.campaigns.filter(x=>x.status==="DRAFT"&&(!x.advertiserOpportunityId||x.advertiserOpportunityId===item.advertiserOpportunityId));
  $("#placement-campaign").innerHTML=campaigns.length?campaigns.map(x=>`<option value="${x.id}">${escapeHtml(x.name)}</option>`).join(""):`<option value="">No compatible draft campaigns</option>`;
  const content=state.contentDetails.filter(x=>x.creatorId===item.creatorId);
  $("#placement-content").innerHTML=content.length?content.map(x=>`<option value="${x.id}">${escapeHtml(x.title)} · ${escapeHtml(x.contentType)}</option>`).join(""):`<option value="">No creator-owned content</option>`;
  updatePlacementSlots();
}
function updatePlacementSlots() {
  const content=state.contentDetails.find(x=>x.id===$("#placement-content").value);
  const slots=content?.adInventorySlots||[];
  $("#placement-slot").innerHTML=slots.length?slots.map(x=>`<option value="${x.id}" ${x.isAvailable?"":"disabled"}>${friendlyStatus(x.slotType)} · ${slotTiming(x)} · ${x.isAvailable?"Available":"Unavailable"}</option>`).join(""):`<option value="">No inventory slots</option>`;
}
async function submitPlacement(form) {
  if (!state.session.canWrite) {
    toast("Your account does not have operator permission.", true);
    return;
  }

  const submit=$("#placement-submit"),data=new FormData(form),value=name=>String(data.get(name)||"").trim();
  submit.disabled=true;submit.textContent="Planning…";
  try {
    const result=await api("/api/campaign-placements",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({
      sourceSystem:value("sourceSystem"),idempotencyKey:value("idempotencyKey"),operatorLabel:value("operatorLabel"),
      blissMatchId:value("blissMatchId"),campaignId:value("campaignId"),contentItemId:value("contentItemId"),adInventorySlotId:value("adInventorySlotId")
    })});
    toast(`Placement planned${result.isReplay?" (replay)":""}`);generatePlacementIdempotencyKey();await loadDashboard();openPlacementRun(result.runId);
  } catch(error){toast(error.message,true);}
  finally{submit.disabled=false;submit.innerHTML=`Plan placement <span>→</span>`;}
}

async function openCreator(id) {
  openDrawer("CREATOR PROFILE","Loading creator…",skeleton());
  try {
    const creator=await api(`/api/creators/${id}`);
    const provenanceIds=new Set([creator.id,...creator.platforms.map(x=>x.id)]);
    const provenance=state.provenances.filter(x=>provenanceIds.has(x.entityId));
    $("#drawer-title").textContent=creator.name;
    $("#drawer-body").innerHTML=`<div class="detail-hero"><div class="detail-hero-top"><span class="status-badge status-active">Canonical profile</span><span class="detail-score">${getInitials(creator.name)}</span></div><h3>${escapeHtml(creator.name)}</h3><p>${escapeHtml(creator.countryCode||"Unknown market")} · ${escapeHtml(creator.primaryLanguage||"Language unknown")} · updated ${formatDate(creator.updatedAt)}</p><div class="detail-actions"><button class="small-button" data-export-creator="${creator.id}">Export case file</button><button class="small-button" data-verify-pack>Verify pack</button><button class="small-button" data-form-match-creator="${creator.id}">Form match</button></div></div>
      <section class="detail-section"><h4>Audience profile</h4><div class="detail-grid">${metric("Audience",formatNumber(creator.audienceSize))}${metric("Female",formatNullablePercent(creator.femalePercentage))}${metric("Male",formatNullablePercent(creator.malePercentage))}${metric("Age range",creator.primaryAgeRange||"Unknown")}${metric("Geography",creator.primaryGeography||"Unknown")}${metric("Engagement",creator.engagementLevel||"Unknown")}</div></section>
      <section class="detail-section"><h4>Platform identities · ${creator.platforms.length}</h4><div class="check-list">${creator.platforms.length?creator.platforms.map(x=>`<div class="check-item"><div><strong>${escapeHtml(x.platform)} · ${escapeHtml(x.externalProfileId||"No external ID")}</strong><small>${escapeHtml(x.profileUrl||"No profile URL")} · ${formatNumber(x.followers)} followers</small></div><span class="status-badge status-active">Linked</span></div>`).join(""):emptyState("No platform identities.")}</div></section>
      <section class="detail-section"><h4>Content · ${creator.contentItems.length}</h4><div class="check-list">${creator.contentItems.length?creator.contentItems.map(x=>`<button class="history-item" data-content-id="${x.id}"><span><strong>${escapeHtml(x.title)}</strong><small>${escapeHtml(x.contentType)} · ${x.adInventorySlots.length} slots</small></span><span>→</span></button>`).join(""):emptyState("No creator content.")}</div></section>
      <section class="detail-section"><h4>Match certificates · ${creator.blissMatches.length}</h4>${creator.blissMatches.length?creator.blissMatches.map(x=>`<button class="history-item" data-match-id="${x.id}"><span><strong>${escapeHtml(opportunityById(x.advertiserOpportunityId)?.name||shortId(x.advertiserOpportunityId))}</strong><small>${formatDate(x.createdAt)}</small></span>${badge(x.status)}</button>`).join(""):emptyState("No certificates for this creator.")}</section>
      <section class="detail-section"><h4>Field provenance · ${provenance.length}</h4>${provenance.length?provenance.map(x=>`<div class="history-item"><span><strong>${escapeHtml(friendlyStatus(x.fieldName))}</strong><small>${escapeHtml(x.sourceName||x.sourceType)} · ${formatDate(x.collectedAt)}</small></span>${badge(x.confidenceLevel)}</div>`).join(""):emptyState("No field provenance recorded.")}</section>`;
  } catch(error){drawerError(error);}
}

async function openAdvertiser(id) {
  openDrawer("ADVERTISER","Loading advertiser…",skeleton());
  try {
    const advertiser=await api(`/api/advertisers/${id}`);
    const programIds=advertiser.programs.map(x=>x.id);
    const opportunities=state.opportunities.filter(x=>programIds.includes(x.advertiserProgramId));
    const accesses=state.networkAccesses.filter(x=>x.advertiserId===id);
    $("#drawer-title").textContent=advertiser.name;
    $("#drawer-body").innerHTML=`<div class="detail-hero"><div class="detail-hero-top"><span class="status-badge status-active">Partner</span><span class="detail-score">${getInitials(advertiser.name)}</span></div><h3>${escapeHtml(advertiser.name)}</h3><p>${escapeHtml(advertiser.countryCode||"Global")} · ${escapeHtml(hostname(advertiser.website)||"No website")}</p></div>
      <section class="detail-section"><h4>About</h4><div class="check-item"><div><strong>Partner profile</strong><small>${escapeHtml(advertiser.description||"No description available.")}</small></div></div></section>
      <section class="detail-section"><h4>Programs · ${advertiser.programs.length}</h4><div class="check-list">${advertiser.programs.map(x=>`<div class="check-item"><div><strong>${escapeHtml(x.name)}</strong><small>${escapeHtml(x.externalProgramId||"No external ID")} · ${state.opportunities.filter(o=>o.advertiserProgramId===x.id).length} opportunities</small></div>${badge(x.status)}</div>`).join("")||emptyState("No programs.")}</div></section>
      <section class="detail-section"><h4>Opportunities · ${opportunities.length}</h4>${opportunities.map(x=>`<div class="history-item"><span><strong>${escapeHtml(x.name)}</strong><small>${escapeHtml(x.productName||"Product not specified")} · ${escapeHtml(x.category||"Uncategorized")}</small></span>${badge(x.status)}</div>`).join("")||emptyState("No opportunities.")}</section>
      <section class="detail-section"><h4>Network access · ${accesses.length}</h4>${accesses.map(x=>`<div class="history-item"><span><strong>${escapeHtml(state.affiliateNetworks.find(n=>n.id===x.affiliateNetworkId)?.name||"Unknown network")}</strong><small>${escapeHtml(x.externalAccountId||"No external account ID")}</small></span>${badge(x.status)}</div>`).join("")||emptyState("No affiliate network access.")}</section>`;
  } catch(error){drawerError(error);}
}

function openContent(id) {
  const item=state.contentDetails.find(x=>x.id===id);
  if(!item)return;
  openDrawer("CONTENT INVENTORY",item.title,`<div class="detail-hero"><div class="detail-hero-top"><span class="status-badge status-active">${escapeHtml(friendlyStatus(item.contentType))}</span><span class="detail-score">${contentSymbol(item.contentType)}</span></div><h3>${escapeHtml(item.title)}</h3><p>${escapeHtml(creatorById(item.creatorId)?.name||"Unknown creator")} · ${item.publishedAt?`published ${formatDate(item.publishedAt)}`:"Publication date unknown"}</p></div>
    <section class="detail-section"><h4>Content identity</h4><div class="detail-grid">${metric("External ID",item.externalContentId||"Unknown")}${metric("URL",hostname(item.url)||"Unknown")}</div></section>
    <section class="detail-section"><h4>Inventory slots · ${item.adInventorySlots.length}</h4><div class="check-list">${item.adInventorySlots.map(x=>`<div class="check-item"><div><strong>${escapeHtml(friendlyStatus(x.slotType))}</strong><small>${slotTiming(x)} · planning does not reserve this slot</small></div>${badge(x.isAvailable?"AVAILABLE":"UNAVAILABLE")}</div>`).join("")||emptyState("No inventory slots.")}</div></section>`);
}

async function openCampaign(id) {
  openDrawer("CAMPAIGN","Loading campaign…",skeleton());
  try {
    const campaign=await api(`/api/campaigns/${id}`),opportunity=opportunityById(campaign.advertiserOpportunityId);
    $("#drawer-title").textContent=campaign.name;
    $("#drawer-body").innerHTML=`<div class="detail-hero"><div class="detail-hero-top">${badge(campaign.status)}<span class="detail-score">▣</span></div><h3>${escapeHtml(campaign.name)}</h3><p>${escapeHtml(opportunity?.name||"No opportunity bound")} · created ${formatDate(campaign.createdAt)}</p><div class="detail-actions"><button class="small-button" data-export-campaign="${campaign.id}">Export case file</button><button class="small-button" data-verify-pack>Verify pack</button></div></div>
      <section class="detail-section"><h4>Planned placements · ${campaign.placements.length}</h4>${campaign.placements.length?campaign.placements.map(x=>`<div class="check-item"><div><strong>${escapeHtml(state.contentDetails.find(c=>c.id===x.contentItemId)?.title||shortId(x.contentItemId))}</strong><small>${x.blissMatchId?`Certificate ${shortId(x.blissMatchId)}`:"Legacy placement"} · slot ${shortId(x.adInventorySlotId)}</small></div>${badge(x.status)}</div>`).join(""):emptyState("No placements are planned for this campaign.")}</section>`;
  } catch(error){drawerError(error);}
}

async function openMatch(id) {
  openDrawer("MATCH CERTIFICATE","Loading match…",skeleton());
  try {
    const [match,runs,reviews]=await Promise.all([api(`/api/bliss/matches/${id}`),api(`/api/bliss/matches/${id}/evaluation-runs`),api(`/api/bliss/matches/${id}/review-decisions`)]);
    const formation=state.formations.find(x=>x.blissMatchId===id);
    const placement=state.placements.find(x=>x.blissMatchId===id);
    $("#drawer-title").textContent=match.creator.name;
    $("#drawer-body").innerHTML=`<div class="detail-hero"><div class="detail-hero-top">${badge(match.status)}<span class="detail-score">${formatScore(match.overallScore)}</span></div><h3>${escapeHtml(match.advertiserOpportunity.name)}</h3><p>${escapeHtml(match.advertiserOpportunity.advertiserProgram.advertiser.name)} · Rule ${escapeHtml(match.ruleVersion.version)} · ${formatPercent(match.confidenceScore)} confidence</p><div class="detail-actions"><button class="small-button" data-export-match="${match.id}">Export case file</button><button class="small-button" data-verify-pack>Verify pack</button>${ruleById(match.ruleVersion.id)?.documentJson?`<button class="small-button" data-evaluate="${match.id}">Replay evaluation</button>`:""}${match.status==="REVIEW_REQUIRED"?`<button class="small-button" data-open-review="${match.id}">Review match</button>`:""}${match.status==="APPROVED"&&!placement?`<button class="small-button" data-open-placement="${match.id}">Plan placement</button>`:""}</div></div>
      <section class="detail-section"><h4>Certificate timeline</h4><div class="check-list">${formation?`<button class="history-item" data-formation-run-id="${formation.id}"><span><strong>Certificate formed</strong><small>${formatDate(formation.completedAt)} · ${escapeHtml(formation.sourceSystem)}</small></span><span>→</span></button>`:""}${runs.map(x=>`<button class="history-item" data-run-id="${x.id}"><span><strong>Deterministic evaluation</strong><small>${formatDate(x.completedAt||x.startedAt)} · ${escapeHtml(x.algorithmVersion)}</small></span>${badge(x.matchStatus)}</button>`).join("")}${reviews.map(x=>`<button class="history-item" data-review-id="${x.id}"><span><strong>Human review · ${escapeHtml(x.reviewerLabel)}</strong><small>${formatDate(x.completedAt)}</small></span>${badge(x.decision)}</button>`).join("")}${placement?`<button class="history-item" data-placement-run-id="${placement.id}"><span><strong>Placement planned</strong><small>${formatDate(placement.completedAt)} · ${escapeHtml(campaignById(placement.campaignId)?.name||shortId(placement.campaignId))}</small></span>${badge("PLANNED")}</button>`:""}</div></section>
      <section class="detail-section"><h4>Eligibility evidence</h4><div class="check-list">${match.eligibilityChecks.length?match.eligibilityChecks.map(x=>`<div class="check-item"><div><strong>${escapeHtml(friendlyStatus(x.checkType))}</strong><small>${escapeHtml(x.explanation||x.reasonCode||"No explanation")}</small></div>${badge(x.result)}</div>`).join(""):emptyState("This certificate has not been evaluated.")}</div></section>
      <section class="detail-section"><h4>Score components</h4><div class="check-list">${match.scoreComponents.length?match.scoreComponents.map(x=>`<div class="check-item"><div><strong>${escapeHtml(friendlyStatus(x.componentName))}</strong><small>${escapeHtml(x.explanation||"No explanation")}</small></div><strong>${formatScore(x.score)} × ${formatScore(x.weight)}</strong></div>`).join(""):emptyState("No score components recorded.")}</div></section>
      <section class="detail-section"><h4>Rule document</h4><pre class="json-block">${prettyJson(ruleById(match.ruleVersion.id)?.documentJson)}</pre></section>`;
  } catch(error){drawerError(error);}
}

async function openRun(id) {
  openDrawer("EVALUATION RUN",shortId(id),skeleton());
  try { const x=await api(`/api/match-evaluation-runs/${id}`);$("#drawer-body").innerHTML=`<div class="detail-hero"><div class="detail-hero-top">${badge(x.matchStatus)}<span class="detail-score">${formatScore(x.overallScore)}</span></div><h3>${escapeHtml(x.algorithmVersion)}</h3><p>Completed ${formatDate(x.completedAt||x.startedAt)} · ${formatPercent(x.confidenceScore)} confidence</p></div><section class="detail-section"><h4>Input snapshot</h4><pre class="json-block">${prettyJson(x.inputSnapshot)}</pre></section><section class="detail-section"><h4>Output snapshot</h4><pre class="json-block">${prettyJson(x.outputSnapshot)}</pre></section>`; } catch(error){drawerError(error);}
}
async function openFormationRun(id) { openDrawer("FORMATION RUN",shortId(id),skeleton());try{const x=await api(`/api/match-formation-runs/${id}`);$("#drawer-body").innerHTML=runDetailHero("＋",x.outcome,opportunityById(x.advertiserOpportunityId)?.name||shortId(x.advertiserOpportunityId),`${creatorById(x.creatorId)?.name||shortId(x.creatorId)} · ${formatDate(x.completedAt)}`)+`<section class="detail-section"><h4>Certificate</h4><div class="check-item"><div><strong>${shortId(x.blissMatchId)}</strong><small>${x.evaluateOnCreate?"Evaluated immediately":"Evaluation deferred"}</small></div><button class="small-button" data-match-id="${x.blissMatchId}">Open match</button></div></section><section class="detail-section"><h4>Input snapshot</h4><pre class="json-block">${prettyJson(x.inputSnapshot)}</pre></section>`;}catch(error){drawerError(error);}}
async function openReviewDecision(id) { openDrawer("REVIEW DECISION",shortId(id),skeleton());try{const x=await api(`/api/match-review-decisions/${id}`);$("#drawer-title").textContent=creatorById(x.creatorId)?.name||shortId(x.creatorId);$("#drawer-body").innerHTML=runDetailHero("☑",x.decision,x.reviewerLabel,`${x.sourceSystem} · ${formatDate(x.completedAt)}`)+`<section class="detail-section"><h4>Decision rationale</h4><div class="check-item"><div><strong>${friendlyStatus(x.resultingMatchStatus)}</strong><small>${escapeHtml(x.rationale)}</small></div><button class="small-button" data-match-id="${x.blissMatchId}">Open match</button></div></section><section class="detail-section"><h4>Input snapshot</h4><pre class="json-block">${prettyJson(x.inputSnapshot)}</pre></section>`;}catch(error){drawerError(error);}}
async function openPlacementRun(id) { openDrawer("PLACEMENT RUN",shortId(id),skeleton());try{const x=await api(`/api/campaign-placement-runs/${id}`);$("#drawer-title").textContent=creatorById(x.creatorId)?.name||shortId(x.creatorId);$("#drawer-body").innerHTML=runDetailHero("▣","PLANNED",campaignById(x.campaignId)?.name||shortId(x.campaignId),`${x.operatorLabel} · ${formatDate(x.completedAt)}`)+`<section class="detail-section"><h4>Inventory intent</h4><div class="check-item"><div><strong>${escapeHtml(state.contentDetails.find(c=>c.id===x.contentItemId)?.title||shortId(x.contentItemId))}</strong><small>Slot ${shortId(x.adInventorySlotId)} · no reservation or delivery</small></div><button class="small-button" data-campaign-id="${x.campaignId}">Open campaign</button></div></section><section class="detail-section"><h4>Input snapshot</h4><pre class="json-block">${prettyJson(x.inputSnapshot)}</pre></section>`;}catch(error){drawerError(error);}}
async function openIngestionRun(id) { openDrawer("INGESTION RUN",shortId(id),skeleton());try{const x=await api(`/api/creator-ingestions/${id}`);$("#drawer-title").textContent=creatorById(x.creatorId)?.name||shortId(x.creatorId);$("#drawer-body").innerHTML=runDetailHero("↓",x.outcome,x.identityKey,`${x.sourceSystem} · ${formatDate(x.completedAt)}`)+`<section class="detail-section"><h4>Canonical identity</h4><div class="check-item"><div><strong>${escapeHtml(x.identityKey)}</strong><small>Provider platform + external profile ID</small></div><button class="small-button" data-creator-id="${x.creatorId}">Open creator</button></div></section><section class="detail-section"><h4>Input snapshot</h4><pre class="json-block">${prettyJson(x.inputSnapshot)}</pre></section>`;}catch(error){drawerError(error);}}

async function evaluateMatch(id,button) {
  if (!state.session.canWrite) {
    toast("Your account does not have operator permission.", true);
    return;
  }

  const original=button.textContent;button.disabled=true;button.textContent="Running…";
  try{const result=await api(`/api/bliss/matches/${id}/evaluate-rules`,{method:"POST"});toast(`${friendlyStatus(result.status)} · evaluation appended`);await loadDashboard();openMatch(id);}
  catch(error){toast(error.message,true);}finally{button.disabled=false;button.textContent=original;}
}

function bindNavigation() {
  window.addEventListener("hashchange", route);
  $("#mobile-menu").addEventListener("click",()=>toggleMobileNav());
  $("#mobile-backdrop").addEventListener("click",()=>toggleMobileNav(false));
}
function route() {
  const parts=(location.hash.replace(/^#\/?/,"")||"overview").split("/").filter(Boolean);
  const valid=["overview","creators","matches","review","placement","wedding-planner","partners","inventory","audit","status"];
  const view=valid.includes(parts[0])?parts[0]:"overview";
  $$(".view").forEach(x=>x.classList.toggle("active",x.id===`view-${view}`));
  $$(".nav-item[data-view]").forEach(x=>{const active=x.dataset.view===view;x.classList.toggle("active",active);if(active)x.setAttribute("aria-current","page");else x.removeAttribute("aria-current");});
  const titles={overview:"Operations overview",creators:"Creator operations",matches:"Match certificates",review:"Human review",placement:"Campaign placement","wedding-planner":"Wedding Planner Phase 2",partners:"Partner directory",inventory:"Inventory and campaigns",audit:"Operations audit",status:"Workspace status"};
  $("#page-title").textContent=titles[view];
  toggleMobileNav(false);
  if(!state.loaded)return;
  if(view==="creators"&&parts[1])openCreator(parts[1]);
  else if(view==="matches"&&parts[1])openMatch(parts[1]);
  else if(view==="partners"&&parts[1])openAdvertiser(parts[1]);
  else if(view==="wedding-planner"&&parts[1])selectWeddingPlannerWorkspace(parts[1]);
  else if(parts.length===1)closeDrawer(false);
}

function bindActions() {
  $("#refresh-button").addEventListener("click",loadDashboard);
  $("#refresh-runtime-status").addEventListener("click",refreshRuntimeStatus);
  $("#verify-write-throttle").addEventListener("click",verifyWriteThrottle);
  $("#export-audit-ledger").addEventListener("click",exportAuditLedger);
  $("#verify-audit-pack").addEventListener("click",chooseAuditPackToVerify);
  $("#verify-audit-file").addEventListener("change",verifySelectedAuditPack);
  $("#operator-button").addEventListener("click",() => {
    if (state.session.authenticationEnabled && !state.session.isAuthenticated) beginLogin();
    else openSettings();
  });
  $("#settings-button").addEventListener("click",openSettings);
  $("#auth-login-button").addEventListener("click",beginLogin);
  $("#auth-logout-button").addEventListener("click",async()=>{
    try {
      await api("/api/auth/logout",{method:"POST"});
      location.assign("/");
    } catch(error) {
      toast(error.message,true);
    }
  });
  $("#save-settings").addEventListener("click",event=>{
    event.preventDefault();
    if (!state.session.authenticationEnabled) {
      state.apiBase=$("#api-url").value.trim().replace(/\/$/,"");
      state.operator=$("#operator-label").value.trim();
      localStorage.setItem("bliss-api-base",state.apiBase);
      localStorage.setItem("bliss-operator-label",state.operator);
    }
    $("#settings-dialog").close();
    syncOperatorUi();
    if (!state.session.authenticationEnabled) loadDashboard();
  });
  $("#workflow-form").addEventListener("submit",event=>{event.preventDefault();submitWorkflow(event.currentTarget);});
  $("#review-form").addEventListener("submit",event=>{event.preventDefault();submitReview(event.currentTarget);});
  $("#placement-form").addEventListener("submit",event=>{event.preventDefault();submitPlacement(event.currentTarget);});
  $("#wedding-planner-open-form").addEventListener("submit",event=>{event.preventDefault();submitWeddingPlannerWorkspace(event.currentTarget);});
  $("#wedding-planner-session-form").addEventListener("submit",event=>{event.preventDefault();submitWeddingPlannerSession(event.currentTarget);});
  $("#wedding-planner-message-form").addEventListener("submit",event=>{event.preventDefault();submitWeddingPlannerMessage(event.currentTarget);});
  $("#wedding-planner-interpret-form").addEventListener("submit",event=>{event.preventDefault();submitWeddingPlannerInterpret(event.currentTarget);});
  $("#wedding-planner-dna-decision-form").addEventListener("submit",event=>{event.preventDefault();submitWeddingPlannerDnaDecision(event.currentTarget);});
  $("#regenerate-review-key").addEventListener("click",generateReviewIdempotencyKey);
  $("#regenerate-placement-key").addEventListener("click",generatePlacementIdempotencyKey);
  $("#regenerate-wedding-planner-workspace-key").addEventListener("click",()=>fillKey("#wedding-planner-workspace-key"));
  $("#regenerate-wedding-planner-session-key").addEventListener("click",()=>fillKey("#wedding-planner-session-key"));
  $("#regenerate-wedding-planner-message-key").addEventListener("click",()=>fillKey("#wedding-planner-message-key"));
  $("#regenerate-wedding-planner-interpret-key").addEventListener("click",()=>fillKey("#wedding-planner-interpret-key"));
  $("#regenerate-wedding-planner-dna-decision-key").addEventListener("click",()=>fillKey("#wedding-planner-dna-decision-key"));
  $("#placement-match").addEventListener("change",event=>{state.selectedPlacement=event.target.value;renderPlacements();});
  $("#placement-content").addEventListener("change",updatePlacementSlots);
  $("#review-match").addEventListener("change",event=>{state.selectedReview=event.target.value;renderReviews();});
  $("#drawer-close").addEventListener("click",()=>closeDrawer());
  $("#drawer-backdrop").addEventListener("click",()=>closeDrawer());
  $("#match-search").addEventListener("input",event=>{state.matchSearch=event.target.value;renderMatches();});
  $("#creator-search").addEventListener("input",event=>{state.creatorSearch=event.target.value;renderCreators();});
  $("#audit-search").addEventListener("input",event=>{state.auditSearch=event.target.value;renderAudit();});
  $("#match-filters").addEventListener("click",event=>{const button=event.target.closest("[data-status]");if(!button)return;state.matchFilter=button.dataset.status;$$(".filter-chip",$("#match-filters")).forEach(x=>{const active=x===button;x.classList.toggle("active",active);x.setAttribute("aria-pressed",String(active));});renderMatches();});
  $("#partner-tabs").addEventListener("click",event=>{const button=event.target.closest("[data-partner-tab]");if(!button)return;state.partnerTab=button.dataset.partnerTab;renderPartners();});
  $("#inventory-tabs").addEventListener("click",event=>{const button=event.target.closest("[data-inventory-tab]");if(!button)return;state.inventoryTab=button.dataset.inventoryTab;renderInventory();});
  $("#audit-tabs").addEventListener("click",event=>{const button=event.target.closest("[data-audit-tab]");if(!button)return;state.auditTab=button.dataset.auditTab;renderAudit();});
  document.addEventListener("click",handleDocumentClick);
  document.addEventListener("keydown",handleKeydown);
}

function handleDocumentClick(event) {
  const target=event.target.closest("button,a");
  if(!target)return;
  if(target.matches("[data-workflow]"))showWorkflow(target.dataset.workflow);
  else if(target.matches("[data-close-dialog]"))target.closest("dialog").close();
  else if(target.matches("[data-regenerate-inline]"))target.previousElementSibling.value=`manual-${crypto.randomUUID()}`;
  else if(target.matches("[data-creator-id]"))location.hash=`#/creators/${target.dataset.creatorId}`;
  else if(target.matches("[data-wedding-workspace]"))location.hash=`#/wedding-planner/${target.dataset.weddingWorkspace}`;
  else if(target.matches("[data-match-id]"))location.hash=`#/matches/${target.dataset.matchId}`;
  else if(target.matches("[data-campaign-id]")){state.inventoryTab="campaigns";renderInventory();location.hash=`#/inventory/${target.dataset.campaignId}`;}
  else if(target.matches("[data-content-id]"))openContent(target.dataset.contentId);
  else if(target.matches("[data-run-id]"))openRun(target.dataset.runId);
  else if(target.matches("[data-formation-run-id]"))openFormationRun(target.dataset.formationRunId);
  else if(target.matches("[data-review-id]"))openReviewDecision(target.dataset.reviewId);
  else if(target.matches("[data-placement-run-id]"))openPlacementRun(target.dataset.placementRunId);
  else if(target.matches("[data-ingest-run-id]"))openIngestionRun(target.dataset.ingestRunId);
  else if(target.matches("[data-export-match]"))exportMatchCase(target.dataset.exportMatch,target);
  else if(target.matches("[data-export-creator]"))exportCreatorCase(target.dataset.exportCreator,target);
  else if(target.matches("[data-export-campaign]"))exportCampaignCase(target.dataset.exportCampaign,target);
  else if(target.matches("[data-verify-pack]"))chooseAuditPackToVerify();
  else if(target.matches("[data-evaluate]"))evaluateMatch(target.dataset.evaluate,target);
  else if(target.matches("[data-select-review]")){state.selectedReview=target.dataset.selectReview;renderReviews();$("#review-form").scrollIntoView({behavior:"smooth",block:"start"});}
  else if(target.matches("[data-select-placement]")){state.selectedPlacement=target.dataset.selectPlacement;renderPlacements();$("#placement-form").scrollIntoView({behavior:"smooth",block:"start"});}
  else if(target.matches("[data-open-review]")){state.selectedReview=target.dataset.openReview;location.hash="#/review";setTimeout(()=>renderReviews(),0);}
  else if(target.matches("[data-open-placement]")){state.selectedPlacement=target.dataset.openPlacement;location.hash="#/placement";setTimeout(()=>renderPlacements(),0);}
  else if(target.matches("[data-form-match-creator]"))showWorkflow("formation",{creatorId:target.dataset.formMatchCreator});
  else if(target.matches("[data-audit-type]"))openAuditRecord(target.dataset.auditType,target.dataset.auditId);
}

function handleKeydown(event) {
  if(event.key==="Escape"&&$("#detail-drawer").classList.contains("open"))closeDrawer();
  if(event.key==="Tab"&&$("#detail-drawer").classList.contains("open")){
    const focusable=$$("button,[href],input,select,textarea,[tabindex]:not([tabindex='-1'])",$("#detail-drawer")).filter(x=>!x.disabled);
    if(!focusable.length)return;
    const first=focusable[0],last=focusable.at(-1);
    if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}
    else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}
  }
}

function openAuditRecord(type,id) {
  ({evaluation:openRun,review:openReviewDecision,placement:openPlacementRun,ingestion:openIngestionRun,formation:openFormationRun}[type]||(()=>{}))(id);
}
function openDrawer(eyebrow,title,body) {
  drawerReturnFocus=document.activeElement;
  $("#drawer-eyebrow").textContent=eyebrow;$("#drawer-title").textContent=title;$("#drawer-body").innerHTML=body;
  $("#detail-drawer").classList.add("open");$("#detail-drawer").setAttribute("aria-hidden","false");$("#drawer-backdrop").classList.add("open");
  document.body.style.overflow="hidden";setTimeout(()=>$("#drawer-close").focus(),0);
}
function closeDrawer(restoreHash=true) {
  const wasOpen=$("#detail-drawer").classList.contains("open");
  $("#detail-drawer").classList.remove("open");$("#detail-drawer").setAttribute("aria-hidden","true");$("#drawer-backdrop").classList.remove("open");document.body.style.overflow="";
  if(restoreHash&&wasOpen){const view=(location.hash.replace(/^#\/?/,"").split("/")[0]||"overview");history.replaceState(null,"",`#/${view}`);}
  if(wasOpen&&drawerReturnFocus?.focus)drawerReturnFocus.focus();
}
function drawerError(error){$("#drawer-body").innerHTML=emptyState(error.message);}
function beginLogin() {
  const returnUrl = `${location.pathname}${location.search}${location.hash}`;
  location.assign(`/api/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`);
}
function showAuthenticationGate() {
  $(".content").classList.add("auth-required");
  $("#auth-gate").hidden = false;
  $("#refresh-button").disabled = true;
  setConnection("auth", "OIDC session required");
}
function openSettings(){
  $("#api-url").value=state.apiBase;
  $("#operator-label").value=state.operator;
  $("#settings-dialog").showModal();
}
function toggleMobileNav(force){const open=force??!$(".sidebar").classList.contains("open");$(".sidebar").classList.toggle("open",open);$("#mobile-backdrop").classList.toggle("open",open);$("#mobile-menu").setAttribute("aria-expanded",String(open));}
function syncOperatorUi(){
  const secured = state.session.authenticationEnabled;
  const label = secured
    ? (state.session.displayName || "Sign in")
    : (state.operator || "Set operator");
  $("#operator-name").textContent=label;
  $("#operator-avatar").textContent=label==="Sign in"||label==="Set operator"?"?":getInitials(label);
  $$(".operator-field").forEach(field=>{
    field.value=label==="Sign in"||label==="Set operator"?"":label;
    field.readOnly=secured;
  });
  $("#local-operator-settings").hidden=secured;
  $("#api-url").disabled=secured;
  $("#auth-logout-button").hidden=!(secured&&state.session.isAuthenticated);
  $("#settings-session-copy").textContent=secured
    ? `Signed in as ${label}. Permissions come from your identity provider roles.`
    : "Authentication is disabled for this development workspace. The local operator label is audit metadata only.";
  $("#workspace-label").textContent=secured?"SSO protected":state.apiBase?hostname(state.apiBase):"Development workspace";
}
function applyPermissions(){
  $$("[data-workflow], [data-evaluate], [data-select-placement]").forEach(button=>{
    button.disabled=!state.session.canWrite;
    if(button.disabled)button.title="Operator role required";
  });
  $$("[data-select-review]").forEach(button=>{
    button.disabled=!state.session.canReview;
    if(button.disabled)button.title="Reviewer role required";
  });
  $("#review-submit").disabled=!state.reviewQueue.length||!state.session.canReview;
  $("#placement-submit").disabled=!state.placementQueue.length||!state.session.canWrite;
}
function setConnection(status,detail){const dot=$("#connection-dot");dot.className=`pulse-dot ${status==="loading"||status==="auth"?"":status}`;$("#connection-label").textContent=status==="online"?"API connected":status==="offline"?"API unavailable":status==="auth"?"Sign in required":"Connecting";$("#connection-detail").textContent=detail||(state.apiBase?hostname(state.apiBase):"Same-origin backend");}
function renderUnavailable(message){const content=emptyState(`Could not load backend data: ${message}`);["match-list","creator-grid","review-queue-list","placement-queue-list","recent-activity","status-chart","partner-content","inventory-content","audit-content"].forEach(id=>{const element=$(`#${id}`);if(element)element.innerHTML=content;});}
function toast(message,isError=false){const element=$("#toast");element.textContent=message;element.className=`toast show${isError?" error":""}`;clearTimeout(toast.timer);toast.timer=setTimeout(()=>element.classList.remove("show"),3600);}

function creatorById(id){return state.creators.find(x=>x.id===id);}
function advertiserById(id){return state.advertisers.find(x=>x.id===id);}
function programById(id){return state.programs.find(x=>x.id===id);}
function opportunityById(id){return state.opportunities.find(x=>x.id===id);}
function campaignById(id){return state.campaigns.find(x=>x.id===id);}
function ruleById(id){return state.ruleVersions.find(x=>x.id===id);}
function statusClass(status="CREATED"){return `status-${String(status).toLowerCase()}`;}
function friendlyStatus(value="UNKNOWN"){return String(value).replaceAll("_"," ").toLowerCase().replace(/\b\w/g,char=>char.toUpperCase());}
function formatScore(value){return value==null?"—":Number(value).toFixed(2);}
function formatPercent(value){return value==null?"—":`${Math.round(Number(value)*100)}%`;}
function formatNullablePercent(value){return value==null?"Unknown":`${Number(value).toFixed(0)}%`;}
function formatNumber(value){return value==null?"Unknown":new Intl.NumberFormat("en",{notation:"compact"}).format(value);}
function formatDate(value){return value?new Intl.DateTimeFormat("en",{dateStyle:"medium",timeStyle:"short"}).format(new Date(value)):"—";}
function relativeTime(value){if(!value)return"—";const seconds=Math.round((new Date(value)-new Date())/1000);for(const[unit,name]of[[86400,"day"],[3600,"hour"],[60,"minute"]])if(Math.abs(seconds)>=unit)return new Intl.RelativeTimeFormat("en",{numeric:"auto"}).format(Math.round(seconds/unit),name);return"just now";}
function shortId(id=""){return id.length>13?`${id.slice(0,8)}…${id.slice(-4)}`:id;}
function getInitials(value="?"){return value.trim().split(/\s+/).slice(0,2).map(part=>part[0]||"").join("").toUpperCase()||"?";}
function contentSymbol(type){return type==="VIDEO"?"▶":type==="PODCAST"?"◉":"▦";}
function hostname(value){if(!value)return"";try{return new URL(value).hostname;}catch{return String(value);}}
function slotTiming(slot){if(slot.startSecond==null&&slot.durationSeconds==null)return"Timing flexible";return `${slot.startSecond??0}s start · ${slot.durationSeconds??"?"}s duration`;}
function byCompleted(a,b){return new Date(b.completedAt||b.startedAt)-new Date(a.completedAt||a.startedAt);}
function auditHaystack(value){return JSON.stringify(value).toLowerCase();}
function code(value){return `<code>${escapeHtml(value)}</code>`;}
function badge(value){return `<span class="status-badge ${statusClass(value)}">${escapeHtml(friendlyStatus(value))}</span>`;}
function inspectButton(attribute,id){return `<button class="small-button" ${attribute}="${id}">Inspect</button>`;}
function metric(label,value){return `<div class="metric-box"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></div>`;}
function activityButton(symbol,title,detail,at,attribute){return `<button class="activity-item" ${attribute}><span class="activity-icon">${symbol}</span><span><strong>${escapeHtml(title)}</strong><small>${escapeHtml(detail)}</small></span><span class="activity-time">${relativeTime(at)}</span></button>`;}
function runDetailHero(symbol,status,title,subtitle){return `<div class="detail-hero"><div class="detail-hero-top">${badge(status)}<span class="detail-score">${symbol}</span></div><h3>${escapeHtml(title)}</h3><p>${escapeHtml(subtitle)}</p></div>`;}
function skeleton(){return `<div class="activity-skeleton"></div><div class="activity-skeleton"></div><div class="activity-skeleton"></div>`;}
function generateReviewIdempotencyKey(){const field=$("#review-idempotency");if(field)field.value=`manual-${crypto.randomUUID()}`;}
function generatePlacementIdempotencyKey(){const field=$("#placement-idempotency");if(field)field.value=`manual-${crypto.randomUUID()}`;}
function fillKey(selector){const field=$(selector);if(field)field.value=`manual-${crypto.randomUUID()}`;}
function generateWeddingPlannerKeys(){
  fillKey("#wedding-planner-workspace-key");
  fillKey("#wedding-planner-session-key");
  fillKey("#wedding-planner-message-key");
  fillKey("#wedding-planner-interpret-key");
  fillKey("#wedding-planner-dna-decision-key");
}

function renderWeddingPlanner() {
  const advertiserSelect = $("#wedding-planner-advertiser");
  const workspaceSelect = $("#wedding-planner-workspace");
  const sessionSelect = $("#wedding-planner-session");
  const interpretWorkspace = $("#wedding-planner-interpret-workspace");
  const dnaVersionSelect = $("#wedding-planner-dna-version");
  if (!advertiserSelect) return;
  advertiserSelect.innerHTML = state.advertisers.map(x => `<option value="${x.id}">${escapeHtml(x.name)}</option>`).join("");
  const workspaceOptions = state.weddingPlannerWorkspaces.map(x => `<option value="${x.workspaceId}">${escapeHtml(x.advertiserName)}</option>`).join("");
  workspaceSelect.innerHTML = workspaceOptions;
  if (interpretWorkspace) interpretWorkspace.innerHTML = workspaceOptions;
  if (state.selectedWeddingPlannerWorkspace) {
    workspaceSelect.value = state.selectedWeddingPlannerWorkspace;
    if (interpretWorkspace) interpretWorkspace.value = state.selectedWeddingPlannerWorkspace;
  }
  sessionSelect.innerHTML = state.weddingPlannerSessions.map(x => `<option value="${x.sessionId}">${x.sessionId.slice(0, 8)} · ${x.messageCount} messages</option>`).join("");
  if (state.selectedWeddingPlannerSession) sessionSelect.value = state.selectedWeddingPlannerSession;
  const list = $("#wedding-planner-workspace-list");
  list.innerHTML = state.weddingPlannerWorkspaces.length
    ? state.weddingPlannerWorkspaces.map(x => `<button class="activity-item" data-wedding-workspace="${x.workspaceId}"><span class="activity-icon">💒</span><span><strong>${escapeHtml(x.advertiserName)}</strong><small>${escapeHtml(x.status)} · primary workspace</small></span><span class="activity-time">${relativeTime(x.updatedAt)}</span></button>`).join("")
    : emptyState("No Wedding Planner workspaces yet.");
  const messages = $("#wedding-planner-message-list");
  messages.innerHTML = state.weddingPlannerMessages.length
    ? state.weddingPlannerMessages.map(x => `<div class="activity-item"><span class="activity-icon">${x.sequenceNumber}</span><span><strong>${escapeHtml(x.actorType)}</strong><small>${escapeHtml(x.body)}</small></span><span class="activity-time">${relativeTime(x.createdAt)}</span></div>`).join("")
    : emptyState("Select a session to resume durable messages.");
  const runs = $("#wedding-planner-agent-run-list");
  if (runs) {
    runs.innerHTML = state.weddingPlannerAgentRuns.length
      ? state.weddingPlannerAgentRuns.map(x => `<div class="activity-item"><span class="activity-icon">◇</span><span><strong>${escapeHtml(x.logicalRole)} · ${escapeHtml(x.status)}</strong><small>${escapeHtml(x.promptPackVersion || "—")} · ${escapeHtml(x.outcome || x.errorMessage || "no outcome")} · ${x.totalTokens ?? "—"} tokens</small></span><span class="activity-time">${relativeTime(x.completedAt || x.startedAt)}</span></div>`).join("")
      : emptyState("Select a session to inspect Concierge agent runs.");
  }
  const versions = state.weddingPlannerBrandDna?.versions || [];
  const currentId = state.weddingPlannerBrandDna?.currentApprovedBrandDnaVersionId || null;
  const currentLabel = $("#wedding-planner-dna-current");
  if (currentLabel) currentLabel.textContent = currentId ? `Current: ${shortId(currentId)}` : "Current: none";
  if (dnaVersionSelect) {
    dnaVersionSelect.innerHTML = versions.length
      ? versions.map(x => `<option value="${x.brandDnaVersionId}">v${x.versionNumber} · ${escapeHtml(x.status)}${x.brandDnaVersionId === currentId ? " · CURRENT" : ""}</option>`).join("")
      : `<option value="">No Brand DNA versions</option>`;
  }
  const dnaList = $("#wedding-planner-brand-dna-list");
  if (dnaList) {
    dnaList.innerHTML = versions.length
      ? versions.map(x => `<div class="activity-item"><span class="activity-icon">${x.versionNumber}</span><span><strong>${escapeHtml(x.status)}${x.brandDnaVersionId === currentId ? " · CURRENT APPROVED" : ""}</strong><small>${escapeHtml(x.summary || "No summary")} · ${escapeHtml(x.schemaVersion)}</small></span><span class="activity-time">${relativeTime(x.createdAt)}</span></div>`).join("")
      : emptyState("Select or create a workspace to list Brand DNA versions.");
  }
}

async function selectWeddingPlannerWorkspace(workspaceId) {
  state.selectedWeddingPlannerWorkspace = workspaceId;
  try {
    state.weddingPlannerSessions = await api(`/api/wedding-planner/workspaces/${workspaceId}/sessions`);
    state.selectedWeddingPlannerSession = state.weddingPlannerSessions[0]?.sessionId || null;
    if (state.selectedWeddingPlannerSession) {
      state.weddingPlannerMessages = await api(`/api/wedding-planner/sessions/${state.selectedWeddingPlannerSession}/messages`);
      state.weddingPlannerAgentRuns = await api(`/api/wedding-planner/sessions/${state.selectedWeddingPlannerSession}/agent-runs`);
    } else {
      state.weddingPlannerMessages = [];
      state.weddingPlannerAgentRuns = [];
    }
    state.weddingPlannerBrandDna = await api(`/api/wedding-planner/workspaces/${workspaceId}/brand-dna`);
  } catch (error) {
    toast(error.message, true);
    state.weddingPlannerSessions = [];
    state.weddingPlannerMessages = [];
    state.weddingPlannerAgentRuns = [];
    state.weddingPlannerBrandDna = null;
  }
  renderWeddingPlanner();
}

async function submitWeddingPlannerWorkspace(form) {
  const value = name => form.elements[name].value.trim();
  try {
    const result = await api("/api/wedding-planner/workspaces", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        advertiserId: value("advertiserId"),
        sourceSystem: value("sourceSystem"),
        idempotencyKey: value("idempotencyKey")
      })
    });
    toast(result.isReplay ? "Primary workspace resumed" : "Primary workspace opened");
    fillKey("#wedding-planner-workspace-key");
    await loadDashboard();
    await selectWeddingPlannerWorkspace(result.workspaceId);
    location.hash = `#/wedding-planner/${result.workspaceId}`;
  } catch (error) {
    toast(error.message, true);
  }
}

async function submitWeddingPlannerSession(form) {
  const value = name => form.elements[name].value.trim();
  try {
    const result = await api(`/api/wedding-planner/workspaces/${value("workspaceId")}/sessions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        sourceSystem: value("sourceSystem"),
        idempotencyKey: value("idempotencyKey")
      })
    });
    toast(result.isReplay ? "Planning session resumed" : "Planning session created");
    fillKey("#wedding-planner-session-key");
    state.selectedWeddingPlannerWorkspace = result.workspaceId;
    state.selectedWeddingPlannerSession = result.sessionId;
    state.weddingPlannerSessions = await api(`/api/wedding-planner/workspaces/${result.workspaceId}/sessions`);
    state.weddingPlannerMessages = await api(`/api/wedding-planner/sessions/${result.sessionId}/messages`);
    state.weddingPlannerAgentRuns = await api(`/api/wedding-planner/sessions/${result.sessionId}/agent-runs`);
    state.weddingPlannerBrandDna = await api(`/api/wedding-planner/workspaces/${result.workspaceId}/brand-dna`);
    renderWeddingPlanner();
  } catch (error) {
    toast(error.message, true);
  }
}

async function submitWeddingPlannerMessage(form) {
  const value = name => form.elements[name].value.trim();
  try {
    const result = await api(`/api/wedding-planner/sessions/${value("sessionId")}/messages`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        actorType: value("actorType"),
        body: value("body"),
        sourceSystem: value("sourceSystem"),
        idempotencyKey: value("idempotencyKey")
      })
    });
    toast(result.isReplay ? "Message replayed" : "Message appended");
    form.elements.body.value = "";
    fillKey("#wedding-planner-message-key");
    state.selectedWeddingPlannerSession = result.sessionId;
    state.weddingPlannerMessages = await api(`/api/wedding-planner/sessions/${result.sessionId}/messages`);
    state.weddingPlannerAgentRuns = await api(`/api/wedding-planner/sessions/${result.sessionId}/agent-runs`).catch(() => state.weddingPlannerAgentRuns);
    renderWeddingPlanner();
  } catch (error) {
    toast(error.message, true);
  }
}

async function submitWeddingPlannerInterpret(form) {
  const value = name => form.elements[name].value.trim();
  try {
    const workspaceId = value("workspaceId");
    const result = await api(`/api/wedding-planner/workspaces/${workspaceId}/brand-dna/interpret`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        sourceSystem: value("sourceSystem"),
        idempotencyKey: value("idempotencyKey")
      })
    });
    toast(result.isReplay ? `Brand DNA v${result.versionNumber} replayed` : `Brand DNA v${result.versionNumber} proposed`);
    fillKey("#wedding-planner-interpret-key");
    state.selectedWeddingPlannerWorkspace = workspaceId;
    state.weddingPlannerBrandDna = await api(`/api/wedding-planner/workspaces/${workspaceId}/brand-dna`);
    renderWeddingPlanner();
  } catch (error) {
    toast(error.message, true);
  }
}

async function submitWeddingPlannerDnaDecision(form) {
  const value = name => form.elements[name].value.trim();
  const decision = value("decision");
  if (decision === "APPROVE" && form.elements.confirmApprove?.checked !== true) {
    toast("Confirm the APPROVE checkbox before approving Brand DNA.", true);
    return;
  }
  try {
    const result = await api(`/api/wedding-planner/brand-dna/${value("brandDnaVersionId")}/decisions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        decision,
        rationale: value("rationale"),
        sourceSystem: value("sourceSystem"),
        idempotencyKey: value("idempotencyKey")
      })
    });
    toast(`${friendlyStatus(result.decision)} recorded${result.isReplay ? " (replay)" : ""}`);
    form.elements.rationale.value = "";
    if (form.elements.confirmApprove) form.elements.confirmApprove.checked = false;
    fillKey("#wedding-planner-dna-decision-key");
    const workspaceId = result.workspaceId || state.selectedWeddingPlannerWorkspace;
    if (workspaceId) {
      state.weddingPlannerBrandDna = await api(`/api/wedding-planner/workspaces/${workspaceId}/brand-dna`);
    }
    renderWeddingPlanner();
  } catch (error) {
    toast(error.message, true);
  }
}
function prettyJson(value){try{return escapeHtml(JSON.stringify(typeof value==="string"?JSON.parse(value):value,null,2));}catch{return escapeHtml(value||"No snapshot");}}
function emptyState(message){return `<div class="empty-state">${escapeHtml(message)}</div>`;}
function escapeHtml(value){return String(value??"").replace(/[&<>"']/g,char=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#039;"}[char]));}
