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
  weddingPlannerAgentRuns: [], weddingPlannerBrandDna: null, weddingPlannerColorProfiles: null,
  weddingPlannerResearchJobs: [], weddingPlannerResearchReports: null,
  weddingPlannerResearchAgentRuns: [],
  weddingPlannerWorkshopJobs: [], weddingPlannerConceptPackages: null,
  weddingPlannerWorkshopContributions: [], weddingPlannerWorkshopAgentRuns: [],
  weddingPlannerWorkshopPinnedColorDocument: null,
  weddingPlannerSelectedConceptId: null,
  weddingPlannerCreativeJobs: [], weddingPlannerCreativePackages: null,
  weddingPlannerCreativeContributions: [], weddingPlannerCreativeAssets: [],
  weddingPlannerCreativeAgentRuns: [],
  weddingPlannerSelectedVariantId: null,
  selectedWeddingPlannerWorkspace: null, selectedWeddingPlannerSession: null,
  selectedWeddingPlannerColorProfileId: null,
  selectedWeddingPlannerResearchJobId: null,
  selectedWeddingPlannerResearchReportId: null,
  selectedWeddingPlannerWorkshopJobId: null,
  selectedWeddingPlannerConceptPackageId: null,
  selectedWeddingPlannerCreativeJobId: null,
  selectedWeddingPlannerCreativePackageId: null,
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
  const titles={overview:"Operations overview",creators:"Creator operations",matches:"Match certificates",review:"Human review",placement:"Campaign placement","wedding-planner":"Wedding Planner Phase 6",partners:"Partner directory",inventory:"Inventory and campaigns",audit:"Operations audit",status:"Workspace status"};
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
  $("#wedding-planner-color-compute-form")?.addEventListener("submit",event=>{event.preventDefault();submitWeddingPlannerColorCompute(event.currentTarget);});
  $("#wedding-planner-color-decision-form")?.addEventListener("submit",event=>{event.preventDefault();submitWeddingPlannerColorDecision(event.currentTarget);});
  $("#wedding-planner-research-job-form")?.addEventListener("submit",event=>{event.preventDefault();submitWeddingPlannerResearchJob(event.currentTarget);});
  $("#wedding-planner-research-decision-form")?.addEventListener("submit",event=>{event.preventDefault();submitWeddingPlannerResearchDecision(event.currentTarget);});
  $("#wedding-planner-workshop-job-form")?.addEventListener("submit",event=>{event.preventDefault();submitWeddingPlannerWorkshopJob(event.currentTarget);});
  $("#wedding-planner-workshop-decision-form")?.addEventListener("submit",event=>{event.preventDefault();submitWeddingPlannerWorkshopDecision(event.currentTarget);});
  $("#wedding-planner-creative-job-form")?.addEventListener("submit",event=>{event.preventDefault();submitWeddingPlannerCreativeJob(event.currentTarget);});
  $("#wedding-planner-creative-decision-form")?.addEventListener("submit",event=>{event.preventDefault();submitWeddingPlannerCreativeDecision(event.currentTarget);});
  $("#wedding-planner-color-version")?.addEventListener("change",event=>{
    state.selectedWeddingPlannerColorProfileId = event.currentTarget.value || null;
    renderWeddingPlannerColorInspect();
  });
  $("#wedding-planner-research-report-version")?.addEventListener("change",event=>{
    state.selectedWeddingPlannerResearchReportId = event.currentTarget.value || null;
    loadWeddingPlannerResearchAgentRuns().then(() => renderWeddingPlannerResearchInspect());
  });
  $("#wedding-planner-workshop-package-version")?.addEventListener("change",event=>{
    state.selectedWeddingPlannerConceptPackageId = event.currentTarget.value || null;
    state.weddingPlannerSelectedConceptId = null;
    loadWeddingPlannerWorkshopDetails().then(() => renderWeddingPlannerWorkshopInspect());
  });
  $("#wedding-planner-workshop-decision")?.addEventListener("change",event=>{
    const decision = event.currentTarget.value;
    if (decision === "REJECT") {
      $$('input[name="selectedConceptId"]').forEach(input => { input.checked = false; });
    }
  });
  $("#wedding-planner-creative-package-version")?.addEventListener("change",event=>{
    state.selectedWeddingPlannerCreativePackageId = event.currentTarget.value || null;
    state.weddingPlannerSelectedVariantId = null;
    loadWeddingPlannerCreativeDetails().then(() => {
      renderWeddingPlannerCreativeInspect();
      syncOpsCreativeVariantRadios();
    });
  });
  $("#wedding-planner-creative-job-kind")?.addEventListener("change",()=>syncOpsCreativeRevisionFields());
  $("#wedding-planner-creative-decision")?.addEventListener("change",event=>{
    const decision = event.currentTarget.value;
    if (decision === "REJECT") {
      $$('#wedding-planner-creative-variant-radios input[name="selectedVariantId"]').forEach(input => { input.checked = false; });
    }
  });
  $("#regenerate-review-key").addEventListener("click",generateReviewIdempotencyKey);
  $("#regenerate-placement-key").addEventListener("click",generatePlacementIdempotencyKey);
  $("#regenerate-wedding-planner-workspace-key").addEventListener("click",()=>fillKey("#wedding-planner-workspace-key"));
  $("#regenerate-wedding-planner-session-key").addEventListener("click",()=>fillKey("#wedding-planner-session-key"));
  $("#regenerate-wedding-planner-message-key").addEventListener("click",()=>fillKey("#wedding-planner-message-key"));
  $("#regenerate-wedding-planner-interpret-key").addEventListener("click",()=>fillKey("#wedding-planner-interpret-key"));
  $("#regenerate-wedding-planner-dna-decision-key").addEventListener("click",()=>fillKey("#wedding-planner-dna-decision-key"));
  $("#regenerate-wedding-planner-color-compute-key")?.addEventListener("click",()=>fillKey("#wedding-planner-color-compute-key"));
  $("#regenerate-wedding-planner-color-decision-key")?.addEventListener("click",()=>fillKey("#wedding-planner-color-decision-key"));
  $("#regenerate-wedding-planner-research-job-key")?.addEventListener("click",()=>fillKey("#wedding-planner-research-job-key"));
  $("#regenerate-wedding-planner-research-decision-key")?.addEventListener("click",()=>fillKey("#wedding-planner-research-decision-key"));
  $("#regenerate-wedding-planner-workshop-job-key")?.addEventListener("click",()=>fillKey("#wedding-planner-workshop-job-key"));
  $("#regenerate-wedding-planner-workshop-decision-key")?.addEventListener("click",()=>fillKey("#wedding-planner-workshop-decision-key"));
  $("#regenerate-wedding-planner-creative-job-key")?.addEventListener("click",()=>fillKey("#wedding-planner-creative-job-key"));
  $("#regenerate-wedding-planner-creative-decision-key")?.addEventListener("click",()=>fillKey("#wedding-planner-creative-decision-key"));
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
  else if(target.matches("[data-wedding-color-profile]")){
    state.selectedWeddingPlannerColorProfileId=target.dataset.weddingColorProfile;
    const select=$("#wedding-planner-color-version");
    if(select)select.value=state.selectedWeddingPlannerColorProfileId;
    renderWeddingPlannerColorInspect();
  }
  else if(target.matches("[data-wedding-research-job]")){
    state.selectedWeddingPlannerResearchJobId=target.dataset.weddingResearchJob;
    renderWeddingPlanner();
  }
  else if(target.matches("[data-wedding-research-report]")){
    state.selectedWeddingPlannerResearchReportId=target.dataset.weddingResearchReport;
    const select=$("#wedding-planner-research-report-version");
    if(select)select.value=state.selectedWeddingPlannerResearchReportId;
    loadWeddingPlannerResearchAgentRuns().then(() => {
      renderWeddingPlanner();
    });
  }
  else if(target.matches("[data-wedding-workshop-job]")){
    state.selectedWeddingPlannerWorkshopJobId=target.dataset.weddingWorkshopJob;
    renderWeddingPlanner();
  }
  else if(target.matches("[data-wedding-concept-package]")){
    state.selectedWeddingPlannerConceptPackageId=target.dataset.weddingConceptPackage;
    state.weddingPlannerSelectedConceptId=null;
    const select=$("#wedding-planner-workshop-package-version");
    if(select)select.value=state.selectedWeddingPlannerConceptPackageId;
    loadWeddingPlannerWorkshopDetails().then(() => {
      renderWeddingPlanner();
    });
  }
  else if(target.matches("[data-wedding-creative-job]")){
    state.selectedWeddingPlannerCreativeJobId=target.dataset.weddingCreativeJob;
    renderWeddingPlanner();
  }
  else if(target.matches("[data-wedding-creative-package]")){
    state.selectedWeddingPlannerCreativePackageId=target.dataset.weddingCreativePackage;
    state.weddingPlannerSelectedVariantId=null;
    const select=$("#wedding-planner-creative-package-version");
    if(select)select.value=state.selectedWeddingPlannerCreativePackageId;
    loadWeddingPlannerCreativeDetails().then(() => {
      renderWeddingPlanner();
    });
  }
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
  fillKey("#wedding-planner-color-compute-key");
  fillKey("#wedding-planner-color-decision-key");
  fillKey("#wedding-planner-research-job-key");
  fillKey("#wedding-planner-research-decision-key");
  fillKey("#wedding-planner-workshop-job-key");
  fillKey("#wedding-planner-workshop-decision-key");
  fillKey("#wedding-planner-creative-job-key");
  fillKey("#wedding-planner-creative-decision-key");
}

function renderWeddingPlanner() {
  const advertiserSelect = $("#wedding-planner-advertiser");
  const workspaceSelect = $("#wedding-planner-workspace");
  const sessionSelect = $("#wedding-planner-session");
  const interpretWorkspace = $("#wedding-planner-interpret-workspace");
  const colorWorkspace = $("#wedding-planner-color-workspace");
  const researchWorkspace = $("#wedding-planner-research-workspace");
  const workshopWorkspace = $("#wedding-planner-workshop-workspace");
  const creativeWorkspace = $("#wedding-planner-creative-workspace");
  const dnaVersionSelect = $("#wedding-planner-dna-version");
  const colorVersionSelect = $("#wedding-planner-color-version");
  const researchReportSelect = $("#wedding-planner-research-report-version");
  const workshopPackageSelect = $("#wedding-planner-workshop-package-version");
  const creativePackageSelect = $("#wedding-planner-creative-package-version");
  if (!advertiserSelect) return;
  advertiserSelect.innerHTML = state.advertisers.map(x => `<option value="${x.id}">${escapeHtml(x.name)}</option>`).join("");
  const workspaceOptions = state.weddingPlannerWorkspaces.map(x => `<option value="${x.workspaceId}">${escapeHtml(x.advertiserName)}</option>`).join("");
  workspaceSelect.innerHTML = workspaceOptions;
  if (interpretWorkspace) interpretWorkspace.innerHTML = workspaceOptions;
  if (colorWorkspace) colorWorkspace.innerHTML = workspaceOptions;
  if (researchWorkspace) researchWorkspace.innerHTML = workspaceOptions;
  if (workshopWorkspace) workshopWorkspace.innerHTML = workspaceOptions;
  if (creativeWorkspace) creativeWorkspace.innerHTML = workspaceOptions;
  if (state.selectedWeddingPlannerWorkspace) {
    workspaceSelect.value = state.selectedWeddingPlannerWorkspace;
    if (interpretWorkspace) interpretWorkspace.value = state.selectedWeddingPlannerWorkspace;
    if (colorWorkspace) colorWorkspace.value = state.selectedWeddingPlannerWorkspace;
    if (researchWorkspace) researchWorkspace.value = state.selectedWeddingPlannerWorkspace;
    if (workshopWorkspace) workshopWorkspace.value = state.selectedWeddingPlannerWorkspace;
    if (creativeWorkspace) creativeWorkspace.value = state.selectedWeddingPlannerWorkspace;
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
      ? state.weddingPlannerAgentRuns.map(x => `<div class="activity-item"><span class="activity-icon">◇</span><span><strong>${escapeHtml(x.logicalRole)} · ${escapeHtml(x.status)}</strong><small>${escapeHtml(x.promptPackVersion || "—")} · ${escapeHtml(x.workerProfileVersion || "—")} · ${escapeHtml(x.outcome || x.errorMessage || "no outcome")} · ${x.totalTokens ?? "—"} tokens · $${escapeHtml(formatUsd(x.estimatedCostUsd))}</small></span><span class="activity-time">${relativeTime(x.completedAt || x.startedAt)}</span></div>`).join("")
      : emptyState("Select a workspace to inspect Concierge, Brand DNA Interpreter, and Curator runs.");
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

  const colorVersions = state.weddingPlannerColorProfiles?.versions || [];
  const colorCurrentId = state.weddingPlannerColorProfiles?.currentApprovedColorProfileVersionId || null;
  const colorCurrentLabel = $("#wedding-planner-color-current");
  if (colorCurrentLabel) colorCurrentLabel.textContent = colorCurrentId ? `Current: ${shortId(colorCurrentId)}` : "Current: none";
  if (!state.selectedWeddingPlannerColorProfileId && colorVersions.length) {
    state.selectedWeddingPlannerColorProfileId =
      colorVersions.find(x => x.status === "PROPOSED")?.colorProfileVersionId ||
      colorCurrentId ||
      colorVersions[0].colorProfileVersionId;
  }
  if (colorVersionSelect) {
    colorVersionSelect.innerHTML = colorVersions.length
      ? colorVersions.map(x => {
          const markers = [x.status];
          if (x.colorProfileVersionId === colorCurrentId || x.isCurrentApproved) markers.push("CURRENT");
          return `<option value="${x.colorProfileVersionId}"${x.colorProfileVersionId === state.selectedWeddingPlannerColorProfileId ? " selected" : ""}>v${x.versionNumber} · ${escapeHtml(markers.join(" · "))}</option>`;
        }).join("")
      : `<option value="">No color profiles</option>`;
  }
  const colorList = $("#wedding-planner-color-profile-list");
  if (colorList) {
    colorList.innerHTML = colorVersions.length
      ? colorVersions.map(x => {
          const isCurrent = x.colorProfileVersionId === colorCurrentId || x.isCurrentApproved;
          return `<button class="activity-item" data-wedding-color-profile="${x.colorProfileVersionId}" type="button"><span class="activity-icon">${x.versionNumber}</span><span><strong>${escapeHtml(x.status)}${isCurrent ? " · CURRENT APPROVED" : ""}</strong><small>${escapeHtml(x.summary || "No summary")} · ${escapeHtml(x.schemaVersion)} · ${escapeHtml(x.algorithmVersion)} · SHA ${escapeHtml(shortId(x.inputSha256 || ""))} · Brand DNA ${escapeHtml(shortId(x.approvedBrandDnaVersionId || ""))}</small></span><span class="activity-time">${relativeTime(x.createdAt)}</span></button>`;
        }).join("")
      : emptyState("Select or create a workspace to list color profiles.");
  }
  renderWeddingPlannerColorInspect();

  const researchJobs = state.weddingPlannerResearchJobs || [];
  const jobCount = $("#wedding-planner-research-job-count");
  if (jobCount) jobCount.textContent = `${researchJobs.length} job${researchJobs.length === 1 ? "" : "s"}`;
  if (!state.selectedWeddingPlannerResearchJobId && researchJobs.length) {
    state.selectedWeddingPlannerResearchJobId = researchJobs[0].researchJobId;
  }
  const jobList = $("#wedding-planner-research-job-list");
  if (jobList) {
    jobList.innerHTML = researchJobs.length
      ? researchJobs.map(job => {
          const selected = job.researchJobId === state.selectedWeddingPlannerResearchJobId;
          const replayNote = job.isReplay && job.status === "FAILED"
            ? " · FAILED replay (not a retry)"
            : job.isReplay ? " · replay" : "";
          return `<button class="activity-item${selected ? " selected" : ""}" data-wedding-research-job="${job.researchJobId}" type="button"><span class="activity-icon">⌕</span><span><strong>${escapeHtml(job.status || "UNKNOWN")}${escapeHtml(replayNote)}</strong><small>${escapeHtml(job.topic || "")} · SHA ${escapeHtml(shortId(job.inputSha256 || ""))} · provider ${escapeHtml(job.researchProviderKey || "—")} · adapter ${escapeHtml(job.researchAdapterVersion || "—")} · cost $${escapeHtml(formatUsd(job.researchEstimatedCostUsd))}${job.errorMessage ? ` · error: ${escapeHtml(job.errorMessage)}` : ""}</small></span><span class="activity-time">${relativeTime(job.completedAt || job.startedAt)}</span></button>`;
        }).join("")
      : emptyState("Select or create a workspace to list Curator research jobs.");
  }

  const researchVersions = state.weddingPlannerResearchReports?.versions || [];
  const researchCurrentId = state.weddingPlannerResearchReports?.currentApprovedResearchReportVersionId || null;
  const researchCurrentLabel = $("#wedding-planner-research-current");
  if (researchCurrentLabel) researchCurrentLabel.textContent = researchCurrentId ? `Current: ${shortId(researchCurrentId)}` : "Current: none";
  if (!state.selectedWeddingPlannerResearchReportId && researchVersions.length) {
    state.selectedWeddingPlannerResearchReportId =
      researchVersions.find(x => x.status === "PROPOSED")?.researchReportVersionId ||
      researchCurrentId ||
      researchVersions[0].researchReportVersionId;
  }
  if (researchReportSelect) {
    researchReportSelect.innerHTML = researchVersions.length
      ? researchVersions.map(x => {
          const markers = [x.status];
          if (x.researchReportVersionId === researchCurrentId || x.isCurrentApproved) markers.push("CURRENT");
          return `<option value="${x.researchReportVersionId}"${x.researchReportVersionId === state.selectedWeddingPlannerResearchReportId ? " selected" : ""}>v${x.versionNumber} · ${escapeHtml(markers.join(" · "))}</option>`;
        }).join("")
      : `<option value="">No research reports</option>`;
  }
  const reportList = $("#wedding-planner-research-report-list");
  if (reportList) {
    reportList.innerHTML = researchVersions.length
      ? researchVersions.map(x => {
          const isCurrent = x.researchReportVersionId === researchCurrentId || x.isCurrentApproved;
          return `<button class="activity-item" data-wedding-research-report="${x.researchReportVersionId}" type="button"><span class="activity-icon">${x.versionNumber}</span><span><strong>${escapeHtml(x.status)}${isCurrent ? " · CURRENT APPROVED" : ""}</strong><small>${escapeHtml(x.summary || "No summary")} · ${escapeHtml(x.schemaVersion)} · cost $${escapeHtml(formatUsd(x.estimatedTotalCostUsd))} · Brand DNA ${escapeHtml(shortId(x.approvedBrandDnaVersionId || ""))}</small></span><span class="activity-time">${relativeTime(x.createdAt)}</span></button>`;
        }).join("")
      : emptyState("Select or create a workspace to list research reports.");
  }
  renderWeddingPlannerResearchInspect();

  const workshopJobs = state.weddingPlannerWorkshopJobs || [];
  const workshopJobCount = $("#wedding-planner-workshop-job-count");
  if (workshopJobCount) workshopJobCount.textContent = `${workshopJobs.length} job${workshopJobs.length === 1 ? "" : "s"}`;
  if (!state.selectedWeddingPlannerWorkshopJobId && workshopJobs.length) {
    state.selectedWeddingPlannerWorkshopJobId = workshopJobs[0].workshopJobId;
  }
  const workshopJobList = $("#wedding-planner-workshop-job-list");
  if (workshopJobList) {
    workshopJobList.innerHTML = workshopJobs.length
      ? workshopJobs.map(job => {
          const selected = job.workshopJobId === state.selectedWeddingPlannerWorkshopJobId;
          const replayNote = job.isReplay && job.status === "FAILED"
            ? " · FAILED replay (not a retry)"
            : job.isReplay ? " · replay" : "";
          return `<button class="activity-item${selected ? " selected" : ""}" data-wedding-workshop-job="${job.workshopJobId}" type="button"><span class="activity-icon">◇</span><span><strong>${escapeHtml(job.status || "UNKNOWN")}${escapeHtml(replayNote)}</strong><small>${escapeHtml(job.channelFormat || "")} · SHA ${escapeHtml(job.inputSha256 || "")} · Brand DNA ${escapeHtml(shortId(job.approvedBrandDnaVersionId || ""))} v${escapeHtml(String(job.approvedBrandDnaVersionNumber ?? "—"))} · Color ${escapeHtml(shortId(job.approvedColorProfileVersionId || ""))} v${escapeHtml(String(job.approvedColorProfileVersionNumber ?? "—"))} · Research ${escapeHtml(shortId(job.approvedResearchReportVersionId || ""))} v${escapeHtml(String(job.approvedResearchReportVersionNumber ?? "—"))}${job.errorMessage ? ` · error: ${escapeHtml(job.errorMessage)}` : ""}</small></span><span class="activity-time">${relativeTime(job.completedAt || job.startedAt)}</span></button>`;
        }).join("")
      : emptyState("Select or create a workspace to list Concept Workshop jobs.");
  }

  const workshopVersions = state.weddingPlannerConceptPackages?.versions || [];
  const workshopCurrentId = state.weddingPlannerConceptPackages?.currentApprovedConceptPackageVersionId || null;
  const workshopCurrentLabel = $("#wedding-planner-workshop-current");
  if (workshopCurrentLabel) workshopCurrentLabel.textContent = workshopCurrentId ? `Current: ${shortId(workshopCurrentId)}` : "Current: none";
  if (!state.selectedWeddingPlannerConceptPackageId && workshopVersions.length) {
    state.selectedWeddingPlannerConceptPackageId =
      workshopVersions.find(x => x.status === "PROPOSED")?.conceptPackageVersionId ||
      workshopCurrentId ||
      workshopVersions[0].conceptPackageVersionId;
  }
  if (workshopPackageSelect) {
    workshopPackageSelect.innerHTML = workshopVersions.length
      ? workshopVersions.map(x => {
          const markers = [x.status];
          if (x.conceptPackageVersionId === workshopCurrentId || x.isCurrentApproved) markers.push("CURRENT");
          return `<option value="${x.conceptPackageVersionId}"${x.conceptPackageVersionId === state.selectedWeddingPlannerConceptPackageId ? " selected" : ""}>v${x.versionNumber} · ${escapeHtml(markers.join(" · "))}</option>`;
        }).join("")
      : `<option value="">No concept packages</option>`;
  }
  const packageList = $("#wedding-planner-workshop-package-list");
  if (packageList) {
    packageList.innerHTML = workshopVersions.length
      ? workshopVersions.map(x => {
          const isCurrent = x.conceptPackageVersionId === workshopCurrentId || x.isCurrentApproved;
          return `<button class="activity-item" data-wedding-concept-package="${x.conceptPackageVersionId}" type="button"><span class="activity-icon">${x.versionNumber}</span><span><strong>${escapeHtml(x.status)}${isCurrent ? " · CURRENT APPROVED" : ""}</strong><small>${escapeHtml(x.summary || "No summary")} · ${escapeHtml(x.schemaVersion)} · ${escapeHtml(x.channelFormat || "")} · cost $${escapeHtml(formatUsd(x.estimatedTotalCostUsd))} · Brand DNA ${escapeHtml(shortId(x.approvedBrandDnaVersionId || ""))} · Color ${escapeHtml(shortId(x.approvedColorProfileVersionId || ""))} · Research ${escapeHtml(shortId(x.approvedResearchReportVersionId || ""))}</small></span><span class="activity-time">${relativeTime(x.createdAt)}</span></button>`;
        }).join("")
      : emptyState("Select or create a workspace to list concept packages.");
  }
  renderWeddingPlannerWorkshopInspect();

  const creativeJobs = state.weddingPlannerCreativeJobs || [];
  const creativeJobCount = $("#wedding-planner-creative-job-count");
  if (creativeJobCount) creativeJobCount.textContent = `${creativeJobs.length} job${creativeJobs.length === 1 ? "" : "s"}`;
  if (!state.selectedWeddingPlannerCreativeJobId && creativeJobs.length) {
    state.selectedWeddingPlannerCreativeJobId = creativeJobs[0].creativeProductionJobId;
  }
  const creativeJobList = $("#wedding-planner-creative-job-list");
  if (creativeJobList) {
    creativeJobList.innerHTML = creativeJobs.length
      ? creativeJobs.map(job => {
          const selected = job.creativeProductionJobId === state.selectedWeddingPlannerCreativeJobId;
          const replayNote = job.isReplay && job.status === "FAILED"
            ? " · failed replay (not a retry)"
            : job.isReplay ? " · replay" : "";
          const formats = Array.isArray(job.formats) ? job.formats.join(",") : "";
          return `<button class="activity-item${selected ? " selected" : ""}" data-wedding-creative-job="${job.creativeProductionJobId}" type="button"><span class="activity-icon">▣</span><span><strong>${escapeHtml(job.status || "UNKNOWN")}${escapeHtml(replayNote)} · ${escapeHtml(job.jobKind || "")}</strong><small>${escapeHtml(formats)} · variants ${escapeHtml(String(job.requestedVariantCount ?? "—"))} · SHA ${escapeHtml(job.inputSha256 || "")} · concept ${escapeHtml(job.selectedConceptId || "")} · DNA ${escapeHtml(shortId(job.approvedBrandDnaVersionId || ""))} · Color ${escapeHtml(shortId(job.approvedColorProfileVersionId || ""))} · Research ${escapeHtml(shortId(job.approvedResearchReportVersionId || ""))} · asset $${escapeHtml(formatUsd(job.assetProviderEstimatedCostUsd))}${job.errorMessage ? ` · error: ${escapeHtml(job.errorMessage)}` : ""}</small></span><span class="activity-time">${relativeTime(job.completedAt || job.startedAt)}</span></button>`;
        }).join("")
      : emptyState("Select or create a workspace to list creative-production jobs.");
  }

  const creativeVersions = state.weddingPlannerCreativePackages?.versions || [];
  const creativeCurrentId = state.weddingPlannerCreativePackages?.currentApprovedCreativePackageVersionId || null;
  const creativeCurrentLabel = $("#wedding-planner-creative-current");
  if (creativeCurrentLabel) creativeCurrentLabel.textContent = creativeCurrentId ? `Current: ${shortId(creativeCurrentId)}` : "Current: none";
  if (!state.selectedWeddingPlannerCreativePackageId && creativeVersions.length) {
    state.selectedWeddingPlannerCreativePackageId =
      creativeVersions.find(x => x.status === "PROPOSED")?.creativePackageVersionId ||
      creativeCurrentId ||
      creativeVersions[0].creativePackageVersionId;
  }
  if (creativePackageSelect) {
    creativePackageSelect.innerHTML = creativeVersions.length
      ? creativeVersions.map(x => {
          const markers = [x.status];
          if (x.creativePackageVersionId === creativeCurrentId || x.isCurrentApproved) markers.push("CURRENT");
          return `<option value="${x.creativePackageVersionId}"${x.creativePackageVersionId === state.selectedWeddingPlannerCreativePackageId ? " selected" : ""}>v${x.versionNumber} · ${escapeHtml(markers.join(" · "))}</option>`;
        }).join("")
      : `<option value="">No creative packages</option>`;
  }
  const revisionParent = $("#wedding-planner-creative-revision-parent");
  if (revisionParent) {
    revisionParent.innerHTML = creativeVersions.length
      ? `<option value="">Select parent</option>` + creativeVersions.map(x =>
          `<option value="${x.creativePackageVersionId}">v${x.versionNumber} · ${escapeHtml(x.status)} · ${escapeHtml(x.selectedConceptId || "")}</option>`
        ).join("")
      : `<option value="">No parent packages</option>`;
  }
  syncOpsCreativeRevisionFields();
  const creativePackageList = $("#wedding-planner-creative-package-list");
  if (creativePackageList) {
    creativePackageList.innerHTML = creativeVersions.length
      ? creativeVersions.map(x => {
          const isCurrent = x.creativePackageVersionId === creativeCurrentId || x.isCurrentApproved;
          return `<button class="activity-item" data-wedding-creative-package="${x.creativePackageVersionId}" type="button"><span class="activity-icon">${x.versionNumber}</span><span><strong>${escapeHtml(x.status)}${isCurrent ? " · CURRENT APPROVED" : ""}</strong><small>${escapeHtml(x.summary || "No summary")} · ${escapeHtml(x.schemaVersion)} · ${escapeHtml(x.jobKind || "")} · concept ${escapeHtml(x.selectedConceptId || "")} · AI+asset $${escapeHtml(formatUsd(x.estimatedTotalCostUsd))} · asset $${escapeHtml(formatUsd(x.estimatedAssetCostUsd))} · parent ${escapeHtml(shortId(x.parentCreativePackageVersionId || "—"))}</small></span><span class="activity-time">${relativeTime(x.createdAt)}</span></button>`;
        }).join("")
      : emptyState("Select or create a workspace to list creative packages.");
  }
  renderWeddingPlannerCreativeInspect();
  syncOpsCreativeVariantRadios();
}

function parseWeddingPlannerColorDocument(documentJson) {
  if (!documentJson) return null;
  try {
    return typeof documentJson === "string" ? JSON.parse(documentJson) : documentJson;
  } catch {
    return null;
  }
}

function seedProvenanceLabel(seeds, role) {
  if (!seeds) return "SERVER";
  if (role === "primary") return "HUMAN";
  if (role === "secondary") return seeds.secondaryDerived ? "DERIVED" : "HUMAN";
  if (role === "accent") return seeds.accentDerived ? "DERIVED" : "HUMAN";
  if (role === "background") return seeds.backgroundDefaulted ? "DEFAULT" : "HUMAN";
  if (role === "surface") return seeds.surfaceDefaulted ? "DEFAULT" : "HUMAN";
  return "SERVER";
}

function renderWeddingPlannerColorInspect() {
  const panel = $("#wedding-planner-color-inspect");
  if (!panel) return;
  const versions = state.weddingPlannerColorProfiles?.versions || [];
  const currentId = state.weddingPlannerColorProfiles?.currentApprovedColorProfileVersionId || null;
  const selected = versions.find(x => x.colorProfileVersionId === state.selectedWeddingPlannerColorProfileId) || versions[0] || null;
  if (!selected) {
    panel.innerHTML = emptyState("Select a color profile version to inspect the server document.");
    return;
  }
  const doc = parseWeddingPlannerColorDocument(selected.documentJson);
  const palette = doc?.palette || {};
  const seeds = doc?.seeds || null;
  const roles = ["primary", "secondary", "accent", "background", "surface", "onPrimary", "onSecondary", "onAccent", "onBackground", "onSurface", "neutral50", "neutral100", "neutral200", "neutral400", "neutral600", "neutral800", "neutral900"];
  const swatches = roles.map(role => {
    const hex = palette[role]?.hex;
    if (!hex) return "";
    const provenance = ["primary", "secondary", "accent", "background", "surface"].includes(role)
      ? ` · ${seedProvenanceLabel(seeds, role)}`
      : "";
    return `<div class="metric-box"><span>${escapeHtml(role)}${escapeHtml(provenance)}</span><strong style="display:flex;align-items:center;gap:8px"><span style="display:inline-block;width:18px;height:18px;border-radius:4px;border:1px solid rgba(0,0,0,.12);background:${escapeHtml(hex)}"></span>${escapeHtml(hex)}</strong></div>`;
  }).filter(Boolean).join("");
  const pairs = Array.isArray(doc?.contrastEvidence?.pairs) ? doc.contrastEvidence.pairs : [];
  const contrastRows = pairs.length
    ? pairs.map(pair => {
        const ratio = pair.ratio == null ? "—" : Number(pair.ratio).toFixed(2);
        const badges = [
          ["AA", pair.aaNormal],
          ["AA large", pair.aaLarge],
          ["AAA", pair.aaaNormal],
          ["AAA large", pair.aaaLarge]
        ].map(([label, pass]) => `<span class="status-badge ${pass ? "status-approved" : "status-ineligible"}">${escapeHtml(label)} ${pass ? "pass" : "fail"}</span>`).join(" ");
        return `<div class="activity-item"><span class="activity-icon" style="background:linear-gradient(90deg,${escapeHtml(pair.foregroundHex || "#000")},${escapeHtml(pair.backgroundHex || "#fff")})"></span><span><strong>${escapeHtml(pair.foregroundRole || "?")} on ${escapeHtml(pair.backgroundRole || "?")} · ${escapeHtml(ratio)}</strong><small>${escapeHtml(pair.foregroundHex || "")} / ${escapeHtml(pair.backgroundHex || "")}<br>${badges}</small></span></div>`;
      }).join("")
    : emptyState("No contrast evidence pairs in server documentJson.");
  const isCurrent = selected.colorProfileVersionId === currentId || selected.isCurrentApproved;
  panel.innerHTML = `
    <div class="detail-hero"><div class="detail-hero-top">${badge(selected.status)}${isCurrent ? badge("CURRENT") : ""}</div>
      <h3>Color profile v${escapeHtml(String(selected.versionNumber))}</h3>
      <p>${escapeHtml(selected.summary || "No summary")}</p>
    </div>
    <div class="metric-grid">
      ${metric("Schema", selected.schemaVersion || "—")}
      ${metric("Algorithm", selected.algorithmVersion || "—")}
      ${metric("Input SHA-256", selected.inputSha256 || "—")}
      ${metric("Brand DNA provenance", selected.approvedBrandDnaVersionId || "—")}
      ${metric("Current pointer", isCurrent ? "YES" : "NO")}
    </div>
    <div class="metric-grid">${swatches || emptyState("No palette roles in server documentJson.")}</div>
    <div class="safety-note">${escapeHtml(doc?.contrastEvidence?.disclaimer || "Contrast disclaimer missing from server document.")}</div>
    <div class="safety-note">${escapeHtml(doc?.geometryDisclaimer || "Geometry disclaimer missing from server document.")}</div>
    ${contrastRows}
  `;
}

const CURATOR_LOGICAL_ROLES = [
  "MARKET_LANDSCAPE_RESEARCHER",
  "AUDIENCE_CONTEXT_RESEARCHER",
  "COMPETITOR_SIGNALS_RESEARCHER",
  "CHANNEL_FORMAT_RESEARCHER",
  "EVIDENCE_ANALYST",
  "SOURCE_VERIFIER",
  "CLAIMS_RISK_REVIEWER",
  "RESEARCH_SYNTHESIZER"
];
const EIGHT_TO_THREE_EXPLANATION = "Eight logical roles map to 3 workers/runs, not 8 subscriptions";
const FOUR_TO_THREE_EXPLANATION = "Four logical roles map to 3 workers/runs, not 4 subscriptions";
const SYNTHETIC_DEVELOPMENT_PROTOTYPE = "SYNTHETIC DEVELOPMENT PROTOTYPE";
const CONCEPT_PACKAGE_DISCLAIMER =
  "Approval of this package is concept-direction approval only. It is not research, claim, legal, matching, accessibility, compliance, campaign-ready, asset, QA, or production-artwork approval. Marketing copy is CREATIVE_NON_FACTUAL unless a factual claim cites source IDs from the pinned approved research report. Brand DNA and Color Profile are creative constraints, not factual evidence. Prototypes are structured low-fi specs only; no images are generated.";
const WORKSHOP_LOGICAL_ROLES = ["BRAND_STRATEGIST", "ART_DIRECTOR", "COPYWRITER", "PRODUCTION_ARTIST"];
const WORKSHOP_WORKER_PROFILES = ["CONCEPT_STRATEGY_V1", "CONCEPT_CREATIVE_V1", "PROTOTYPE_PRODUCTION_V1"];
const ALLOWED_TEXT_REFS = ["copy.headline", "copy.body", "copy.cta"];
const ALLOWED_PROTOTYPE_TEMPLATES = ["LOFI_STACK_V1", "LOFI_SPLIT_V1", "LOFI_BANNER_V1"];
const ALLOWED_REGION_TYPES = ["HERO", "HEADER", "BODY", "HEADLINE", "SUBHEAD", "CTA", "FOOTER", "LOGO_SLOT"];
const ALLOWED_PLACEHOLDER_KINDS = ["HERO_IMAGE", "LOGO", "PRODUCT", "DECORATIVE"];

function parseWeddingPlannerResearchDocument(documentJson) {
  if (!documentJson) return null;
  try {
    return typeof documentJson === "string" ? JSON.parse(documentJson) : documentJson;
  } catch {
    return null;
  }
}

function formatUsd(value) {
  if (value == null || value === "") return "—";
  const number = Number(value);
  return Number.isFinite(number) ? number.toFixed(6) : String(value);
}

function renderSafeResearchLink(url) {
  const trimmed = String(url || "").trim();
  if (!/^https?:\/\//i.test(trimmed)) {
    return `<code>${escapeHtml(trimmed || "—")}</code>`;
  }
  try {
    const parsed = new URL(trimmed);
    if (parsed.protocol !== "http:" && parsed.protocol !== "https:") {
      return `<code>${escapeHtml(trimmed)}</code>`;
    }
  } catch {
    return `<code>${escapeHtml(trimmed)}</code>`;
  }
  return `<a href="${escapeHtml(trimmed)}" target="_blank" rel="noopener noreferrer nofollow">${escapeHtml(trimmed)}</a>`;
}

function renderWeddingPlannerResearchInspect() {
  const panel = $("#wedding-planner-research-inspect");
  if (!panel) return;
  const versions = state.weddingPlannerResearchReports?.versions || [];
  const currentId = state.weddingPlannerResearchReports?.currentApprovedResearchReportVersionId || null;
  const selected = versions.find(x => x.researchReportVersionId === state.selectedWeddingPlannerResearchReportId) || versions[0] || null;
  const selectedJob = (state.weddingPlannerResearchJobs || []).find(x => x.researchJobId === state.selectedWeddingPlannerResearchJobId) || null;

  if (!selected && !selectedJob) {
    panel.innerHTML = emptyState("Select a research job or report to inspect provider metadata, documentJson, and agent-run receipts.");
    return;
  }

  const jobBlock = selectedJob
    ? `<div class="detail-hero"><div class="detail-hero-top">${badge(selectedJob.status)}${selectedJob.isReplay ? badge("REPLAY") : ""}</div>
        <h3>Research job · ${escapeHtml(selectedJob.topic || "")}</h3>
        <p>${escapeHtml(selectedJob.objective || "")}</p>
      </div>
      <div class="metric-grid">
        ${metric("Input SHA-256", selectedJob.inputSha256 || "—")}
        ${metric("Provider", selectedJob.researchProviderKey || "—")}
        ${metric("Adapter", selectedJob.researchAdapterVersion || "—")}
        ${metric("Worker", selectedJob.researchWorkerKey || "—")}
        ${metric("Provider request", selectedJob.researchProviderRequestId || "—")}
        ${metric("Acquisition cost USD", formatUsd(selectedJob.researchEstimatedCostUsd))}
        ${metric("Brand DNA provenance", selectedJob.approvedBrandDnaVersionId || "—")}
        ${metric("Color provenance", selectedJob.approvedColorProfileVersionId || "optional/none")}
        ${metric("Error", selectedJob.errorCode || selectedJob.errorMessage ? `${selectedJob.errorCode || ""} ${selectedJob.errorMessage || ""}` : "—")}
      </div>
      ${selectedJob.isReplay && selectedJob.status === "FAILED"
        ? `<div class="safety-note"><strong>Failed replay is not a retry.</strong> The existing FAILED job was returned unchanged; providers and AI were not called again.</div>`
        : ""}`
    : "";

  if (!selected) {
    panel.innerHTML = jobBlock || emptyState("No research report selected.");
    return;
  }

  const doc = parseWeddingPlannerResearchDocument(selected.documentJson);
  const sources = Array.isArray(doc?.sources) ? doc.sources : [];
  const hasSynthetic = sources.some(source =>
    source?.synthetic === true ||
    /\.invalid\b/i.test(String(source?.url || "")) ||
    /SYNTHETIC/i.test(String(source?.title || "")) ||
    /SYNTHETIC/i.test(String(source?.publisher || ""))
  );
  const contributions = Array.isArray(doc?.contributions) ? doc.contributions : [];
  const ordered = CURATOR_LOGICAL_ROLES.map(role => contributions.find(item => item?.logicalRole === role)).filter(Boolean);
  const synthesis = doc?.synthesis || null;
  const provenance = doc?.provenance || null;
  const runs = state.weddingPlannerResearchAgentRuns || [];
  const isCurrent = selected.researchReportVersionId === currentId || selected.isCurrentApproved;

  const sourceRows = sources.length
    ? sources.map(source => {
        const synthetic = source?.synthetic === true;
        const invalidHost = /\.invalid\b/i.test(String(source?.url || ""));
        return `<div class="activity-item"><span class="activity-icon">${synthetic || invalidHost ? "⚠" : "▣"}</span><span><strong>${escapeHtml(source.id || "source")}${synthetic ? " · SYNTHETIC" : ""}${invalidHost ? " · .invalid" : ""}</strong><small>${escapeHtml(source.title || "")} · ${escapeHtml(source.publisher || "")}<br>${renderSafeResearchLink(source.url || "")}</small></span></div>`;
      }).join("")
    : emptyState("No sources in server documentJson.");

  const contributionRows = ordered.length === 8
    ? ordered.map(role => {
        const findings = Array.isArray(role.findings) ? role.findings : [];
        const findingText = findings.map(finding =>
          `${finding.type || "—"} (confidence ${finding.confidence ?? "—"}) · citations: ${(Array.isArray(finding.citationSourceIds) ? finding.citationSourceIds : []).join(", ") || "(none)"} · ${finding.statement || ""}`
        ).join(" | ");
        return `<div class="activity-item"><span class="activity-icon">◎</span><span><strong>${escapeHtml(role.logicalRole || "")}</strong><small>${escapeHtml(role.summary || "")}<br>${escapeHtml(findingText || "No findings in documentJson.")}</small></span></div>`;
      }).join("")
    : `<div class="safety-note">Expected exactly 8 role contributions in documentJson; found ${ordered.length} recognized roles (total ${contributions.length}). No client-generated findings or citations are invented.</div>`;

  const runRows = runs.length
    ? runs.map(run => `<div class="activity-item"><span class="activity-icon">◇</span><span><strong>${escapeHtml(run.logicalRole || "—")} · ${escapeHtml(run.status || "—")}</strong><small>profile ${escapeHtml(run.workerProfileVersion || "—")} · prompt ${escapeHtml(run.promptPackVersion || "—")} · assigned ${escapeHtml(run.assignedRolesJson || "—")}<br>tokens ${escapeHtml(String(run.promptTokens ?? "—"))}/${escapeHtml(String(run.completionTokens ?? "—"))}/${escapeHtml(String(run.totalTokens ?? "—"))} · cost $${escapeHtml(formatUsd(run.estimatedCostUsd))}</small></span><span class="activity-time">${relativeTime(run.completedAt || run.startedAt)}</span></div>`).join("")
    : emptyState("No agent-run receipts from the report endpoint.");

  panel.innerHTML = `
    ${jobBlock}
    <div class="detail-hero"><div class="detail-hero-top">${badge(selected.status)}${isCurrent ? badge("CURRENT") : ""}</div>
      <h3>Research report v${escapeHtml(String(selected.versionNumber))}</h3>
      <p>${escapeHtml(selected.summary || "No summary")}</p>
    </div>
    <div class="metric-grid">
      ${metric("Schema", selected.schemaVersion || "—")}
      ${metric("Estimated total cost USD", formatUsd(selected.estimatedTotalCostUsd))}
      ${metric("Brand DNA provenance", selected.approvedBrandDnaVersionId || "—")}
      ${metric("Color provenance", selected.approvedColorProfileVersionId || "optional/none")}
      ${metric("Current pointer", isCurrent ? "YES" : "NO")}
      ${metric("Producing job", selected.producingResearchJobId || "—")}
    </div>
    <div class="safety-note"><strong>Research disclaimer (from documentJson).</strong> ${escapeHtml(doc?.disclaimer || "Disclaimer missing from server documentJson.")}</div>
    ${hasSynthetic ? `<div class="safety-note curator-synthetic-warning"><strong>SYNTHETIC local evidence.</strong> Catalog uses .invalid hosts and/or SYNTHETIC markers. Fixture research only — never live research.</div>` : ""}
    <div class="safety-note">${escapeHtml(EIGHT_TO_THREE_EXPLANATION)}. Showing ${runs.length} receipt(s) from /research-reports/{id}/agent-runs.</div>
    <div class="metric-grid">
      ${metric("Provenance Brand DNA", provenance?.approvedBrandDnaVersionId || "—")}
      ${metric("Provenance Brand DNA #", String(provenance?.approvedBrandDnaVersionNumber ?? "—"))}
      ${metric("Provenance color", provenance?.approvedColorProfileVersionId || "optional/none")}
      ${metric("Provenance job", provenance?.researchJobId || "—")}
    </div>
    <h3>Sources</h3>
    ${sourceRows}
    <h3>Contributions (exactly 8)</h3>
    ${contributionRows}
    <h3>Synthesis</h3>
    ${synthesis
      ? `<div class="activity-item"><span class="activity-icon">✦</span><span><strong>Executive summary</strong><small>${escapeHtml(synthesis.executiveSummary || "")}<br>Open questions: ${escapeHtml((Array.isArray(synthesis.openQuestions) ? synthesis.openQuestions : []).join(" · ") || "(none)")}<br>Risks: ${escapeHtml((Array.isArray(synthesis.risks) ? synthesis.risks : []).join(" · ") || "(none)")}</small></span></div>`
      : emptyState("No synthesis object in server documentJson.")}
    <h3>Agent run receipts (exactly 3 workers)</h3>
    ${runRows}
  `;
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
    state.weddingPlannerColorProfiles = await api(`/api/wedding-planner/workspaces/${workspaceId}/color-profiles`);
    state.selectedWeddingPlannerColorProfileId =
      state.weddingPlannerColorProfiles?.versions?.find(x => x.status === "PROPOSED")?.colorProfileVersionId ||
      state.weddingPlannerColorProfiles?.currentApprovedColorProfileVersionId ||
      state.weddingPlannerColorProfiles?.versions?.[0]?.colorProfileVersionId ||
      null;
    state.weddingPlannerResearchJobs = await api(`/api/wedding-planner/workspaces/${workspaceId}/research-jobs`);
    state.weddingPlannerResearchReports = await api(`/api/wedding-planner/workspaces/${workspaceId}/research-reports`);
    state.selectedWeddingPlannerResearchJobId = state.weddingPlannerResearchJobs?.[0]?.researchJobId || null;
    state.selectedWeddingPlannerResearchReportId =
      state.weddingPlannerResearchReports?.versions?.find(x => x.status === "PROPOSED")?.researchReportVersionId ||
      state.weddingPlannerResearchReports?.currentApprovedResearchReportVersionId ||
      state.weddingPlannerResearchReports?.versions?.[0]?.researchReportVersionId ||
      null;
    state.weddingPlannerWorkshopJobs = await api(`/api/wedding-planner/workspaces/${workspaceId}/workshop-jobs`);
    state.weddingPlannerConceptPackages = await api(`/api/wedding-planner/workspaces/${workspaceId}/concept-packages`);
    state.selectedWeddingPlannerWorkshopJobId = state.weddingPlannerWorkshopJobs?.[0]?.workshopJobId || null;
    state.selectedWeddingPlannerConceptPackageId =
      state.weddingPlannerConceptPackages?.versions?.find(x => x.status === "PROPOSED")?.conceptPackageVersionId ||
      state.weddingPlannerConceptPackages?.currentApprovedConceptPackageVersionId ||
      state.weddingPlannerConceptPackages?.versions?.[0]?.conceptPackageVersionId ||
      null;
    state.weddingPlannerSelectedConceptId = null;
    state.weddingPlannerCreativeJobs = await api(`/api/wedding-planner/workspaces/${workspaceId}/creative-production-jobs`);
    state.weddingPlannerCreativePackages = await api(`/api/wedding-planner/workspaces/${workspaceId}/creative-packages`);
    state.selectedWeddingPlannerCreativeJobId = state.weddingPlannerCreativeJobs?.[0]?.creativeProductionJobId || null;
    state.selectedWeddingPlannerCreativePackageId =
      state.weddingPlannerCreativePackages?.versions?.find(x => x.status === "PROPOSED")?.creativePackageVersionId ||
      state.weddingPlannerCreativePackages?.currentApprovedCreativePackageVersionId ||
      state.weddingPlannerCreativePackages?.versions?.[0]?.creativePackageVersionId ||
      null;
    state.weddingPlannerSelectedVariantId = null;
    await mergeWeddingPlannerInterpreterRuns();
    await mergeWeddingPlannerWorkspaceRuns(workspaceId);
    await loadWeddingPlannerResearchAgentRuns();
    await loadWeddingPlannerWorkshopDetails();
    await loadWeddingPlannerCreativeDetails();
  } catch (error) {
    toast(error.message, true);
    state.weddingPlannerSessions = [];
    state.weddingPlannerMessages = [];
    state.weddingPlannerAgentRuns = [];
    state.weddingPlannerBrandDna = null;
    state.weddingPlannerColorProfiles = null;
    state.selectedWeddingPlannerColorProfileId = null;
    state.weddingPlannerResearchJobs = [];
    state.weddingPlannerResearchReports = null;
    state.selectedWeddingPlannerResearchJobId = null;
    state.selectedWeddingPlannerResearchReportId = null;
    state.weddingPlannerResearchAgentRuns = [];
    state.weddingPlannerWorkshopJobs = [];
    state.weddingPlannerConceptPackages = null;
    state.selectedWeddingPlannerWorkshopJobId = null;
    state.selectedWeddingPlannerConceptPackageId = null;
    state.weddingPlannerWorkshopContributions = [];
    state.weddingPlannerWorkshopAgentRuns = [];
    state.weddingPlannerWorkshopPinnedColorDocument = null;
    state.weddingPlannerSelectedConceptId = null;
    state.weddingPlannerCreativeJobs = [];
    state.weddingPlannerCreativePackages = null;
    state.selectedWeddingPlannerCreativeJobId = null;
    state.selectedWeddingPlannerCreativePackageId = null;
    state.weddingPlannerCreativeContributions = [];
    state.weddingPlannerCreativeAssets = [];
    state.weddingPlannerCreativeAgentRuns = [];
    state.weddingPlannerSelectedVariantId = null;
  }
  renderWeddingPlanner();
}

async function mergeWeddingPlannerInterpreterRuns() {
  const versions = state.weddingPlannerBrandDna?.versions || [];
  const runIds = [...new Set(versions.map(x => x.producingAgentRunId).filter(Boolean))];
  const interpreterRuns = await Promise.all(
    runIds.map(id => api(`/api/wedding-planner/agent-runs/${id}`).catch(() => null))
  );
  const combined = [...state.weddingPlannerAgentRuns, ...interpreterRuns.filter(Boolean)];
  state.weddingPlannerAgentRuns = [
    ...new Map(combined.map(run => [run.agentRunId, run])).values()
  ].sort((a, b) => new Date(a.startedAt) - new Date(b.startedAt));
}

async function mergeWeddingPlannerWorkspaceRuns(workspaceId) {
  try {
    const workspaceRuns = await api(`/api/wedding-planner/workspaces/${workspaceId}/agent-runs`);
    if (!Array.isArray(workspaceRuns)) return;
    const combined = [...state.weddingPlannerAgentRuns, ...workspaceRuns];
    state.weddingPlannerAgentRuns = [
      ...new Map(combined.map(run => [run.agentRunId, run])).values()
    ].sort((a, b) => new Date(a.startedAt) - new Date(b.startedAt));
  } catch {
    /* Workspace agent-runs are additive; session/interpreter runs remain usable. */
  }
}

async function loadWeddingPlannerResearchAgentRuns() {
  const reportId = state.selectedWeddingPlannerResearchReportId;
  if (!reportId) {
    state.weddingPlannerResearchAgentRuns = [];
    return;
  }
  try {
    const runs = await api(`/api/wedding-planner/research-reports/${reportId}/agent-runs`);
    state.weddingPlannerResearchAgentRuns = Array.isArray(runs) ? runs : [];
  } catch {
    state.weddingPlannerResearchAgentRuns = [];
  }
}

async function loadWeddingPlannerWorkshopDetails() {
  const packageId = state.selectedWeddingPlannerConceptPackageId;
  if (!packageId) {
    state.weddingPlannerWorkshopContributions = [];
    state.weddingPlannerWorkshopAgentRuns = [];
    state.weddingPlannerWorkshopPinnedColorDocument = null;
    return;
  }
  try {
    const [contributions, runs, packageDto] = await Promise.all([
      api(`/api/wedding-planner/concept-packages/${packageId}/contributions`),
      api(`/api/wedding-planner/concept-packages/${packageId}/agent-runs`),
      api(`/api/wedding-planner/concept-packages/${packageId}`)
    ]);
    state.weddingPlannerWorkshopContributions = Array.isArray(contributions) ? contributions : [];
    state.weddingPlannerWorkshopAgentRuns = Array.isArray(runs) ? runs : [];
    const versions = state.weddingPlannerConceptPackages?.versions || [];
    const idx = versions.findIndex(x => x.conceptPackageVersionId === packageId);
    if (idx >= 0 && packageDto) versions[idx] = packageDto;
    if (packageDto?.approvedColorProfileVersionId) {
      try {
        const colorVersion = await api(`/api/wedding-planner/color-profiles/${packageDto.approvedColorProfileVersionId}`);
        state.weddingPlannerWorkshopPinnedColorDocument = parseWeddingPlannerColorDocument(colorVersion?.documentJson);
      } catch {
        state.weddingPlannerWorkshopPinnedColorDocument = null;
      }
    } else {
      state.weddingPlannerWorkshopPinnedColorDocument = null;
    }
  } catch {
    state.weddingPlannerWorkshopContributions = [];
    state.weddingPlannerWorkshopAgentRuns = [];
    state.weddingPlannerWorkshopPinnedColorDocument = null;
  }
}

function parseWeddingPlannerConceptPackageDocument(documentJson) {
  if (!documentJson) return null;
  try {
    return typeof documentJson === "string" ? JSON.parse(documentJson) : documentJson;
  } catch {
    return null;
  }
}

function sanitizeWorkshopBounds(bounds, canvasW, canvasH) {
  if (!bounds || typeof bounds !== "object") return null;
  const x = Number(bounds.x);
  const y = Number(bounds.y);
  const w = Number(bounds.w);
  const h = Number(bounds.h);
  if (![x, y, w, h].every(Number.isFinite)) return null;
  if (x < 0 || y < 0 || w <= 0 || h <= 0) return null;
  if (x + w > canvasW + 0.0001 || y + h > canvasH + 0.0001) return null;
  return { x, y, w, h };
}

function resolveWorkshopPaletteHex(palette, roleRef) {
  if (!roleRef) return null;
  const entry = palette?.[String(roleRef)];
  const hex = entry?.hex || (typeof entry === "string" ? entry : null);
  if (!hex || !/^#[0-9A-Fa-f]{6}$/.test(String(hex))) return null;
  return String(hex).toUpperCase();
}

function renderOpsSafePrototypePreview(concept, colorDocument) {
  const prototype = concept?.prototype;
  if (!prototype || typeof prototype !== "object") {
    return emptyState("No prototype-spec.v1 in server documentJson for this concept.");
  }
  const template = String(prototype.template || "");
  if (!ALLOWED_PROTOTYPE_TEMPLATES.includes(template)) {
    return emptyState("Unsupported or missing prototype template. Safe renderer fails closed.");
  }
  const canvas = prototype.canvas || {};
  const canvasW = Number(canvas.width);
  const canvasH = Number(canvas.height);
  if (!Number.isFinite(canvasW) || !Number.isFinite(canvasH) || canvasW <= 0 || canvasH <= 0) {
    return emptyState("Invalid canvas bounds in prototype-spec. Fail closed.");
  }
  const regions = Array.isArray(prototype.regions) ? prototype.regions : [];
  if (regions.length < 2 || regions.length > 12) {
    return emptyState("Prototype regions must be 2–12 items from server JSON. Fail closed.");
  }
  const maxDisplay = 320;
  const scale = Math.min(1, maxDisplay / canvasW);
  const displayW = Math.round(canvasW * scale);
  const displayH = Math.round(canvasH * scale);
  const palette = colorDocument?.palette || {};
  const copy = concept.copy || {};
  const regionHtml = regions.map(region => {
    const bounds = sanitizeWorkshopBounds(region?.bounds, canvasW, canvasH);
    if (!bounds) return "";
    const type = String(region.type || "");
    if (!ALLOWED_REGION_TYPES.includes(type)) return "";
    const left = Math.round(bounds.x * scale);
    const top = Math.round(bounds.y * scale);
    const width = Math.max(1, Math.round(bounds.w * scale));
    const height = Math.max(1, Math.round(bounds.h * scale));
    const color = resolveWorkshopPaletteHex(palette, region.paletteRoleRef);
    const styleParts = [`left:${left}px`, `top:${top}px`, `width:${width}px`, `height:${height}px`];
    if (color) styleParts.push(`background:${color}`);
    if (region.assetPlaceholder) {
      const kind = String(region.assetPlaceholder.kind || "");
      const label = String(region.assetPlaceholder.label || "Placeholder");
      if (!ALLOWED_PLACEHOLDER_KINDS.includes(kind)) {
        return `<div class="workshop-prototype-region" style="${styleParts.join(";")}"><div class="workshop-placeholder">INVALID PLACEHOLDER</div></div>`;
      }
      return `<div class="workshop-prototype-region" style="${styleParts.join(";")}"><div class="workshop-placeholder">${escapeHtml(kind)} · ${escapeHtml(label)}</div></div>`;
    }
    const textRef = region.textRef ? String(region.textRef) : "";
    let text = "";
    if (textRef) {
      if (!ALLOWED_TEXT_REFS.includes(textRef)) {
        return `<div class="workshop-prototype-region" style="${styleParts.join(";")}"><div class="workshop-placeholder">INVALID TEXT REF</div></div>`;
      }
      if (textRef === "copy.headline") text = copy.headline || "";
      else if (textRef === "copy.body") text = copy.body || "";
      else if (textRef === "copy.cta") text = copy.cta || "";
    }
    return `<div class="workshop-prototype-region" style="${styleParts.join(";")}" data-template="${escapeHtml(template)}">${escapeHtml(text)}</div>`;
  }).filter(Boolean).join("");
  return `<div class="workshop-prototype-frame" data-template="${escapeHtml(template)}" style="width:${displayW}px;height:${displayH}px;max-width:100%;position:relative;border:1px dashed #9bb4cc;background:#f0f4f8;overflow:hidden;" role="img" aria-label="Low-fi prototype preview placeholder frame">${regionHtml || `<div class="workshop-placeholder">No valid regions</div>`}</div><div class="safety-note">${escapeHtml(SYNTHETIC_DEVELOPMENT_PROTOTYPE)} · placeholders only · no generated image/assets · concept direction only</div>`;
}

function renderWeddingPlannerWorkshopInspect() {
  const panel = $("#wedding-planner-workshop-inspect");
  if (!panel) return;
  const versions = state.weddingPlannerConceptPackages?.versions || [];
  const currentId = state.weddingPlannerConceptPackages?.currentApprovedConceptPackageVersionId || null;
  const selected = versions.find(x => x.conceptPackageVersionId === state.selectedWeddingPlannerConceptPackageId) || versions[0] || null;
  const selectedJob = (state.weddingPlannerWorkshopJobs || []).find(x => x.workshopJobId === state.selectedWeddingPlannerWorkshopJobId) || null;
  if (!selected && !selectedJob) {
    panel.innerHTML = emptyState("Select a workshop job or concept package to inspect input SHA, provenance, documentJson, contributions, and agent-run receipts.");
    return;
  }

  const jobBlock = selectedJob ? `
    <div class="metric-grid">
      ${metric("Job status", selectedJob.status || "—")}
      ${metric("Input SHA-256", selectedJob.inputSha256 || "—")}
      ${metric("Channel", selectedJob.channelFormat || "—")}
      ${metric("Canvas", `${selectedJob.canvasWidth ?? "—"}×${selectedJob.canvasHeight ?? "—"}`)}
      ${metric("Brand DNA", `${shortId(selectedJob.approvedBrandDnaVersionId)} v${selectedJob.approvedBrandDnaVersionNumber ?? "—"}`)}
      ${metric("Color profile", `${shortId(selectedJob.approvedColorProfileVersionId)} v${selectedJob.approvedColorProfileVersionNumber ?? "—"}`)}
      ${metric("Research report", `${shortId(selectedJob.approvedResearchReportVersionId)} v${selectedJob.approvedResearchReportVersionNumber ?? "—"}`)}
      ${metric("Package", selectedJob.outputConceptPackageVersionId || "—")}
      ${metric("Replay", selectedJob.isReplay ? "YES" : "NO")}
    </div>
    <div class="safety-note"><strong>Brief.</strong> Objective: ${escapeHtml(selectedJob.objective || "—")}<br>Campaign goal (planning text): ${escapeHtml(selectedJob.campaignGoal || "—")}<br>Audience: ${escapeHtml(selectedJob.audienceFocus || "—")}<br>CTA: ${escapeHtml(selectedJob.cta || "—")}</div>
  ` : "";

  if (!selected) {
    panel.innerHTML = jobBlock || emptyState("No concept package selected.");
    return;
  }

  const doc = parseWeddingPlannerConceptPackageDocument(selected.documentJson);
  const concepts = Array.isArray(doc?.concepts) ? doc.concepts : [];
  const contributions = state.weddingPlannerWorkshopContributions || [];
  const runs = state.weddingPlannerWorkshopAgentRuns || [];
  const isCurrent = selected.conceptPackageVersionId === currentId || selected.isCurrentApproved;
  const hasSynthetic =
    String(doc?.marker || "") === SYNTHETIC_DEVELOPMENT_PROTOTYPE ||
    JSON.stringify(doc || {}).includes(SYNTHETIC_DEVELOPMENT_PROTOTYPE);
  const selectedConceptNote = state.weddingPlannerSelectedConceptId
    ? metric("Selected concept (decision)", state.weddingPlannerSelectedConceptId)
    : metric("Selected concept (decision)", "— (shown when returned by decision)");

  const conceptRows = concepts.length === 3
    ? concepts.map(concept => {
        const copy = concept.copy || {};
        const claims = Array.isArray(concept.factualClaims) ? concept.factualClaims : [];
        const preview = renderOpsSafePrototypePreview(concept, state.weddingPlannerWorkshopPinnedColorDocument);
        const conceptNumber = String(concept.id || "").match(/^concept_(\d+)$/)?.[1];
        return `<div class="activity-item"><span class="activity-icon">${escapeHtml(conceptNumber ? `C${conceptNumber}` : "?")}</span><span><strong>${escapeHtml(concept.id || "—")} · ${escapeHtml(concept.name || "—")}</strong><small>Rationale: ${escapeHtml(concept.rationale || "—")}<br>Visual: ${escapeHtml(concept.visualDirection || "—")}<br>Palette refs: ${escapeHtml((concept.paletteRoleRefs || []).join(", ") || "—")}<br>Copy kind ${escapeHtml(copy.kind || "—")} · H: ${escapeHtml(copy.headline || "—")} · B: ${escapeHtml(copy.body || "—")} · CTA: ${escapeHtml(copy.cta || "—")}<br>Claims: ${claims.length ? claims.map(c => `${escapeHtml(c.statement || "")} [${escapeHtml((c.sourceIds || []).join(", "))}]`).join(" · ") : "none"}</small>${preview}</span></div>`;
      }).join("")
    : emptyState(`Expected exactly 3 concepts in server documentJson; found ${concepts.length}. No client concept invention.`);

  const contributionRows = WORKSHOP_LOGICAL_ROLES.map(role => {
    const apiRow = contributions.find(x => x.logicalRole === role);
    const docRow = (Array.isArray(doc?.contributions) ? doc.contributions : []).find(x => x.logicalRole === role);
    let summary = docRow?.summary || "";
    if (apiRow?.contributionJson) {
      try {
        const parsed = typeof apiRow.contributionJson === "string" ? JSON.parse(apiRow.contributionJson) : apiRow.contributionJson;
        if (parsed?.summary) summary = parsed.summary;
      } catch { /* keep */ }
    }
    return `<div class="activity-item"><span class="activity-icon">◇</span><span><strong>${escapeHtml(role)}</strong><small>${escapeHtml(summary || "—")}<br>Producing run ${escapeHtml(apiRow?.producingAgentRunId || "—")}</small></span></div>`;
  }).join("");

  const runRows = runs.length
    ? runs.map(run => `<div class="activity-item"><span class="activity-icon">◇</span><span><strong>${escapeHtml(run.logicalRole || "—")} · ${escapeHtml(run.status || "—")}</strong><small>profile ${escapeHtml(run.workerProfileVersion || "—")} · prompt ${escapeHtml(run.promptPackVersion || "—")} · assigned ${escapeHtml(run.assignedRolesJson || "—")}<br>provider ${escapeHtml(run.providerKey || "—")} · model ${escapeHtml(run.modelId || "—")} · tokens ${escapeHtml(String(run.totalTokens ?? "—"))} · cost $${escapeHtml(formatUsd(run.estimatedCostUsd))}</small></span><span class="activity-time">${relativeTime(run.completedAt || run.startedAt)}</span></div>`).join("")
    : emptyState("No agent-run receipts for this package.");

  panel.innerHTML = `
    ${jobBlock}
    <div class="detail-hero"><div class="detail-hero-top">${badge(selected.status)}${isCurrent ? badge("CURRENT") : ""}${hasSynthetic ? badge(SYNTHETIC_DEVELOPMENT_PROTOTYPE) : ""}</div>
      <h3>Concept package v${escapeHtml(String(selected.versionNumber))}</h3>
      <p>${escapeHtml(selected.summary || "No summary")}</p>
    </div>
    <div class="metric-grid">
      ${metric("Schema", selected.schemaVersion || "—")}
      ${metric("Channel", selected.channelFormat || "—")}
      ${metric("Canvas", `${selected.canvasWidth ?? "—"}×${selected.canvasHeight ?? "—"}`)}
      ${metric("Estimated total cost USD", formatUsd(selected.estimatedTotalCostUsd))}
      ${metric("Brand DNA provenance", `${shortId(selected.approvedBrandDnaVersionId)} v${selected.approvedBrandDnaVersionNumber ?? "—"}`)}
      ${metric("Color provenance", `${shortId(selected.approvedColorProfileVersionId)} v${selected.approvedColorProfileVersionNumber ?? "—"}`)}
      ${metric("Research provenance", `${shortId(selected.approvedResearchReportVersionId)} v${selected.approvedResearchReportVersionNumber ?? "—"}`)}
      ${metric("Current pointer", isCurrent ? "YES" : "NO")}
      ${metric("Producing job", selected.producingWorkshopJobId || "—")}
      ${selectedConceptNote}
    </div>
    <div class="safety-note"><strong>Concept-direction disclaimer (from documentJson).</strong> ${escapeHtml(doc?.disclaimer || CONCEPT_PACKAGE_DISCLAIMER)}</div>
    ${hasSynthetic ? `<div class="safety-note curator-synthetic-warning"><strong>${escapeHtml(SYNTHETIC_DEVELOPMENT_PROTOTYPE)}</strong> Concept direction only — no generated image/assets; not campaign-ready, QA, matching, or legal/claim approval.</div>` : ""}
    <div class="safety-note">${escapeHtml(FOUR_TO_THREE_EXPLANATION)}. Profiles ${escapeHtml(WORKSHOP_WORKER_PROFILES.join(", "))}. Showing ${runs.length} receipt(s) from /concept-packages/{id}/agent-runs — exactly 3 workers expected.</div>
    <h3>Concepts (exactly 3)</h3>
    ${conceptRows}
    <h3>Contributions (exactly 4)</h3>
    ${contributionRows}
    <h3>Agent run receipts (exactly 3 workers)</h3>
    ${runRows}
  `;
}

async function submitWeddingPlannerWorkshopJob(form) {
  const value = name => form.elements[name].value.trim();
  const deliverables = linesToList(form.elements.deliverables?.value, 1, 6);
  const constraints = linesToList(form.elements.constraints?.value, 0, 12);
  if (!deliverables) {
    toast("Provide 1–6 non-empty deliverables, one per line.", true);
    return;
  }
  if (!constraints && String(form.elements.constraints?.value || "").trim()) {
    toast("Constraints must be 0–12 non-empty lines.", true);
    return;
  }
  try {
    const workspaceId = value("workspaceId");
    const result = await api(`/api/wedding-planner/workspaces/${workspaceId}/workshop-jobs`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        objective: value("objective"),
        campaignGoal: value("campaignGoal"),
        audienceFocus: value("audienceFocus"),
        channelFormat: value("channelFormat"),
        deliverables,
        cta: value("cta"),
        constraints: constraints || [],
        sourceSystem: value("sourceSystem"),
        idempotencyKey: value("idempotencyKey")
      })
    });
    const replayNote = result.isReplay && result.status === "FAILED"
      ? " (failed replay — not a retry)"
      : result.isReplay ? " (replay)" : "";
    toast(`Workshop job ${result.status}${replayNote}`);
    fillKey("#wedding-planner-workshop-job-key");
    state.selectedWeddingPlannerWorkspace = workspaceId;
    state.selectedWeddingPlannerWorkshopJobId = result.workshopJobId;
    if (result.outputConceptPackageVersionId) {
      state.selectedWeddingPlannerConceptPackageId = result.outputConceptPackageVersionId;
    }
    state.weddingPlannerWorkshopJobs = await api(`/api/wedding-planner/workspaces/${workspaceId}/workshop-jobs`);
    state.weddingPlannerConceptPackages = await api(`/api/wedding-planner/workspaces/${workspaceId}/concept-packages`);
    await mergeWeddingPlannerWorkspaceRuns(workspaceId);
    await loadWeddingPlannerWorkshopDetails();
    renderWeddingPlanner();
  } catch (error) {
    toast(error.message, true);
  }
}

async function submitWeddingPlannerWorkshopDecision(form) {
  const value = name => form.elements[name].value.trim();
  const decision = value("decision");
  if (decision === "APPROVE" && form.elements.confirmApprove?.checked !== true) {
    toast("Confirm the APPROVE checkbox before approving a concept package.", true);
    return;
  }
  const selectedRadio = form.querySelector('input[name="selectedConceptId"]:checked');
  const selectedConceptId = selectedRadio ? String(selectedRadio.value || "").trim() : "";
  if (decision === "APPROVE") {
    if (!selectedConceptId || !["concept_1", "concept_2", "concept_3"].includes(selectedConceptId)) {
      toast("APPROVE requires a radio-selected concept id (concept_1, concept_2, or concept_3).", true);
      return;
    }
  }
  if (decision === "REJECT" && selectedConceptId) {
    toast("REJECT forbids concept selection. Clear the selected concept before rejecting.", true);
    return;
  }
  const payload = {
    decision,
    rationale: value("rationale"),
    sourceSystem: value("sourceSystem"),
    idempotencyKey: value("idempotencyKey"),
    selectedConceptId: decision === "APPROVE" ? selectedConceptId : null
  };
  try {
    const result = await api(`/api/wedding-planner/concept-packages/${value("conceptPackageVersionId")}/decisions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    toast(`Concept package ${friendlyStatus(result.decision)} recorded${result.isReplay ? " (replay)" : ""} — concept-direction approval only`);
    form.elements.rationale.value = "";
    if (form.elements.confirmApprove) form.elements.confirmApprove.checked = false;
    form.querySelectorAll('input[name="selectedConceptId"]').forEach(input => { input.checked = false; });
    fillKey("#wedding-planner-workshop-decision-key");
    state.weddingPlannerSelectedConceptId = result.selectedConceptId || null;
    const workspaceId = result.workspaceId || state.selectedWeddingPlannerWorkspace;
    if (workspaceId) {
      state.weddingPlannerConceptPackages = await api(`/api/wedding-planner/workspaces/${workspaceId}/concept-packages`);
      state.selectedWeddingPlannerConceptPackageId = result.conceptPackageVersionId || state.selectedWeddingPlannerConceptPackageId;
      await loadWeddingPlannerWorkshopDetails();
    }
    renderWeddingPlanner();
  } catch (error) {
    toast(error.message, true);
  }
}

const CREATIVE_PACKAGE_DISCLAIMER_OPS =
  "Approval of this package is draft creative approval only. It is not research, claim, legal, matching, accessibility, compliance, campaign-ready, QA, or final production-artwork approval. It does not authorize Bliss matching or placement. Marketing copy is CREATIVE_NON_FACTUAL unless a factual claim exactly preserves a cited claim from the pinned selected concept using source IDs from the pinned approved research report. Brand DNA and Color Profile are creative constraints, not factual evidence. Draft PNG assets are provider-generated renditions for review only; Wedding Planner orchestrates providers and is not itself an image generator. Phase 5 concept contributions remain pinned provenance and are not re-approved here.";
const SYNTHETIC_DEVELOPMENT_CREATIVE_PACKAGE = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE";
const THIRTEEN_TO_SIX_EXPLANATION = "Thirteen logical roles map to 6 workers/runs, not 13 subscriptions";
const CREATIVE_LOGICAL_ROLES_OPS = [
  "CREATIVE_DIRECTOR", "CAMPAIGN_STRATEGIST", "AUDIENCE_STRATEGIST", "OFFER_STRATEGIST", "CHANNEL_STRATEGIST",
  "VISUAL_DESIGNER", "LAYOUT_DESIGNER", "TYPOGRAPHY_DESIGNER", "IMAGE_PROMPT_DESIGNER",
  "HEADLINE_SPECIALIST", "BODY_COPY_SPECIALIST", "CTA_SPECIALIST", "VARIANT_PRODUCER"
];
const CREATIVE_WORKER_PROFILES_OPS = [
  "CREATIVE_DIRECTION_V1",
  "STRATEGY_ADAPTATION_V1",
  "VISUAL_SYSTEM_V1",
  "IMAGE_DIRECTION_V1",
  "COPY_SYSTEM_V1",
  "VARIANT_PRODUCTION_V1"
];
const CREATIVE_FORMATS_OPS = ["STATIC_SOCIAL_SQUARE", "STATIC_SOCIAL_STORY", "STATIC_DISPLAY_BANNER", "EMAIL_HERO"];
const GUID_PATTERN_OPS = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

function syncOpsCreativeRevisionFields() {
  const kind = $("#wedding-planner-creative-job-kind")?.value || "INITIAL";
  const fields = $("#wedding-planner-creative-revision-fields");
  if (fields) fields.hidden = kind !== "REVISION";
}

function isValidGuidOps(value) {
  return typeof value === "string" && GUID_PATTERN_OPS.test(value);
}

function safeCreativeAssetContentUrlOps(assetId) {
  if (!isValidGuidOps(assetId)) return null;
  return `/api/wedding-planner/creative-assets/${assetId}/content`;
}

function renderOpsSafeDraftPngPreview(assetId, altText) {
  const src = safeCreativeAssetContentUrlOps(assetId);
  if (!src) {
    return `<div class="safety-note">Invalid or missing creative asset id — draft PNG preview withheld. Only same-origin /api/wedding-planner/creative-assets/{guid}/content is allowed.</div>`;
  }
  const alt = String(altText || "Draft creative PNG for review only").slice(0, 200);
  return `<img class="creative-draft-png" src="${escapeHtml(src)}" alt="${escapeHtml(alt)}" loading="lazy" decoding="async">`;
}

function parseWeddingPlannerCreativePackageDocument(documentJson) {
  if (!documentJson) return null;
  try {
    return typeof documentJson === "string" ? JSON.parse(documentJson) : documentJson;
  } catch {
    return null;
  }
}

async function loadWeddingPlannerCreativeDetails() {
  const packageId = state.selectedWeddingPlannerCreativePackageId;
  if (!packageId) {
    state.weddingPlannerCreativeContributions = [];
    state.weddingPlannerCreativeAssets = [];
    state.weddingPlannerCreativeAgentRuns = [];
    return;
  }
  try {
    const [contributions, assets, runs, packageDto] = await Promise.all([
      api(`/api/wedding-planner/creative-packages/${packageId}/contributions`),
      api(`/api/wedding-planner/creative-packages/${packageId}/assets`),
      api(`/api/wedding-planner/creative-packages/${packageId}/agent-runs`),
      api(`/api/wedding-planner/creative-packages/${packageId}`)
    ]);
    state.weddingPlannerCreativeContributions = Array.isArray(contributions) ? contributions : [];
    state.weddingPlannerCreativeAssets = Array.isArray(assets) ? assets : [];
    state.weddingPlannerCreativeAgentRuns = Array.isArray(runs) ? runs : [];
    const versions = state.weddingPlannerCreativePackages?.versions || [];
    const idx = versions.findIndex(x => x.creativePackageVersionId === packageId);
    if (idx >= 0 && packageDto) versions[idx] = packageDto;
  } catch {
    state.weddingPlannerCreativeContributions = [];
    state.weddingPlannerCreativeAssets = [];
    state.weddingPlannerCreativeAgentRuns = [];
  }
}

function syncOpsCreativeVariantRadios() {
  const host = $("#wedding-planner-creative-variant-radios");
  if (!host) return;
  const versions = state.weddingPlannerCreativePackages?.versions || [];
  const selected = versions.find(x => x.creativePackageVersionId === state.selectedWeddingPlannerCreativePackageId) || null;
  const doc = parseWeddingPlannerCreativePackageDocument(selected?.documentJson);
  const variants = Array.isArray(doc?.variants) ? doc.variants : [];
  if (!variants.length) {
    host.innerHTML = `<p class="safety-note">Variants load from the selected package documentJson.</p>`;
    return;
  }
  host.innerHTML = variants.map(v =>
    `<label class="checkbox-label"><input type="radio" name="selectedVariantId" value="${escapeHtml(v.id || "")}"> ${escapeHtml(v.id || "")} · ${escapeHtml(v.format || "")}</label>`
  ).join("");
}

function renderWeddingPlannerCreativeInspect() {
  const panel = $("#wedding-planner-creative-inspect");
  if (!panel) return;
  const versions = state.weddingPlannerCreativePackages?.versions || [];
  const currentId = state.weddingPlannerCreativePackages?.currentApprovedCreativePackageVersionId || null;
  const selected = versions.find(x => x.creativePackageVersionId === state.selectedWeddingPlannerCreativePackageId) || versions[0] || null;
  const selectedJob = (state.weddingPlannerCreativeJobs || []).find(x => x.creativeProductionJobId === state.selectedWeddingPlannerCreativeJobId) || null;
  if (!selected && !selectedJob) {
    panel.innerHTML = emptyState("Select a creative-production job or creative package to inspect input SHA, provenance, documentJson, contributions, assets, and agent-run receipts.");
    return;
  }

  const doc = parseWeddingPlannerCreativePackageDocument(selected?.documentJson);
  const isCurrent = selected && (selected.creativePackageVersionId === currentId || selected.isCurrentApproved);
  const isSuperseded = selected && selected.status === "SUPERSEDED";
  const hasSynthetic = doc?.marker === SYNTHETIC_DEVELOPMENT_CREATIVE_PACKAGE
    || JSON.stringify(doc || {}).includes(SYNTHETIC_DEVELOPMENT_CREATIVE_PACKAGE)
    || String(selected?.summary || "").includes(SYNTHETIC_DEVELOPMENT_CREATIVE_PACKAGE);
  const contributions = state.weddingPlannerCreativeContributions || [];
  const assets = state.weddingPlannerCreativeAssets || [];
  const runs = state.weddingPlannerCreativeAgentRuns || [];
  const variants = Array.isArray(doc?.variants) ? doc.variants : [];
  const snap = doc?.selectedConceptSnapshot;

  const jobBlock = selectedJob ? `
    <div class="detail-hero"><div class="detail-hero-top">${badge(selectedJob.status)}</div>
      <h3>Creative-production job · ${escapeHtml(selectedJob.jobKind || "")}</h3>
      <p>${escapeHtml(selectedJob.objective || "No objective")}</p>
    </div>
    <div class="metric-grid">
      ${metric("Input SHA-256", selectedJob.inputSha256 || "—")}
      ${metric("Selected concept", selectedJob.selectedConceptId || "—")}
      ${metric("Concept package pin", selectedJob.approvedConceptPackageVersionId || "—")}
      ${metric("Brand DNA pin", `${shortId(selectedJob.approvedBrandDnaVersionId || "")} v${selectedJob.approvedBrandDnaVersionNumber ?? "—"}`)}
      ${metric("Color pin", `${shortId(selectedJob.approvedColorProfileVersionId || "")} v${selectedJob.approvedColorProfileVersionNumber ?? "—"}`)}
      ${metric("Research pin", `${shortId(selectedJob.approvedResearchReportVersionId || "")} v${selectedJob.approvedResearchReportVersionNumber ?? "—"}`)}
      ${metric("Formats", Array.isArray(selectedJob.formats) ? selectedJob.formats.join(", ") : "—")}
      ${metric("Variant count", String(selectedJob.requestedVariantCount ?? "—"))}
      ${metric("Parent revision", selectedJob.revisionParentCreativePackageVersionId || "—")}
      ${metric("Asset provider", selectedJob.assetProviderKey || "—")}
      ${metric("Asset adapter", selectedJob.assetProviderAdapterVersion || "—")}
      ${metric("Asset cost USD", formatUsd(selectedJob.assetProviderEstimatedCostUsd))}
      ${metric("Output package", selectedJob.outputCreativePackageVersionId || "—")}
    </div>
  ` : "";

  const selectedVariantNote = state.weddingPlannerSelectedVariantId
    ? metric("Selected variant (decision)", state.weddingPlannerSelectedVariantId)
    : "";

  const contributionRows = CREATIVE_LOGICAL_ROLES_OPS.map(role => {
    const row = contributions.find(x => x.logicalRole === role);
    const docRow = (doc?.contributions || []).find(x => x.logicalRole === role);
    let summary = docRow?.summary || "";
    if (row?.contributionJson) {
      try {
        const parsed = typeof row.contributionJson === "string" ? JSON.parse(row.contributionJson) : row.contributionJson;
        if (parsed?.summary) summary = parsed.summary;
      } catch { /* keep */ }
    }
    return `<div class="activity-item"><span class="activity-icon">◆</span><span><strong>${escapeHtml(role)}</strong><small>${escapeHtml(summary || "—")}<br>Producing run ${escapeHtml(row?.producingAgentRunId || "—")}</small></span></div>`;
  }).join("");

  const variantRows = variants.length
    ? variants.map(v => {
        const copy = v.copy || {};
        const assetId = v.asset?.creativeAssetId || null;
        return `<div class="activity-item"><span class="activity-icon">▣</span><span><strong>${escapeHtml(v.id || "—")} · ${escapeHtml(v.format || "—")}</strong><small>Canvas ${escapeHtml(String(v.canvas?.width ?? "—"))}×${escapeHtml(String(v.canvas?.height ?? "—"))}<br>Copy kind ${escapeHtml(copy.kind || "—")}<br>Headline: ${escapeHtml(copy.headline || "—")}<br>Body: ${escapeHtml(copy.body || "—")}<br>CTA: ${escapeHtml(copy.cta || "—")}<br>Palette: ${escapeHtml((v.paletteRoleRefs || []).join(", ") || "—")}<br>Image prompt: ${escapeHtml(v.imagePrompt || "—")}<br>Claims: ${escapeHtml(JSON.stringify(v.factualClaims || []))}<br>Asset ${escapeHtml(assetId || "—")}</small>${renderOpsSafeDraftPngPreview(assetId, `Draft PNG ${v.id || ""} review only`)}</span></div>`;
      }).join("")
    : emptyState("No variants in server documentJson.");

  const assetRows = assets.length
    ? assets.map(a => `<div class="activity-item"><span class="activity-icon">▤</span><span><strong>${escapeHtml(a.variantId || "—")} · ${escapeHtml(a.format || "—")}</strong><small>${escapeHtml(String(a.width))}×${escapeHtml(String(a.height))} · ${escapeHtml(a.contentType || "")} · ${escapeHtml(String(a.byteSize ?? "—"))} bytes<br>SHA ${escapeHtml(a.sha256 || "—")}<br>Provider ${escapeHtml(a.providerKey || "—")} · adapter ${escapeHtml(a.adapterVersion || "—")} · cost $${escapeHtml(formatUsd(a.estimatedCostUsd))}<br>Asset id ${escapeHtml(a.creativeAssetId || "—")}</small>${renderOpsSafeDraftPngPreview(a.creativeAssetId, `Draft PNG asset ${a.variantId || ""} review only`)}</span></div>`).join("")
    : emptyState("No assets returned.");

  const runRows = runs.length
    ? runs.map(r => `<div class="activity-item"><span class="activity-icon">◇</span><span><strong>${escapeHtml(r.logicalRole || "—")} · ${escapeHtml(r.status || "—")}</strong><small>Profile ${escapeHtml(r.workerProfileVersion || "—")} · prompt ${escapeHtml(r.promptPackVersion || "—")}<br>Assigned ${escapeHtml(r.assignedRolesJson || "—")}<br>Tokens ${escapeHtml(String(r.promptTokens ?? "—"))}/${escapeHtml(String(r.completionTokens ?? "—"))}/${escapeHtml(String(r.totalTokens ?? "—"))} · $${escapeHtml(formatUsd(r.estimatedCostUsd))}<br>Provider ${escapeHtml(r.providerKey || "—")} · model ${escapeHtml(r.modelId || "—")}</small></span></div>`).join("")
    : emptyState("No agent-run receipts.");

  const snapBlock = snap
    ? `<div class="activity-item"><span class="activity-icon">◎</span><span><strong>${escapeHtml(snap.id || "—")} · ${escapeHtml(snap.name || "—")}</strong><small>${escapeHtml(snap.rationale || "")}<br>Copy kind ${escapeHtml(snap.copy?.kind || "—")} · palette ${escapeHtml((snap.paletteRoleRefs || []).join(", ") || "—")}<br>Phase 5 provenance pinned — not re-approved here</small></span></div>`
    : emptyState("No selectedConceptSnapshot in documentJson.");

  panel.innerHTML = `
    ${jobBlock}
    ${selected ? `
      <div class="detail-hero"><div class="detail-hero-top">${badge(selected.status)}${isCurrent ? badge("CURRENT") : ""}${isSuperseded ? badge("SUPERSEDED") : ""}</div>
        <h3>Creative package v${escapeHtml(String(selected.versionNumber))}</h3>
        <p>${escapeHtml(selected.summary || "No summary")}</p>
      </div>
      <div class="metric-grid">
        ${metric("Schema", selected.schemaVersion || "—")}
        ${metric("Job kind", selected.jobKind || "—")}
        ${metric("Selected concept", selected.selectedConceptId || "—")}
        ${metric("Concept package pin", selected.approvedConceptPackageVersionId || "—")}
        ${metric("Brand DNA pin", `${shortId(selected.approvedBrandDnaVersionId || "")} v${selected.approvedBrandDnaVersionNumber ?? "—"}`)}
        ${metric("Color pin", `${shortId(selected.approvedColorProfileVersionId || "")} v${selected.approvedColorProfileVersionNumber ?? "—"}`)}
        ${metric("Research pin", `${shortId(selected.approvedResearchReportVersionId || "")} v${selected.approvedResearchReportVersionNumber ?? "—"}`)}
        ${metric("Parent revision", selected.parentCreativePackageVersionId || "—")}
        ${metric("Estimated total cost USD", formatUsd(selected.estimatedTotalCostUsd))}
        ${metric("Estimated asset cost USD", formatUsd(selected.estimatedAssetCostUsd))}
        ${metric("Current pointer", isCurrent ? "YES" : "NO")}
        ${selectedVariantNote}
      </div>
      <div class="safety-note"><strong>Draft creative disclaimer (from documentJson).</strong> ${escapeHtml(doc?.disclaimer || CREATIVE_PACKAGE_DISCLAIMER_OPS)}</div>
      ${hasSynthetic ? `<div class="safety-note creative-synthetic-warning"><strong>${escapeHtml(SYNTHETIC_DEVELOPMENT_CREATIVE_PACKAGE)}</strong> Local/synthetic creative output. Draft creative approval only — not final, campaign-ready, QA, legal, matching, accessibility, or compliance.</div>` : ""}
      <div class="safety-note">${escapeHtml(THIRTEEN_TO_SIX_EXPLANATION)}. Profiles ${escapeHtml(CREATIVE_WORKER_PROFILES_OPS.join(", "))}. Showing ${runs.length} receipt(s) from /creative-packages/{id}/agent-runs. Asset provider is separate and non-AI. Draft PNG &lt;img&gt; only from same-origin creative-assets content.</div>
      <h3>Selected concept snapshot (Phase 5 provenance)</h3>
      ${snapBlock}
      <h3>Variants (exact N)</h3>
      ${variantRows}
      <h3>Contributions (exactly 13)</h3>
      ${contributionRows}
      <h3>Assets (provider receipts)</h3>
      ${assetRows}
      <h3>Agent run receipts (exactly 6 workers)</h3>
      ${runRows}
    ` : emptyState("No creative package selected.")}
  `;
}

async function submitWeddingPlannerCreativeJob(form) {
  const value = name => form.elements[name]?.value?.trim?.() ?? String(form.elements[name]?.value || "").trim();
  const jobKind = value("jobKind");
  const formats = [...form.querySelectorAll('input[name="creativeFormat"]:checked')]
    .map(input => String(input.value || "").trim())
    .filter(Boolean);
  const uniqueFormats = [...new Set(formats)];
  const variantCount = Number(value("requestedVariantCount"));
  if (!uniqueFormats.length || uniqueFormats.length > 4 || uniqueFormats.some(f => !CREATIVE_FORMATS_OPS.includes(f)) || uniqueFormats.length !== formats.length) {
    toast("Select 1–4 unique Phase 5 formats.", true);
    return;
  }
  if (!Number.isInteger(variantCount) || variantCount < 1 || variantCount > 4 || variantCount < uniqueFormats.length) {
    toast("Requested variant count must be 1–4 and ≥ selected formats.", true);
    return;
  }
  const payload = {
    jobKind,
    objective: value("objective"),
    formats: uniqueFormats,
    requestedVariantCount: variantCount,
    sourceSystem: value("sourceSystem"),
    idempotencyKey: value("idempotencyKey")
  };
  if (jobKind === "REVISION") {
    const parent = value("revisionParentCreativePackageVersionId");
    const notes = value("revisionNotes");
    if (!parent || !GUID_PATTERN_OPS.test(parent)) {
      toast("REVISION requires a parent creative package version id.", true);
      return;
    }
    if (!notes) {
      toast("REVISION requires non-empty revision notes.", true);
      return;
    }
    payload.revisionParentCreativePackageVersionId = parent;
    payload.revisionNotes = notes;
  } else if (jobKind === "INITIAL") {
    if (value("revisionParentCreativePackageVersionId") || value("revisionNotes")) {
      toast("INITIAL forbids revision parent and revision notes.", true);
      return;
    }
  } else {
    toast("Job kind must be INITIAL or REVISION.", true);
    return;
  }
  try {
    const workspaceId = value("workspaceId");
    const result = await api(`/api/wedding-planner/workspaces/${workspaceId}/creative-production-jobs`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    const replayNote = result.isReplay && result.status === "FAILED"
      ? " (failed replay — not a retry)"
      : result.isReplay ? " (replay)" : "";
    toast(`Creative-production job ${result.status}${replayNote}`);
    fillKey("#wedding-planner-creative-job-key");
    state.selectedWeddingPlannerWorkspace = workspaceId;
    state.selectedWeddingPlannerCreativeJobId = result.creativeProductionJobId;
    if (result.outputCreativePackageVersionId) {
      state.selectedWeddingPlannerCreativePackageId = result.outputCreativePackageVersionId;
    }
    state.weddingPlannerCreativeJobs = await api(`/api/wedding-planner/workspaces/${workspaceId}/creative-production-jobs`);
    state.weddingPlannerCreativePackages = await api(`/api/wedding-planner/workspaces/${workspaceId}/creative-packages`);
    await mergeWeddingPlannerWorkspaceRuns(workspaceId);
    await loadWeddingPlannerCreativeDetails();
    renderWeddingPlanner();
  } catch (error) {
    toast(error.message, true);
  }
}

async function submitWeddingPlannerCreativeDecision(form) {
  const value = name => form.elements[name].value.trim();
  const decision = value("decision");
  if (decision === "APPROVE" && form.elements.confirmApprove?.checked !== true) {
    toast("Confirm the APPROVE checkbox before approving a creative package.", true);
    return;
  }
  const selectedRadio = form.querySelector('input[name="selectedVariantId"]:checked');
  const selectedVariantId = selectedRadio ? String(selectedRadio.value || "").trim() : "";
  if (decision === "APPROVE") {
    if (!selectedVariantId || !/^variant_[1-4]$/.test(selectedVariantId)) {
      toast("APPROVE requires a radio-selected variant id (variant_1 .. variant_N).", true);
      return;
    }
  }
  if (decision === "REJECT" && selectedVariantId) {
    toast("REJECT forbids variant selection. Clear the selected variant before rejecting.", true);
    return;
  }
  const payload = {
    decision,
    rationale: value("rationale"),
    sourceSystem: value("sourceSystem"),
    idempotencyKey: value("idempotencyKey"),
    selectedVariantId: decision === "APPROVE" ? selectedVariantId : null
  };
  try {
    const result = await api(`/api/wedding-planner/creative-packages/${value("creativePackageVersionId")}/decisions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    toast(`Creative package ${friendlyStatus(result.decision)} recorded${result.isReplay ? " (replay)" : ""} — draft creative approval only`);
    form.elements.rationale.value = "";
    if (form.elements.confirmApprove) form.elements.confirmApprove.checked = false;
    form.querySelectorAll('input[name="selectedVariantId"]').forEach(input => { input.checked = false; });
    fillKey("#wedding-planner-creative-decision-key");
    state.weddingPlannerSelectedVariantId = result.selectedVariantId || null;
    const workspaceId = result.workspaceId || state.selectedWeddingPlannerWorkspace;
    if (workspaceId) {
      state.weddingPlannerCreativePackages = await api(`/api/wedding-planner/workspaces/${workspaceId}/creative-packages`);
      state.selectedWeddingPlannerCreativePackageId = result.creativePackageVersionId || state.selectedWeddingPlannerCreativePackageId;
      await loadWeddingPlannerCreativeDetails();
    }
    renderWeddingPlanner();
  } catch (error) {
    toast(error.message, true);
  }
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
    state.weddingPlannerColorProfiles = await api(`/api/wedding-planner/workspaces/${result.workspaceId}/color-profiles`);
    state.selectedWeddingPlannerColorProfileId =
      state.weddingPlannerColorProfiles?.currentApprovedColorProfileVersionId ||
      state.weddingPlannerColorProfiles?.versions?.[0]?.colorProfileVersionId ||
      null;
    state.weddingPlannerResearchJobs = await api(`/api/wedding-planner/workspaces/${result.workspaceId}/research-jobs`).catch(() => []);
    state.weddingPlannerResearchReports = await api(`/api/wedding-planner/workspaces/${result.workspaceId}/research-reports`).catch(() => null);
    state.selectedWeddingPlannerResearchJobId = state.weddingPlannerResearchJobs?.[0]?.researchJobId || null;
    state.selectedWeddingPlannerResearchReportId =
      state.weddingPlannerResearchReports?.currentApprovedResearchReportVersionId ||
      state.weddingPlannerResearchReports?.versions?.[0]?.researchReportVersionId ||
      null;
    state.weddingPlannerWorkshopJobs = await api(`/api/wedding-planner/workspaces/${result.workspaceId}/workshop-jobs`).catch(() => []);
    state.weddingPlannerConceptPackages = await api(`/api/wedding-planner/workspaces/${result.workspaceId}/concept-packages`).catch(() => null);
    state.selectedWeddingPlannerWorkshopJobId = state.weddingPlannerWorkshopJobs?.[0]?.workshopJobId || null;
    state.selectedWeddingPlannerConceptPackageId =
      state.weddingPlannerConceptPackages?.currentApprovedConceptPackageVersionId ||
      state.weddingPlannerConceptPackages?.versions?.[0]?.conceptPackageVersionId ||
      null;
    await mergeWeddingPlannerInterpreterRuns();
    await mergeWeddingPlannerWorkspaceRuns(result.workspaceId);
    await loadWeddingPlannerResearchAgentRuns();
    await loadWeddingPlannerWorkshopDetails();
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
    await mergeWeddingPlannerInterpreterRuns();
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
      state.weddingPlannerColorProfiles = await api(`/api/wedding-planner/workspaces/${workspaceId}/color-profiles`).catch(() => state.weddingPlannerColorProfiles);
    }
    renderWeddingPlanner();
  } catch (error) {
    toast(error.message, true);
  }
}

async function submitWeddingPlannerColorCompute(form) {
  const value = name => form.elements[name].value.trim();
  const optional = name => {
    const raw = value(name);
    return raw || null;
  };
  try {
    const workspaceId = value("workspaceId");
    const result = await api(`/api/wedding-planner/workspaces/${workspaceId}/color-profiles/compute`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        primaryHex: value("primaryHex"),
        secondaryHex: optional("secondaryHex"),
        accentHex: optional("accentHex"),
        backgroundHex: optional("backgroundHex"),
        surfaceHex: optional("surfaceHex"),
        notes: optional("notes"),
        sourceSystem: value("sourceSystem"),
        idempotencyKey: value("idempotencyKey")
      })
    });
    toast(result.isReplay ? `Color profile v${result.versionNumber} replayed` : `Color profile v${result.versionNumber} proposed`);
    fillKey("#wedding-planner-color-compute-key");
    state.selectedWeddingPlannerWorkspace = workspaceId;
    state.selectedWeddingPlannerColorProfileId = result.colorProfileVersionId;
    state.weddingPlannerColorProfiles = await api(`/api/wedding-planner/workspaces/${workspaceId}/color-profiles`);
    renderWeddingPlanner();
  } catch (error) {
    toast(error.message, true);
  }
}

async function submitWeddingPlannerColorDecision(form) {
  const value = name => form.elements[name].value.trim();
  const decision = value("decision");
  if (decision === "APPROVE" && form.elements.confirmApprove?.checked !== true) {
    toast("Confirm the APPROVE checkbox before approving a color profile.", true);
    return;
  }
  try {
    const result = await api(`/api/wedding-planner/color-profiles/${value("colorProfileVersionId")}/decisions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        decision,
        rationale: value("rationale"),
        sourceSystem: value("sourceSystem"),
        idempotencyKey: value("idempotencyKey")
      })
    });
    toast(`Color ${friendlyStatus(result.decision)} recorded${result.isReplay ? " (replay)" : ""}`);
    form.elements.rationale.value = "";
    if (form.elements.confirmApprove) form.elements.confirmApprove.checked = false;
    fillKey("#wedding-planner-color-decision-key");
    const workspaceId = result.workspaceId || state.selectedWeddingPlannerWorkspace;
    if (workspaceId) {
      state.weddingPlannerColorProfiles = await api(`/api/wedding-planner/workspaces/${workspaceId}/color-profiles`);
      state.selectedWeddingPlannerColorProfileId = result.colorProfileVersionId || state.selectedWeddingPlannerColorProfileId;
    }
    renderWeddingPlanner();
  } catch (error) {
    toast(error.message, true);
  }
}

function linesToList(value, min, max) {
  const lines = String(value || "")
    .split(/\r?\n/)
    .map(line => line.trim())
    .filter(Boolean);
  if (lines.length < min || lines.length > max) return null;
  return lines;
}

async function submitWeddingPlannerResearchJob(form) {
  const value = name => form.elements[name].value.trim();
  const questions = linesToList(form.elements.questions?.value, 1, 8);
  const allowedDomains = linesToList(form.elements.allowedDomains?.value, 0, 10);
  if (!questions) {
    toast("Provide 1–8 non-empty questions, one per line.", true);
    return;
  }
  if (!allowedDomains && String(form.elements.allowedDomains?.value || "").trim()) {
    toast("Allowed domains must be 0–10 non-empty hostnames, one per line.", true);
    return;
  }
  try {
    const workspaceId = value("workspaceId");
    const result = await api(`/api/wedding-planner/workspaces/${workspaceId}/research-jobs`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        topic: value("topic"),
        objective: value("objective"),
        questions,
        geography: value("geography"),
        language: value("language"),
        allowedDomains: allowedDomains || [],
        sourceSystem: value("sourceSystem"),
        idempotencyKey: value("idempotencyKey")
      })
    });
    const replayNote = result.isReplay && result.status === "FAILED"
      ? " (failed replay — not a retry)"
      : result.isReplay ? " (replay)" : "";
    toast(`Research job ${result.status}${replayNote}`);
    fillKey("#wedding-planner-research-job-key");
    state.selectedWeddingPlannerWorkspace = workspaceId;
    state.selectedWeddingPlannerResearchJobId = result.researchJobId;
    if (result.outputResearchReportVersionId) {
      state.selectedWeddingPlannerResearchReportId = result.outputResearchReportVersionId;
    }
    state.weddingPlannerResearchJobs = await api(`/api/wedding-planner/workspaces/${workspaceId}/research-jobs`);
    state.weddingPlannerResearchReports = await api(`/api/wedding-planner/workspaces/${workspaceId}/research-reports`);
    await mergeWeddingPlannerWorkspaceRuns(workspaceId);
    await loadWeddingPlannerResearchAgentRuns();
    renderWeddingPlanner();
  } catch (error) {
    toast(error.message, true);
  }
}

async function submitWeddingPlannerResearchDecision(form) {
  const value = name => form.elements[name].value.trim();
  const decision = value("decision");
  if (decision === "APPROVE" && form.elements.confirmApprove?.checked !== true) {
    toast("Confirm the APPROVE checkbox before approving a research report.", true);
    return;
  }
  try {
    const result = await api(`/api/wedding-planner/research-reports/${value("researchReportVersionId")}/decisions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        decision,
        rationale: value("rationale"),
        sourceSystem: value("sourceSystem"),
        idempotencyKey: value("idempotencyKey")
      })
    });
    toast(`Research ${friendlyStatus(result.decision)} recorded${result.isReplay ? " (replay)" : ""} — research approval only`);
    form.elements.rationale.value = "";
    if (form.elements.confirmApprove) form.elements.confirmApprove.checked = false;
    fillKey("#wedding-planner-research-decision-key");
    const workspaceId = result.workspaceId || state.selectedWeddingPlannerWorkspace;
    if (workspaceId) {
      state.weddingPlannerResearchReports = await api(`/api/wedding-planner/workspaces/${workspaceId}/research-reports`);
      state.selectedWeddingPlannerResearchReportId = result.researchReportVersionId || state.selectedWeddingPlannerResearchReportId;
      await loadWeddingPlannerResearchAgentRuns();
    }
    renderWeddingPlanner();
  } catch (error) {
    toast(error.message, true);
  }
}
function prettyJson(value){try{return escapeHtml(JSON.stringify(typeof value==="string"?JSON.parse(value):value,null,2));}catch{return escapeHtml(value||"No snapshot");}}
function emptyState(message){return `<div class="empty-state">${escapeHtml(message)}</div>`;}
function escapeHtml(value){return String(value??"").replace(/[&<>"']/g,char=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#039;"}[char]));}
