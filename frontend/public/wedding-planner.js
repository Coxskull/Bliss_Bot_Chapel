const SOURCE_SYSTEM = "PUBLIC_WEDDING_PLANNER";
const COLOR_INTELLIGENCE_LABEL = "PHASE 3 · DETERMINISTIC COLOR INTELLIGENCE";
const CURATOR_LABEL = "PHASE 4 · THE CURATOR";
const WORKSHOP_LABEL = "PHASE 5 · CONCEPT / PROTOTYPE WORKSHOP";
const RESEARCH_DISCLAIMER =
  "Approval of this report is research approval only. It is not creative, campaign, claim, legal, matching, accessibility, or compliance approval. Source verification checks metadata and internal consistency only; live URL content is not fetched or certified.";
const CONCEPT_PACKAGE_DISCLAIMER =
  "Approval of this package is concept-direction approval only. It is not research, claim, legal, matching, accessibility, compliance, campaign-ready, asset, QA, or production-artwork approval. Marketing copy is CREATIVE_NON_FACTUAL unless a factual claim cites source IDs from the pinned approved research report. Brand DNA and Color Profile are creative constraints, not factual evidence. Prototypes are structured low-fi specs only; no images are generated.";
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
const CURATOR_WORKER_PROFILES = [
  "CURATOR_RESEARCH_V1",
  "CURATOR_EVIDENCE_V1",
  "CURATOR_SYNTHESIS_RISK_V1"
];
const WORKSHOP_LOGICAL_ROLES = [
  "BRAND_STRATEGIST",
  "ART_DIRECTOR",
  "COPYWRITER",
  "PRODUCTION_ARTIST"
];
const WORKSHOP_WORKER_PROFILES = [
  "CONCEPT_STRATEGY_V1",
  "CONCEPT_CREATIVE_V1",
  "PROTOTYPE_PRODUCTION_V1"
];
const WORKSHOP_CHANNEL_FORMATS = [
  "STATIC_SOCIAL_SQUARE",
  "STATIC_SOCIAL_STORY",
  "STATIC_DISPLAY_BANNER",
  "EMAIL_HERO"
];
const RESEARCH_JOB_STATUSES = ["RUNNING", "SUCCEEDED", "FAILED"];
const RESEARCH_REPORT_STATUSES = ["PROPOSED", "APPROVED", "REJECTED", "SUPERSEDED", "CURRENT"];
const EIGHT_TO_THREE_EXPLANATION =
  "Eight logical roles map to 3 workers/runs, not 8 subscriptions";
const FOUR_TO_THREE_EXPLANATION =
  "Four logical roles map to 3 workers/runs, not 4 subscriptions";
const SYNTHETIC_DEVELOPMENT_PROTOTYPE = "SYNTHETIC DEVELOPMENT PROTOTYPE";
const ALLOWED_TEXT_REFS = ["copy.headline", "copy.body", "copy.cta"];
const ALLOWED_PROTOTYPE_TEMPLATES = ["LOFI_STACK_V1", "LOFI_SPLIT_V1", "LOFI_BANNER_V1"];
const ALLOWED_REGION_TYPES = ["HERO", "HEADER", "BODY", "HEADLINE", "SUBHEAD", "CTA", "FOOTER", "LOGO_SLOT"];
const ALLOWED_PLACEHOLDER_KINDS = ["HERO_IMAGE", "LOGO", "PRODUCT", "DECORATIVE"];

const PALETTE_ROLES = [
  "primary", "secondary", "accent", "background", "surface",
  "onPrimary", "onSecondary", "onAccent", "onBackground", "onSurface",
  "neutral50", "neutral100", "neutral200", "neutral400", "neutral600", "neutral800", "neutral900"
];

const state = {
  session: null,
  liveChatEnabled: false,
  workspaceId: null,
  sessionId: null,
  advertiserId: null,
  csrfToken: null,
  messages: [],
  brandDna: null,
  brandDnaList: null,
  colorProfiles: null,
  colorProfile: null,
  researchJobs: [],
  researchJob: null,
  researchReports: null,
  researchReport: null,
  researchAgentRuns: [],
  workshopJobs: [],
  workshopJob: null,
  conceptPackages: null,
  conceptPackage: null,
  workshopContributions: [],
  workshopAgentRuns: [],
  workshopSelectedConceptId: null,
  workshopPinnedColorDocument: null,
  busy: false
};

const menu = document.querySelector(".menu-button");
const nav = document.querySelector(".site-header nav");
const statusEl = document.querySelector("#planner-session-status");
const streamEl = document.querySelector("#planner-message-stream");
const turnStatusEl = document.querySelector("#planner-turn-status");
const composer = document.querySelector("#planner-composer");
const input = document.querySelector("#planner-message-input");
const sendButton = document.querySelector("#planner-send");
const createDnaButton = document.querySelector("#create-brand-dna");
const dnaSummary = document.querySelector("#brand-dna-summary");
const dnaStatusLabel = document.querySelector("#brand-dna-status-label");
const dnaVersion = document.querySelector("#brand-dna-version");
const dnaSummaryText = document.querySelector("#brand-dna-summary-text");
const dnaCurrentPointer = document.querySelector("#brand-dna-current-pointer");
const dnaDecisionForm = document.querySelector("#brand-dna-decision-form");
const dnaRationale = document.querySelector("#brand-dna-rationale");
const dnaConfirmApprove = document.querySelector("#brand-dna-confirm-approve");

const colorPrereq = document.querySelector("#color-prereq-status");
const colorComputeForm = document.querySelector("#color-compute-form");
const colorComputeSubmit = document.querySelector("#color-compute-submit");
const colorRefreshList = document.querySelector("#color-refresh-list");
const colorNotes = document.querySelector("#color-notes");
const colorProfileSelect = document.querySelector("#color-profile-select");
const colorProfileView = document.querySelector("#color-profile-view");
const colorStatusLabel = document.querySelector("#color-status-label");
const colorCurrentBadge = document.querySelector("#color-current-badge");
const colorVersionNumber = document.querySelector("#color-version-number");
const colorSummaryText = document.querySelector("#color-summary-text");
const colorSchemaVersion = document.querySelector("#color-schema-version");
const colorAlgorithmVersion = document.querySelector("#color-algorithm-version");
const colorInputSha = document.querySelector("#color-input-sha");
const colorBrandDnaProvenance = document.querySelector("#color-brand-dna-provenance");
const colorSwatchGrid = document.querySelector("#color-swatch-grid");
const colorContrastDisclaimer = document.querySelector("#color-contrast-disclaimer");
const colorContrastList = document.querySelector("#color-contrast-list");
const colorGeometryDisclaimer = document.querySelector("#color-geometry-disclaimer");
const colorDecisionForm = document.querySelector("#color-decision-form");
const colorRationale = document.querySelector("#color-rationale");
const colorConfirmApprove = document.querySelector("#color-confirm-approve");

const curatorPrereq = document.querySelector("#curator-prereq-status");
const curatorJobForm = document.querySelector("#curator-job-form");
const curatorJobSubmit = document.querySelector("#curator-job-submit");
const curatorRefreshLists = document.querySelector("#curator-refresh-lists");
const curatorTopic = document.querySelector("#curator-topic");
const curatorObjective = document.querySelector("#curator-objective");
const curatorQuestions = document.querySelector("#curator-questions");
const curatorGeography = document.querySelector("#curator-geography");
const curatorLanguage = document.querySelector("#curator-language");
const curatorAllowedDomains = document.querySelector("#curator-allowed-domains");
const curatorJobSelect = document.querySelector("#curator-job-select");
const curatorJobMeta = document.querySelector("#curator-job-meta");
const curatorReportSelect = document.querySelector("#curator-report-select");
const curatorCurrentPointer = document.querySelector("#curator-current-pointer");
const curatorReportView = document.querySelector("#curator-report-view");
const curatorReportStatusLabel = document.querySelector("#curator-report-status-label");
const curatorReportCurrentBadge = document.querySelector("#curator-report-current-badge");
const curatorReportVersionNumber = document.querySelector("#curator-report-version-number");
const curatorReportSummaryText = document.querySelector("#curator-report-summary-text");
const curatorReportSchema = document.querySelector("#curator-report-schema");
const curatorReportCost = document.querySelector("#curator-report-cost");
const curatorDisclaimerBlock = document.querySelector("#curator-disclaimer-block");
const curatorDisclaimerText = document.querySelector("#curator-disclaimer-text");
const curatorSyntheticWarning = document.querySelector("#curator-synthetic-warning");
const curatorProvenance = document.querySelector("#curator-provenance");
const curatorSources = document.querySelector("#curator-sources");
const curatorContributions = document.querySelector("#curator-contributions");
const curatorSynthesis = document.querySelector("#curator-synthesis");
const curatorAgentRuns = document.querySelector("#curator-agent-runs");
const curatorDecisionForm = document.querySelector("#curator-decision-form");
const curatorRationale = document.querySelector("#curator-rationale");
const curatorConfirmApprove = document.querySelector("#curator-confirm-approve");

const workshopPrereq = document.querySelector("#workshop-prereq-status");
const workshopJobForm = document.querySelector("#workshop-job-form");
const workshopJobSubmit = document.querySelector("#workshop-job-submit");
const workshopRefreshLists = document.querySelector("#workshop-refresh-lists");
const workshopObjective = document.querySelector("#workshop-objective");
const workshopCampaignGoal = document.querySelector("#workshop-campaign-goal");
const workshopAudienceFocus = document.querySelector("#workshop-audience-focus");
const workshopChannelFormat = document.querySelector("#workshop-channel-format");
const workshopDeliverables = document.querySelector("#workshop-deliverables");
const workshopCta = document.querySelector("#workshop-cta");
const workshopConstraints = document.querySelector("#workshop-constraints");
const workshopJobSelect = document.querySelector("#workshop-job-select");
const workshopJobMeta = document.querySelector("#workshop-job-meta");
const workshopPackageSelect = document.querySelector("#workshop-package-select");
const workshopCurrentPointer = document.querySelector("#workshop-current-pointer");
const workshopPackageView = document.querySelector("#workshop-package-view");
const workshopPackageStatusLabel = document.querySelector("#workshop-package-status-label");
const workshopPackageCurrentBadge = document.querySelector("#workshop-package-current-badge");
const workshopPackageVersionNumber = document.querySelector("#workshop-package-version-number");
const workshopPackageSummaryText = document.querySelector("#workshop-package-summary-text");
const workshopPackageSchema = document.querySelector("#workshop-package-schema");
const workshopPackageChannel = document.querySelector("#workshop-package-channel");
const workshopPackageCanvas = document.querySelector("#workshop-package-canvas");
const workshopPackageCost = document.querySelector("#workshop-package-cost");
const workshopSelectedConceptLine = document.querySelector("#workshop-selected-concept-line");
const workshopSelectedConceptIdEl = document.querySelector("#workshop-selected-concept-id");
const workshopDisclaimerBlock = document.querySelector("#workshop-disclaimer-block");
const workshopDisclaimerText = document.querySelector("#workshop-disclaimer-text");
const workshopSyntheticWarning = document.querySelector("#workshop-synthetic-warning");
const workshopConcepts = document.querySelector("#workshop-concepts");
const workshopContributions = document.querySelector("#workshop-contributions");
const workshopAgentRuns = document.querySelector("#workshop-agent-runs");
const workshopDecisionForm = document.querySelector("#workshop-decision-form");
const workshopConceptSelect = document.querySelector("#workshop-concept-select");
const workshopRationale = document.querySelector("#workshop-rationale");
const workshopConfirmApprove = document.querySelector("#workshop-confirm-approve");

const colorFields = {
  primary: {
    picker: document.querySelector("#color-primary-picker"),
    text: document.querySelector("#color-primary-hex"),
    required: true
  },
  secondary: {
    picker: document.querySelector("#color-secondary-picker"),
    text: document.querySelector("#color-secondary-hex"),
    required: false
  },
  accent: {
    picker: document.querySelector("#color-accent-picker"),
    text: document.querySelector("#color-accent-hex"),
    required: false
  },
  background: {
    picker: document.querySelector("#color-background-picker"),
    text: document.querySelector("#color-background-hex"),
    required: false
  },
  surface: {
    picker: document.querySelector("#color-surface-picker"),
    text: document.querySelector("#color-surface-hex"),
    required: false
  }
};

menu?.addEventListener("click", () => {
  const open = nav?.classList.toggle("open") ?? false;
  menu.setAttribute("aria-expanded", String(open));
});

document.querySelectorAll(".site-header nav a").forEach(link => {
  link.addEventListener("click", () => {
    nav?.classList.remove("open");
    menu?.setAttribute("aria-expanded", "false");
  });
});

document.addEventListener("DOMContentLoaded", () => {
  bindPlannerUi();
  bootstrapPlanner().catch(error => {
    setSessionStatus("Unable to check authentication", error.message);
    setComposerEnabled(false);
    setColorControlsEnabled(false);
    setCuratorControlsEnabled(false);
    setWorkshopControlsEnabled(false);
    renderColorPrerequisite();
    renderCuratorPrerequisite();
    renderWorkshopPrerequisite();
  });
});

function bindPlannerUi() {
  composer?.addEventListener("submit", event => {
    event.preventDefault();
    submitTurn().catch(error => setTurnStatus("error", error.message));
  });

  createDnaButton?.addEventListener("click", () => {
    createBrandDnaProposal().catch(error => setTurnStatus("error", error.message));
  });

  dnaDecisionForm?.addEventListener("submit", event => {
    event.preventDefault();
    const submitter = event.submitter;
    const decision = submitter?.dataset?.decision;
    if (!decision) return;
    decideBrandDna(decision).catch(error => setTurnStatus("error", error.message));
  });

  colorComputeForm?.addEventListener("submit", event => {
    event.preventDefault();
    computeColorProfile().catch(error => setTurnStatus("error", error.message));
  });

  colorRefreshList?.addEventListener("click", () => {
    loadColorProfiles().catch(error => setTurnStatus("error", error.message));
  });

  colorProfileSelect?.addEventListener("change", () => {
    const id = colorProfileSelect.value;
    const versions = state.colorProfiles?.versions || [];
    const selected = versions.find(x => x.colorProfileVersionId === id) || null;
    state.colorProfile = selected;
    renderColorProfile(selected, state.colorProfiles);
  });

  colorDecisionForm?.addEventListener("submit", event => {
    event.preventDefault();
    const submitter = event.submitter;
    const decision = submitter?.dataset?.decision;
    if (!decision) return;
    decideColorProfile(decision).catch(error => setTurnStatus("error", error.message));
  });

  curatorJobForm?.addEventListener("submit", event => {
    event.preventDefault();
    submitResearchJob().catch(error => setTurnStatus("error", error.message));
  });

  curatorRefreshLists?.addEventListener("click", () => {
    refreshCuratorLists().catch(error => setTurnStatus("error", error.message));
  });

  curatorJobSelect?.addEventListener("change", () => {
    const id = curatorJobSelect.value;
    const selected = (state.researchJobs || []).find(x => x.researchJobId === id) || null;
    state.researchJob = selected;
    renderResearchJob(selected);
  });

  curatorReportSelect?.addEventListener("change", () => {
    const id = curatorReportSelect.value;
    const versions = state.researchReports?.versions || [];
    const selected = versions.find(x => x.researchReportVersionId === id) || null;
    state.researchReport = selected;
    inspectResearchReport(selected).catch(error => setTurnStatus("error", error.message));
  });

  curatorDecisionForm?.addEventListener("submit", event => {
    event.preventDefault();
    const submitter = event.submitter;
    const decision = submitter?.dataset?.decision;
    if (!decision) return;
    decideResearchReport(decision).catch(error => setTurnStatus("error", error.message));
  });

  workshopJobForm?.addEventListener("submit", event => {
    event.preventDefault();
    submitWorkshopJob().catch(error => setTurnStatus("error", error.message));
  });

  workshopRefreshLists?.addEventListener("click", () => {
    refreshWorkshopLists().catch(error => setTurnStatus("error", error.message));
  });

  workshopJobSelect?.addEventListener("change", () => {
    const id = workshopJobSelect.value;
    const selected = (state.workshopJobs || []).find(x => x.workshopJobId === id) || null;
    state.workshopJob = selected;
    renderWorkshopJob(selected);
  });

  workshopPackageSelect?.addEventListener("change", () => {
    const id = workshopPackageSelect.value;
    const versions = state.conceptPackages?.versions || [];
    const selected = versions.find(x => x.conceptPackageVersionId === id) || null;
    state.conceptPackage = selected;
    state.workshopSelectedConceptId = null;
    inspectConceptPackage(selected).catch(error => setTurnStatus("error", error.message));
  });

  workshopDecisionForm?.addEventListener("submit", event => {
    event.preventDefault();
    const submitter = event.submitter;
    const decision = submitter?.dataset?.decision;
    if (!decision) return;
    decideConceptPackage(decision).catch(error => setTurnStatus("error", error.message));
  });

  Object.values(colorFields).forEach(field => {
    field.picker?.addEventListener("input", () => {
      if (field.text) field.text.value = String(field.picker.value || "").toUpperCase();
    });
    field.text?.addEventListener("input", () => {
      const value = String(field.text.value || "").trim();
      if (/^#[0-9A-Fa-f]{6}$/.test(value) && field.picker) {
        field.picker.value = value;
      }
    });
  });

  document.querySelectorAll("[data-example]").forEach(button => {
    button.addEventListener("click", () => {
      if (!state.liveChatEnabled || !input || input.disabled) return;
      input.value = button.dataset.example || "";
      input.focus();
    });
  });
}

async function bootstrapPlanner() {
  const session = await api("/api/auth/session");
  state.session = session;
  state.csrfToken = session.csrfToken || null;

  const authorized =
    session.authenticationEnabled === true &&
    session.isAuthenticated === true &&
    session.canWrite === true &&
    !!session.advertiserId;

  if (!authorized) {
    state.liveChatEnabled = false;
    setComposerEnabled(false);
    setBrandDnaControlsEnabled(false);
    setColorControlsEnabled(false);
    setCuratorControlsEnabled(false);
    setWorkshopControlsEnabled(false);
    renderAuthGate(session);
    renderColorPrerequisite();
    renderCuratorPrerequisite();
    renderWorkshopPrerequisite();
    return;
  }

  state.liveChatEnabled = true;
  state.advertiserId = session.advertiserId;
  setSessionStatus(
    "Authenticated advertiser session",
    `Signed in as ${session.displayName || "advertiser"}. Opening your primary workspace…`
  );

  await openPrimaryWorkspaceAndSession(session.advertiserId);
  await loadMessages();
  await loadBrandDna();
  await loadColorProfiles();
  await refreshCuratorLists();
  await refreshWorkshopLists();
  setComposerEnabled(true);
  setBrandDnaControlsEnabled(true);
  renderColorPrerequisite();
  setColorControlsEnabled(canComputeColorProfiles());
  renderCuratorPrerequisite();
  setCuratorControlsEnabled(canSubmitResearchJobs());
  renderWorkshopPrerequisite();
  setWorkshopControlsEnabled(canSubmitWorkshopJobs());
  setSessionStatus(
    "Live Concierge ready",
    "Messages are saved to your planning session. Planner replies come only from the server. Brand DNA stays PROPOSED until you approve or reject it."
  );
}

function renderAuthGate(session) {
  if (!session.authenticationEnabled) {
    setSessionStatus(
      "Sign-in required for live chat",
      "Authentication is not enabled in this environment, so the public Concierge stays disabled. No development advertiser is bound automatically."
    );
    return;
  }

  if (!session.isAuthenticated) {
    if (!statusEl) return;
    statusEl.innerHTML = `<span>Sign in to continue</span><p>Live chat requires an authenticated advertiser with write access. <a href="/api/auth/login?returnUrl=${encodeURIComponent("/#wedding-planner")}">Continue with SSO</a>.</p>`;
    return;
  }

  if (!session.canWrite) {
    setSessionStatus(
      "Write access required",
      "You are signed in, but this identity cannot write Wedding Planner records. Ask an operator to grant advertiser write access."
    );
    return;
  }

  setSessionStatus(
    "Advertiser binding required",
    "You are authenticated with write permission, but no AdvertiserId is bound to this session. The composer stays disabled until your identity includes an advertiser claim."
  );
}

async function openPrimaryWorkspaceAndSession(advertiserId) {
  const workspaceKey = stableIdempotencyKey("workspace", advertiserId);
  const workspace = await api("/api/wedding-planner/workspaces", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      advertiserId,
      sourceSystem: SOURCE_SYSTEM,
      idempotencyKey: workspaceKey
    })
  });
  state.workspaceId = workspace.workspaceId;

  const sessionKey = stableIdempotencyKey("session", advertiserId);
  const planningSession = await api(`/api/wedding-planner/workspaces/${workspace.workspaceId}/sessions`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      sourceSystem: SOURCE_SYSTEM,
      idempotencyKey: sessionKey
    })
  });
  state.sessionId = planningSession.sessionId;
}

async function loadMessages() {
  if (!state.sessionId) return;
  const messages = await api(`/api/wedding-planner/sessions/${state.sessionId}/messages`);
  state.messages = Array.isArray(messages) ? messages : [];
  renderMessages(state.messages);
}

async function loadBrandDna() {
  if (!state.workspaceId) return;
  const list = await api(`/api/wedding-planner/workspaces/${state.workspaceId}/brand-dna`);
  state.brandDnaList = list;
  const versions = list?.versions || [];
  const selected =
    versions.find(x => x.status === "PROPOSED") ||
    versions.find(x => x.brandDnaVersionId === list.currentApprovedBrandDnaVersionId) ||
    versions[0] ||
    null;
  state.brandDna = selected;
  renderBrandDna(selected, list);
  renderColorPrerequisite();
  setColorControlsEnabled(canComputeColorProfiles());
  renderCuratorPrerequisite();
  setCuratorControlsEnabled(canSubmitResearchJobs());
  renderWorkshopPrerequisite();
  setWorkshopControlsEnabled(canSubmitWorkshopJobs());
}

async function loadColorProfiles() {
  if (!state.workspaceId) return;
  const list = await api(`/api/wedding-planner/workspaces/${state.workspaceId}/color-profiles`);
  state.colorProfiles = list;
  const versions = list?.versions || [];
  const selected =
    versions.find(x => x.colorProfileVersionId === state.colorProfile?.colorProfileVersionId) ||
    versions.find(x => x.status === "PROPOSED") ||
    versions.find(x => x.colorProfileVersionId === list.currentApprovedColorProfileVersionId) ||
    versions[0] ||
    null;
  state.colorProfile = selected;
  renderColorProfileList(list, selected);
  renderColorProfile(selected, list);
  renderCuratorPrerequisite();
  renderWorkshopPrerequisite();
  setWorkshopControlsEnabled(canSubmitWorkshopJobs());
}

function hasCurrentApprovedBrandDna() {
  return !!state.brandDnaList?.currentApprovedBrandDnaVersionId;
}

function canComputeColorProfiles() {
  return state.liveChatEnabled && !!state.workspaceId && hasCurrentApprovedBrandDna();
}

function canSubmitResearchJobs() {
  return state.liveChatEnabled && !!state.workspaceId && hasCurrentApprovedBrandDna();
}

function hasCurrentApprovedColorProfile() {
  return !!state.colorProfiles?.currentApprovedColorProfileVersionId;
}

function hasCurrentApprovedResearchReport() {
  return !!state.researchReports?.currentApprovedResearchReportVersionId;
}

function canSubmitWorkshopJobs() {
  return (
    state.liveChatEnabled &&
    !!state.workspaceId &&
    hasCurrentApprovedBrandDna() &&
    hasCurrentApprovedColorProfile() &&
    hasCurrentApprovedResearchReport()
  );
}

function renderColorPrerequisite() {
  if (!colorPrereq) return;
  if (!state.session) {
    colorPrereq.dataset.ready = "false";
    colorPrereq.innerHTML = `<span>Prerequisite check</span><p>Checking authentication…</p>`;
    return;
  }

  if (!state.liveChatEnabled) {
    colorPrereq.dataset.ready = "false";
    colorPrereq.innerHTML = `<span>Authenticated writable advertiser required</span><p>Color Intelligence compute stays disabled until an authenticated advertiser with write access is signed in. No random or test advertiser is bound automatically.</p>`;
    return;
  }

  if (!hasCurrentApprovedBrandDna()) {
    colorPrereq.dataset.ready = "false";
    colorPrereq.innerHTML = `<span>Current-approved Brand DNA required</span><p>Approve a Brand DNA proposal first. Color profiles cannot be computed without a current-approved Brand DNA pointer for this workspace.</p>`;
    return;
  }

  colorPrereq.dataset.ready = "true";
  colorPrereq.innerHTML = `<span>Prerequisites met · ${COLOR_INTELLIGENCE_LABEL}</span><p>Authenticated writable advertiser and current-approved Brand DNA are present. Compute uses deterministic <code>aci.hsl.v1</code> only — no AI. HSL offsets are geometry, not psychology. WCAG figures are arithmetic evidence, not accessibility certification.</p>`;
}

function renderCuratorPrerequisite() {
  if (!curatorPrereq) return;
  if (!state.session) {
    curatorPrereq.dataset.ready = "false";
    curatorPrereq.innerHTML = `<span>Prerequisite check</span><p>Checking authentication…</p>`;
    return;
  }

  if (!state.liveChatEnabled) {
    curatorPrereq.dataset.ready = "false";
    curatorPrereq.innerHTML = `<span>Authenticated writable advertiser required</span><p>Curator research job submit stays disabled until an authenticated advertiser with write access is signed in. No random or test advertiser is bound automatically.</p>`;
    return;
  }

  if (!hasCurrentApprovedBrandDna()) {
    curatorPrereq.dataset.ready = "false";
    curatorPrereq.innerHTML = `<span>Current-approved Brand DNA required</span><p>Approve Brand DNA first. Research jobs require current-approved Brand DNA. A current-approved color profile is optional provenance only and is never required.</p>`;
    return;
  }

  const colorNote = state.colorProfiles?.currentApprovedColorProfileVersionId
    ? "Current-approved color profile is present as optional provenance."
    : "No current-approved color profile — optional and not required.";
  curatorPrereq.dataset.ready = "true";
  curatorPrereq.innerHTML = `<span>Prerequisites met · ${CURATOR_LABEL}</span><p>Authenticated writable advertiser and current-approved Brand DNA are present. ${escapeHtml(colorNote)} ${escapeHtml(EIGHT_TO_THREE_EXPLANATION)}. Local synthetic evidence is never presented as live research.</p>`;
}

function renderWorkshopPrerequisite() {
  if (!workshopPrereq) return;
  if (!state.session) {
    workshopPrereq.dataset.ready = "false";
    workshopPrereq.innerHTML = `<span>Prerequisite check</span><p>Checking authentication…</p>`;
    return;
  }

  if (!state.liveChatEnabled) {
    workshopPrereq.dataset.ready = "false";
    workshopPrereq.innerHTML = `<span>Authenticated writable advertiser required</span><p>Concept Workshop submit stays disabled until an authenticated advertiser with write access is signed in. No random or test advertiser is bound automatically.</p>`;
    return;
  }

  const missing = [];
  if (!hasCurrentApprovedBrandDna()) missing.push("current-approved Brand DNA");
  if (!hasCurrentApprovedColorProfile()) missing.push("current-approved Color Profile");
  if (!hasCurrentApprovedResearchReport()) missing.push("current-approved Research Report");
  if (missing.length) {
    workshopPrereq.dataset.ready = "false";
    workshopPrereq.innerHTML = `<span>Missing workshop prerequisites</span><p>Workshop jobs require all three current-approved pointers. Missing: ${escapeHtml(missing.join("; "))}. Approve Brand DNA, Color Profile, and Research Report before submit is enabled. ${escapeHtml(FOUR_TO_THREE_EXPLANATION)}.</p>`;
    return;
  }

  workshopPrereq.dataset.ready = "true";
  workshopPrereq.innerHTML = `<span>Prerequisites met · ${WORKSHOP_LABEL}</span><p>Authenticated writable advertiser with current-approved Brand DNA, Color Profile, and Research Report. ${escapeHtml(FOUR_TO_THREE_EXPLANATION)}. Concept direction only — no generated image/assets; not campaign-ready, QA, matching, or legal/claim approval. ${escapeHtml(SYNTHETIC_DEVELOPMENT_PROTOTYPE)} when Local path was used.</p>`;
}

async function submitTurn() {
  if (!state.liveChatEnabled || !state.sessionId || state.busy) return;
  const body = String(input?.value || "").trim();
  if (!body) return;

  state.busy = true;
  setComposerEnabled(false);
  setTurnStatus("loading", "Sending your message to the Concierge…");

  try {
    const turn = await api(`/api/wedding-planner/sessions/${state.sessionId}/turns`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        body,
        sourceSystem: SOURCE_SYSTEM,
        idempotencyKey: `turn-${crypto.randomUUID()}`
      })
    });

    if (input) input.value = "";
    await loadMessages();

    if (turn?.isReplay) {
      setTurnStatus("replay", "Replayed an existing turn. No duplicate planner reply was created.");
    } else if (!turn?.plannerMessage) {
      setTurnStatus(
        "error",
        "Your message was saved, but no planner reply was returned. The server does not fabricate replies."
      );
    } else {
      clearTurnStatus();
    }
  } catch (error) {
    try {
      await loadMessages();
    } catch {
      /* Keep the prior transcript if refresh also fails. */
    }
    setTurnStatus("error", error.message || "Turn failed. No client-side planner reply is invented.");
    throw error;
  } finally {
    state.busy = false;
    setComposerEnabled(state.liveChatEnabled);
  }
}

async function createBrandDnaProposal() {
  if (!state.liveChatEnabled || !state.workspaceId || state.busy) return;
  state.busy = true;
  createDnaButton.disabled = true;
  setTurnStatus("loading", "Creating Brand DNA proposal from conversation history…");
  try {
    const version = await api(`/api/wedding-planner/workspaces/${state.workspaceId}/brand-dna/interpret`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        sourceSystem: SOURCE_SYSTEM,
        idempotencyKey: `dna-${crypto.randomUUID()}`
      })
    });
    await loadBrandDna();
    state.brandDna = version;
    renderBrandDna(version, state.brandDnaList);
    setTurnStatus(
      version.isReplay ? "replay" : "loading",
      version.isReplay
        ? "Replayed an existing Brand DNA proposal."
        : `Created PROPOSED Brand DNA version ${version.versionNumber}. Approval still requires an explicit human decision.`
    );
    if (!version.isReplay) {
      setTimeout(() => clearTurnStatus(), 4200);
    }
  } finally {
    state.busy = false;
    setBrandDnaControlsEnabled(state.liveChatEnabled);
  }
}

async function decideBrandDna(decision) {
  if (!state.liveChatEnabled || !state.brandDna || state.busy) return;
  const rationale = String(dnaRationale?.value || "").trim();
  if (!rationale) {
    setTurnStatus("error", "A rationale is required for Brand DNA decisions.");
    return;
  }
  if (decision === "APPROVE" && !dnaConfirmApprove?.checked) {
    setTurnStatus("error", "Confirm the APPROVE checkbox before approving a Brand DNA proposal.");
    return;
  }

  state.busy = true;
  setBrandDnaControlsEnabled(false);
  setTurnStatus("loading", `Recording ${decision} decision…`);
  try {
    const result = await api(`/api/wedding-planner/brand-dna/${state.brandDna.brandDnaVersionId}/decisions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        decision,
        rationale,
        sourceSystem: SOURCE_SYSTEM,
        idempotencyKey: `dna-decision-${crypto.randomUUID()}`
      })
    });
    await loadBrandDna();
    if (result?.version) {
      state.brandDna = result.version;
      renderBrandDna(result.version, state.brandDnaList);
    }
    if (dnaRationale) dnaRationale.value = "";
    if (dnaConfirmApprove) dnaConfirmApprove.checked = false;
    setTurnStatus(
      result?.isReplay ? "replay" : "loading",
      result?.isReplay
        ? `Replayed ${decision} decision.`
        : `${decision} recorded. PROPOSED and APPROVED remain distinct durable states.`
    );
    if (!result?.isReplay) {
      setTimeout(() => clearTurnStatus(), 4200);
    }
  } finally {
    state.busy = false;
    setBrandDnaControlsEnabled(state.liveChatEnabled);
    setColorControlsEnabled(canComputeColorProfiles());
    setCuratorControlsEnabled(canSubmitResearchJobs());
  }
}

async function computeColorProfile() {
  if (!canComputeColorProfiles() || state.busy) return;
  const primaryHex = String(colorFields.primary.text?.value || "").trim();
  if (!primaryHex) {
    setTurnStatus("error", "Primary hex is required to compute a color profile.");
    return;
  }

  const payload = {
    primaryHex,
    secondaryHex: optionalHex(colorFields.secondary.text?.value),
    accentHex: optionalHex(colorFields.accent.text?.value),
    backgroundHex: optionalHex(colorFields.background.text?.value),
    surfaceHex: optionalHex(colorFields.surface.text?.value),
    notes: String(colorNotes?.value || "").trim() || null,
    sourceSystem: SOURCE_SYSTEM,
    idempotencyKey: `color-${crypto.randomUUID()}`
  };

  state.busy = true;
  setColorControlsEnabled(false);
  setTurnStatus("loading", "Computing deterministic color profile on the server…");
  try {
    const version = await api(`/api/wedding-planner/workspaces/${state.workspaceId}/color-profiles/compute`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    await loadColorProfiles();
    state.colorProfile = version;
    renderColorProfileList(state.colorProfiles, version);
    renderColorProfile(version, state.colorProfiles);
    setTurnStatus(
      version.isReplay ? "replay" : "loading",
      version.isReplay
        ? "Replayed an existing color profile compute."
        : `Created PROPOSED color profile version ${version.versionNumber}. Palette and contrast come only from the server document.`
    );
    if (!version.isReplay) {
      setTimeout(() => clearTurnStatus(), 4200);
    }
  } finally {
    state.busy = false;
    setColorControlsEnabled(canComputeColorProfiles());
  }
}

async function decideColorProfile(decision) {
  if (!state.liveChatEnabled || !state.colorProfile || state.busy) return;
  const rationale = String(colorRationale?.value || "").trim();
  if (!rationale) {
    setTurnStatus("error", "A rationale is required for color profile decisions.");
    return;
  }
  if (decision === "APPROVE" && !colorConfirmApprove?.checked) {
    setTurnStatus("error", "Confirm the APPROVE checkbox before approving a color profile.");
    return;
  }

  state.busy = true;
  setColorDecisionEnabled(false);
  setTurnStatus("loading", `Recording color profile ${decision} decision…`);
  try {
    const result = await api(`/api/wedding-planner/color-profiles/${state.colorProfile.colorProfileVersionId}/decisions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        decision,
        rationale,
        sourceSystem: SOURCE_SYSTEM,
        idempotencyKey: `color-decision-${crypto.randomUUID()}`
      })
    });
    await loadColorProfiles();
    if (result?.version) {
      state.colorProfile = result.version;
      renderColorProfileList(state.colorProfiles, result.version);
      renderColorProfile(result.version, state.colorProfiles);
    }
    if (colorRationale) colorRationale.value = "";
    if (colorConfirmApprove) colorConfirmApprove.checked = false;
    setTurnStatus(
      result?.isReplay ? "replay" : "loading",
      result?.isReplay
        ? `Replayed color profile ${decision} decision.`
        : `${decision} recorded. PROPOSED, APPROVED, and CURRENT remain distinct.`
    );
    if (!result?.isReplay) {
      setTimeout(() => clearTurnStatus(), 4200);
    }
  } finally {
    state.busy = false;
    setColorControlsEnabled(canComputeColorProfiles());
  }
}

async function refreshCuratorLists() {
  if (!state.workspaceId) return;
  const [jobs, reports] = await Promise.all([
    api(`/api/wedding-planner/workspaces/${state.workspaceId}/research-jobs`),
    api(`/api/wedding-planner/workspaces/${state.workspaceId}/research-reports`)
  ]);
  state.researchJobs = Array.isArray(jobs) ? jobs : [];
  state.researchReports = reports;
  const selectedJob =
    state.researchJobs.find(x => x.researchJobId === state.researchJob?.researchJobId) ||
    state.researchJobs[0] ||
    null;
  state.researchJob = selectedJob;
  renderResearchJobList(state.researchJobs, selectedJob);
  renderResearchJob(selectedJob);

  const versions = reports?.versions || [];
  const selectedReport =
    versions.find(x => x.researchReportVersionId === state.researchReport?.researchReportVersionId) ||
    versions.find(x => x.status === "PROPOSED") ||
    versions.find(x => x.researchReportVersionId === reports.currentApprovedResearchReportVersionId) ||
    versions[0] ||
    null;
  state.researchReport = selectedReport;
  renderResearchReportList(reports, selectedReport);
  await inspectResearchReport(selectedReport);
  renderWorkshopPrerequisite();
  setWorkshopControlsEnabled(canSubmitWorkshopJobs());
}

async function submitResearchJob() {
  if (!canSubmitResearchJobs() || state.busy) return;
  const topic = String(curatorTopic?.value || "").trim();
  const objective = String(curatorObjective?.value || "").trim();
  const geography = String(curatorGeography?.value || "").trim();
  const language = String(curatorLanguage?.value || "").trim();
  const questions = linesToList(curatorQuestions?.value, 1, 8);
  const allowedDomains = linesToList(curatorAllowedDomains?.value, 0, 10);

  if (!topic || !objective || !geography || !language) {
    setTurnStatus("error", "Topic, objective, geography, and language are required.");
    return;
  }
  if (!questions) {
    setTurnStatus("error", "Provide 1–8 non-empty questions, one per line.");
    return;
  }
  if (!allowedDomains && String(curatorAllowedDomains?.value || "").trim()) {
    setTurnStatus("error", "Allowed domains must be 0–10 non-empty hostnames, one per line.");
    return;
  }

  const payload = {
    topic,
    objective,
    questions,
    geography,
    language,
    allowedDomains: allowedDomains || [],
    sourceSystem: SOURCE_SYSTEM,
    idempotencyKey: `research-${crypto.randomUUID()}`
  };

  state.busy = true;
  setCuratorControlsEnabled(false);
  setTurnStatus("loading", "Submitting research job to the Curator…");
  try {
    const job = await api(`/api/wedding-planner/workspaces/${state.workspaceId}/research-jobs`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    await refreshCuratorLists();
    state.researchJob = job;
    renderResearchJobList(state.researchJobs, job);
    renderResearchJob(job);
    setTurnStatus(
      job.isReplay ? "replay" : "loading",
      researchJobStatusMessage(job)
    );
    if (!job.isReplay && job.status === "SUCCEEDED") {
      setTimeout(() => clearTurnStatus(), 4200);
    }
  } finally {
    state.busy = false;
    setCuratorControlsEnabled(canSubmitResearchJobs());
  }
}

function researchJobStatusMessage(job) {
  if (job.isReplay && job.status === "FAILED") {
    return "Replayed an existing FAILED research job. The failed job was returned unchanged — this is not a retry and providers were not called again.";
  }
  if (job.isReplay && job.status === "SUCCEEDED") {
    return "Replayed an existing SUCCEEDED research job. Existing job and report linkage returned with no provider or AI calls.";
  }
  if (job.isReplay) {
    return `Replayed an existing research job in status ${job.status}.`;
  }
  if (job.status === "FAILED") {
    return `Research job FAILED${job.errorCode ? ` (${job.errorCode})` : ""}. ${job.errorMessage || "See job details."}`;
  }
  if (job.status === "SUCCEEDED") {
    return "Research job SUCCEEDED. Inspect the PROPOSED report — findings come only from stored server documentJson.";
  }
  return `Research job status: ${job.status}.`;
}

async function decideResearchReport(decision) {
  if (!state.liveChatEnabled || !state.researchReport || state.busy) return;
  const rationale = String(curatorRationale?.value || "").trim();
  if (!rationale) {
    setTurnStatus("error", "A rationale is required for research report decisions.");
    return;
  }
  if (decision === "APPROVE" && !curatorConfirmApprove?.checked) {
    setTurnStatus("error", "Confirm the APPROVE checkbox before approving a research report.");
    return;
  }

  state.busy = true;
  setCuratorDecisionEnabled(false);
  setTurnStatus("loading", `Recording research report ${decision} decision…`);
  try {
    const result = await api(`/api/wedding-planner/research-reports/${state.researchReport.researchReportVersionId}/decisions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        decision,
        rationale,
        sourceSystem: SOURCE_SYSTEM,
        idempotencyKey: `research-decision-${crypto.randomUUID()}`
      })
    });
    await refreshCuratorLists();
    if (result?.version) {
      state.researchReport = result.version;
      renderResearchReportList(state.researchReports, result.version);
      await inspectResearchReport(result.version);
    }
    if (curatorRationale) curatorRationale.value = "";
    if (curatorConfirmApprove) curatorConfirmApprove.checked = false;
    setTurnStatus(
      result?.isReplay ? "replay" : "loading",
      result?.isReplay
        ? `Replayed research report ${decision} decision.`
        : `${decision} recorded as research approval only — not creative, campaign, claim, legal, or matching approval. PROPOSED, APPROVED, and CURRENT remain distinct.`
    );
    if (!result?.isReplay) {
      setTimeout(() => clearTurnStatus(), 4200);
    }
  } finally {
    state.busy = false;
    setCuratorControlsEnabled(canSubmitResearchJobs());
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

function renderResearchJobList(jobs, selected) {
  if (!curatorJobSelect) return;
  if (!jobs.length) {
    curatorJobSelect.innerHTML = `<option value="">No research jobs yet</option>`;
    curatorJobSelect.disabled = true;
    return;
  }
  curatorJobSelect.disabled = false;
  curatorJobSelect.innerHTML = jobs.map(job => {
    const markers = [job.status || "UNKNOWN"];
    if (job.isReplay) markers.push("REPLAY");
    return `<option value="${escapeHtml(job.researchJobId)}"${selected?.researchJobId === job.researchJobId ? " selected" : ""}>${escapeHtml(markers.join(" · "))} · ${escapeHtml(job.topic || shortId(job.researchJobId))}</option>`;
  }).join("");
}

function renderResearchJob(job) {
  if (!curatorJobMeta) return;
  if (!job) {
    curatorJobMeta.hidden = true;
    curatorJobMeta.innerHTML = "";
    return;
  }
  curatorJobMeta.hidden = false;
  curatorJobMeta.dataset.status = job.status || "";
  const replayNote = job.isReplay && job.status === "FAILED"
    ? `<p><strong>Failed replay:</strong> existing FAILED job returned unchanged — not a retry.</p>`
    : job.isReplay
      ? `<p>Idempotent replay of existing job (no new provider/AI calls).</p>`
      : "";
  const errorBlock = job.status === "FAILED"
    ? `<p><strong>Error</strong> ${escapeHtml(job.errorCode || "—")}: ${escapeHtml(job.errorMessage || "No error message.")}</p>`
    : "";
  curatorJobMeta.innerHTML = `
    <p><strong>${escapeHtml(job.status || "UNKNOWN")}</strong> · ${escapeHtml(job.topic || "")}</p>
    <p>Input SHA-256 <code>${escapeHtml(job.inputSha256 || "—")}</code></p>
    <p>Provider <code>${escapeHtml(job.researchProviderKey || "—")}</code> · adapter <code>${escapeHtml(job.researchAdapterVersion || "—")}</code> · worker <code>${escapeHtml(job.researchWorkerKey || "—")}</code></p>
    <p>Provider request <code>${escapeHtml(job.researchProviderRequestId || "—")}</code> · acquisition cost USD <code>${escapeHtml(formatCost(job.researchEstimatedCostUsd))}</code></p>
    <p>Brand DNA provenance <code>${escapeHtml(job.approvedBrandDnaVersionId || "—")}</code> · color provenance <code>${escapeHtml(job.approvedColorProfileVersionId || "optional/none")}</code></p>
    ${errorBlock}
    ${replayNote}
  `;
}

function renderResearchReportList(list, selected) {
  if (!curatorReportSelect) return;
  const versions = list?.versions || [];
  const currentId = list?.currentApprovedResearchReportVersionId || null;
  if (curatorCurrentPointer) {
    curatorCurrentPointer.hidden = !currentId;
  }
  if (!versions.length) {
    curatorReportSelect.innerHTML = `<option value="">No research reports yet</option>`;
    curatorReportSelect.disabled = true;
    return;
  }
  curatorReportSelect.disabled = false;
  curatorReportSelect.innerHTML = versions.map(version => {
    const markers = [];
    if (version.status) markers.push(version.status);
    if (version.researchReportVersionId === currentId || version.isCurrentApproved) markers.push("CURRENT");
    return `<option value="${escapeHtml(version.researchReportVersionId)}"${selected?.researchReportVersionId === version.researchReportVersionId ? " selected" : ""}>v${escapeHtml(String(version.versionNumber))} · ${escapeHtml(markers.join(" · "))}</option>`;
  }).join("");
}

async function inspectResearchReport(version) {
  if (!curatorReportView) return;
  if (!version) {
    curatorReportView.hidden = true;
    if (curatorDecisionForm) curatorDecisionForm.hidden = true;
    state.researchAgentRuns = [];
    return;
  }

  curatorReportView.hidden = false;
  const currentId = state.researchReports?.currentApprovedResearchReportVersionId || null;
  const isCurrent = !!currentId && (currentId === version.researchReportVersionId || version.isCurrentApproved === true);

  if (curatorReportStatusLabel) {
    curatorReportStatusLabel.textContent = version.status || "UNKNOWN";
    curatorReportStatusLabel.dataset.status = version.status || "";
  }
  if (curatorReportCurrentBadge) curatorReportCurrentBadge.hidden = !isCurrent;
  if (curatorReportVersionNumber) curatorReportVersionNumber.textContent = String(version.versionNumber ?? "—");
  if (curatorReportSummaryText) curatorReportSummaryText.textContent = version.summary || "No summary returned by the server.";
  if (curatorReportSchema) curatorReportSchema.textContent = version.schemaVersion || "—";
  if (curatorReportCost) curatorReportCost.textContent = formatCost(version.estimatedTotalCostUsd);

  const document = parseResearchDocument(version.documentJson);
  renderResearchDocument(document);

  state.researchAgentRuns = [];
  try {
    const runs = await api(`/api/wedding-planner/research-reports/${version.researchReportVersionId}/agent-runs`);
    state.researchAgentRuns = Array.isArray(runs) ? runs : [];
  } catch {
    state.researchAgentRuns = [];
  }
  renderResearchAgentRuns(state.researchAgentRuns);

  const canDecide = state.liveChatEnabled && version.status === "PROPOSED";
  if (curatorDecisionForm) curatorDecisionForm.hidden = !canDecide;
  setCuratorDecisionEnabled(canDecide);
}

function parseResearchDocument(documentJson) {
  if (!documentJson) return null;
  try {
    return typeof documentJson === "string" ? JSON.parse(documentJson) : documentJson;
  } catch {
    return null;
  }
}

function renderResearchDocument(document) {
  const disclaimer = document?.disclaimer || "";
  if (curatorDisclaimerBlock && curatorDisclaimerText) {
    curatorDisclaimerText.textContent = disclaimer;
    curatorDisclaimerBlock.hidden = !disclaimer;
  }

  const sources = Array.isArray(document?.sources) ? document.sources : [];
  const hasSynthetic = sources.some(source =>
    source?.synthetic === true ||
    /\.invalid\b/i.test(String(source?.url || "")) ||
    /SYNTHETIC/i.test(String(source?.title || "")) ||
    /SYNTHETIC/i.test(String(source?.publisher || ""))
  );
  if (curatorSyntheticWarning) {
    curatorSyntheticWarning.hidden = !hasSynthetic;
  }

  if (curatorProvenance) {
    const provenance = document?.provenance || null;
    curatorProvenance.innerHTML = provenance
      ? `<div class="curator-source-card">
          <p>Brand DNA version <code>${escapeHtml(provenance.approvedBrandDnaVersionId || "—")}</code> · number <code>${escapeHtml(String(provenance.approvedBrandDnaVersionNumber ?? "—"))}</code></p>
          <p>Color profile version <code>${escapeHtml(provenance.approvedColorProfileVersionId || "optional/none")}</code></p>
          <p>Research job <code>${escapeHtml(provenance.researchJobId || "—")}</code></p>
        </div>`
      : `<p class="seed-hint">No provenance object in server documentJson.</p>`;
  }

  if (curatorSources) {
    if (!sources.length) {
      curatorSources.innerHTML = `<p class="seed-hint">No sources in server documentJson.</p>`;
    } else {
      curatorSources.innerHTML = sources.map(source => {
        const synthetic = source?.synthetic === true;
        const url = String(source?.url || "");
        const invalidHost = /\.invalid\b/i.test(url);
        const badges = [
          synthetic ? `<span class="synthetic-badge">SYNTHETIC</span>` : "",
          invalidHost ? `<span class="invalid-host-badge">.invalid</span>` : ""
        ].join("");
        return `
          <div class="curator-source-card${synthetic || invalidHost ? " synthetic" : ""}">
            <strong>${escapeHtml(source.id || "source")}${badges}</strong>
            <span>${escapeHtml(source.title || "")}</span>
            <span>${escapeHtml(source.publisher || "")} · ${escapeHtml(source.retrievedAt || "")}</span>
            <div>${renderSafeExternalLink(url)}</div>
          </div>`;
      }).join("");
    }
  }

  if (curatorContributions) {
    const contributions = Array.isArray(document?.contributions) ? document.contributions : [];
    const ordered = CURATOR_LOGICAL_ROLES.map(role =>
      contributions.find(item => item?.logicalRole === role)
    ).filter(Boolean);
    if (ordered.length !== 8) {
      curatorContributions.innerHTML = `<p class="seed-hint">Expected exactly 8 role contributions in documentJson; found ${escapeHtml(String(ordered.length))} recognized roles (total entries: ${escapeHtml(String(contributions.length))}). No client-generated findings are invented.</p>`;
    } else {
      curatorContributions.innerHTML = ordered.map(role => {
        const findings = Array.isArray(role.findings) ? role.findings : [];
        const findingHtml = findings.map(finding => `
          <div class="curator-finding">
            <div class="finding-type">${escapeHtml(finding.type || "—")} · confidence ${escapeHtml(String(finding.confidence ?? "—"))}</div>
            <div>${escapeHtml(finding.statement || "")}</div>
            <div>citationSourceIds: ${escapeHtml((Array.isArray(finding.citationSourceIds) ? finding.citationSourceIds : []).join(", ") || "(none)")}</div>
          </div>`).join("");
        return `
          <div class="curator-contribution-card">
            <strong>${escapeHtml(role.logicalRole || "")}</strong>
            <span>${escapeHtml(role.summary || "")}</span>
            ${findingHtml || `<p class="seed-hint">No findings array for this role in documentJson.</p>`}
          </div>`;
      }).join("");
    }
  }

  if (curatorSynthesis) {
    const synthesis = document?.synthesis || null;
    if (!synthesis) {
      curatorSynthesis.innerHTML = `<p class="seed-hint">No synthesis object in server documentJson.</p>`;
    } else {
      const openQuestions = Array.isArray(synthesis.openQuestions) ? synthesis.openQuestions : [];
      const risks = Array.isArray(synthesis.risks) ? synthesis.risks : [];
      curatorSynthesis.innerHTML = `
        <div class="curator-contribution-card">
          <strong>Executive summary</strong>
          <p>${escapeHtml(synthesis.executiveSummary || "")}</p>
          <strong>Open questions</strong>
          <ul>${openQuestions.map(item => `<li>${escapeHtml(item)}</li>`).join("") || "<li>(none)</li>"}</ul>
          <strong>Risks</strong>
          <ul>${risks.map(item => `<li>${escapeHtml(item)}</li>`).join("") || "<li>(none)</li>"}</ul>
        </div>`;
    }
  }

  void RESEARCH_DISCLAIMER;
  void CURATOR_WORKER_PROFILES;
  void RESEARCH_JOB_STATUSES;
  void RESEARCH_REPORT_STATUSES;
}

function renderSafeExternalLink(url) {
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
  return `<a class="curator-safe-link" href="${escapeHtml(trimmed)}" target="_blank" rel="noopener noreferrer nofollow">${escapeHtml(trimmed)}</a>`;
}

function renderResearchAgentRuns(runs) {
  if (!curatorAgentRuns) return;
  if (!runs.length) {
    curatorAgentRuns.innerHTML = `<p class="seed-hint">No agent-run receipts returned for this report.</p>`;
    return;
  }
  curatorAgentRuns.innerHTML = `
    <p class="curator-roles-note">${escapeHtml(EIGHT_TO_THREE_EXPLANATION)}. Showing ${escapeHtml(String(runs.length))} receipt(s) from the report endpoint.</p>
    ${runs.map(run => `
      <div class="curator-run-card">
        <strong>${escapeHtml(run.logicalRole || "—")} · ${escapeHtml(run.status || "—")}</strong>
        <span>Worker profile <code>${escapeHtml(run.workerProfileVersion || "—")}</code> · prompt <code>${escapeHtml(run.promptPackVersion || "—")}</code></span>
        <span>Assigned roles <code>${escapeHtml(run.assignedRolesJson || "—")}</code></span>
        <span>Tokens prompt/completion/total: ${escapeHtml(String(run.promptTokens ?? "—"))} / ${escapeHtml(String(run.completionTokens ?? "—"))} / ${escapeHtml(String(run.totalTokens ?? "—"))}</span>
        <span>Estimated cost USD <code>${escapeHtml(formatCost(run.estimatedCostUsd))}</code> · provider <code>${escapeHtml(run.providerKey || "—")}</code> · model <code>${escapeHtml(run.modelId || "—")}</code></span>
      </div>`).join("")}
  `;
}

function formatCost(value) {
  if (value == null || value === "") return "—";
  const number = Number(value);
  return Number.isFinite(number) ? number.toFixed(6) : String(value);
}

function shortId(value) {
  const text = String(value || "");
  return text.length <= 12 ? text : `${text.slice(0, 8)}…`;
}

function optionalHex(value) {
  const trimmed = String(value || "").trim();
  return trimmed || null;
}

function renderMessages(messages) {
  if (!streamEl) return;
  if (!messages.length) {
    streamEl.innerHTML = `
      <div class="message-row">
        <div class="mini-bot" aria-hidden="true">⌁</div>
        <div class="message">
          <strong>Your planning session is ready.</strong>
          <p>Send a message to begin. Planner replies appear only after the server Concierge responds.</p>
        </div>
      </div>`;
    return;
  }

  streamEl.innerHTML = messages.map(message => {
    const isPlanner = message.actorType === "PLANNER";
    const label = isPlanner ? "Wedding Planner" : (message.actorLabel || message.actorType || "You");
    const rowClass = isPlanner ? "" : " human";
    const avatar = isPlanner ? "⌁" : initials(label);
    return `
      <div class="message-row${rowClass}">
        <div class="mini-bot" aria-hidden="true">${escapeHtml(avatar)}</div>
        <div class="message">
          <strong>${escapeHtml(label)}</strong>
          <p>${escapeHtml(message.body)}</p>
          <time datetime="${escapeHtml(message.createdAt || "")}">${formatTime(message.createdAt)}</time>
        </div>
      </div>`;
  }).join("");
  streamEl.scrollTop = streamEl.scrollHeight;
}

function renderBrandDna(version, list) {
  if (!dnaSummary) return;
  if (!version) {
    dnaSummary.hidden = true;
    dnaDecisionForm.hidden = true;
    return;
  }

  dnaSummary.hidden = false;
  dnaStatusLabel.textContent = version.status || "UNKNOWN";
  dnaStatusLabel.dataset.status = version.status || "";
  dnaVersion.textContent = String(version.versionNumber ?? "—");
  dnaSummaryText.textContent = version.summary || "No summary returned by the interpreter.";
  const isCurrent = !!list?.currentApprovedBrandDnaVersionId &&
    list.currentApprovedBrandDnaVersionId === version.brandDnaVersionId;
  dnaCurrentPointer.hidden = !isCurrent;
  const canDecide = version.status === "PROPOSED";
  dnaDecisionForm.hidden = !canDecide;
  dnaRationale.disabled = !canDecide || !state.liveChatEnabled;
  dnaConfirmApprove.disabled = !canDecide || !state.liveChatEnabled;
  dnaDecisionForm.querySelectorAll("button").forEach(button => {
    button.disabled = !canDecide || !state.liveChatEnabled;
  });
}

function renderColorProfileList(list, selected) {
  if (!colorProfileSelect) return;
  const versions = list?.versions || [];
  const currentId = list?.currentApprovedColorProfileVersionId || null;
  if (!versions.length) {
    colorProfileSelect.innerHTML = `<option value="">No color profiles yet</option>`;
    colorProfileSelect.disabled = true;
    return;
  }
  colorProfileSelect.disabled = false;
  colorProfileSelect.innerHTML = versions.map(version => {
    const markers = [];
    if (version.status) markers.push(version.status);
    if (version.colorProfileVersionId === currentId || version.isCurrentApproved) markers.push("CURRENT");
    return `<option value="${escapeHtml(version.colorProfileVersionId)}"${selected?.colorProfileVersionId === version.colorProfileVersionId ? " selected" : ""}>v${escapeHtml(String(version.versionNumber))} · ${escapeHtml(markers.join(" · "))}</option>`;
  }).join("");
}

function renderColorProfile(version, list) {
  if (!colorProfileView) return;
  if (!version) {
    colorProfileView.hidden = true;
    if (colorDecisionForm) colorDecisionForm.hidden = true;
    return;
  }

  colorProfileView.hidden = false;
  const currentId = list?.currentApprovedColorProfileVersionId || null;
  const isCurrent = !!currentId && (currentId === version.colorProfileVersionId || version.isCurrentApproved === true);

  if (colorStatusLabel) {
    colorStatusLabel.textContent = version.status || "UNKNOWN";
    colorStatusLabel.dataset.status = version.status || "";
  }
  if (colorCurrentBadge) colorCurrentBadge.hidden = !isCurrent;
  if (colorVersionNumber) colorVersionNumber.textContent = String(version.versionNumber ?? "—");
  if (colorSummaryText) colorSummaryText.textContent = version.summary || "No summary returned by the server.";
  if (colorSchemaVersion) colorSchemaVersion.textContent = version.schemaVersion || "—";
  if (colorAlgorithmVersion) colorAlgorithmVersion.textContent = version.algorithmVersion || "—";
  if (colorInputSha) colorInputSha.textContent = version.inputSha256 || "—";
  if (colorBrandDnaProvenance) {
    colorBrandDnaProvenance.textContent = version.approvedBrandDnaVersionId || "—";
  }

  const document = parseColorDocument(version.documentJson);
  renderColorSwatches(document);
  renderColorContrast(document);
  if (colorGeometryDisclaimer) {
    colorGeometryDisclaimer.textContent = document?.geometryDisclaimer || "";
    colorGeometryDisclaimer.hidden = !document?.geometryDisclaimer;
  }

  const canDecide = state.liveChatEnabled && version.status === "PROPOSED";
  if (colorDecisionForm) colorDecisionForm.hidden = !canDecide;
  setColorDecisionEnabled(canDecide);
}

function parseColorDocument(documentJson) {
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

function renderColorSwatches(document) {
  if (!colorSwatchGrid) return;
  const palette = document?.palette;
  if (!palette) {
    colorSwatchGrid.innerHTML = `<p class="seed-hint">No palette roles in server documentJson.</p>`;
    return;
  }
  const seeds = document?.seeds || null;
  colorSwatchGrid.innerHTML = PALETTE_ROLES.map(role => {
    const entry = palette[role];
    const hex = entry?.hex;
    if (!hex) return "";
    const provenance = ["primary", "secondary", "accent", "background", "surface"].includes(role)
      ? seedProvenanceLabel(seeds, role)
      : "";
    return `
      <div class="color-swatch">
        <div class="color-swatch-chip" style="background:${escapeHtml(hex)}" title="${escapeHtml(hex)}"></div>
        <strong>${escapeHtml(role)}</strong>
        <code>${escapeHtml(hex)}</code>
        ${provenance ? `<span class="provenance">${escapeHtml(provenance)}</span>` : ""}
      </div>`;
  }).filter(Boolean).join("");
}

function renderColorContrast(document) {
  if (!colorContrastList) return;
  const evidence = document?.contrastEvidence;
  if (colorContrastDisclaimer) {
    colorContrastDisclaimer.textContent = evidence?.disclaimer || "";
    colorContrastDisclaimer.hidden = !evidence?.disclaimer;
  }
  const pairs = Array.isArray(evidence?.pairs) ? evidence.pairs : [];
  if (!pairs.length) {
    colorContrastList.innerHTML = `<p class="seed-hint">No contrast evidence pairs in server documentJson.</p>`;
    return;
  }
  colorContrastList.innerHTML = pairs.map(pair => {
    const ratio = pair.ratio == null ? "—" : Number(pair.ratio).toFixed(2);
    return `
      <div class="color-contrast-row">
        <div class="color-contrast-pair" aria-hidden="true">
          <span style="background:${escapeHtml(pair.foregroundHex || "#000000")}"></span>
          <span style="background:${escapeHtml(pair.backgroundHex || "#FFFFFF")}"></span>
        </div>
        <div class="color-contrast-info">
          <strong>${escapeHtml(pair.foregroundRole || "?")} on ${escapeHtml(pair.backgroundRole || "?")}</strong>
          <span>Ratio ${escapeHtml(ratio)} · ${escapeHtml(pair.foregroundHex || "")} / ${escapeHtml(pair.backgroundHex || "")}</span>
        </div>
        <div class="aa-badges">
          ${aaBadge("AA", pair.aaNormal)}
          ${aaBadge("AA large", pair.aaLarge)}
          ${aaBadge("AAA", pair.aaaNormal)}
          ${aaBadge("AAA large", pair.aaaLarge)}
        </div>
      </div>`;
  }).join("");
}

function aaBadge(label, pass) {
  const cls = pass === true ? "pass" : "fail";
  const text = pass === true ? `${label} pass` : `${label} fail`;
  return `<span class="aa-badge ${cls}">${escapeHtml(text)}</span>`;
}

function setComposerEnabled(enabled) {
  if (input) input.disabled = !enabled;
  if (sendButton) sendButton.disabled = !enabled;
  document.querySelectorAll("[data-example]").forEach(button => {
    button.disabled = !enabled;
  });
}

function setBrandDnaControlsEnabled(enabled) {
  if (createDnaButton) createDnaButton.disabled = !enabled || !state.workspaceId;
  const canDecide = enabled && state.brandDna?.status === "PROPOSED";
  if (dnaRationale) dnaRationale.disabled = !canDecide;
  if (dnaConfirmApprove) dnaConfirmApprove.disabled = !canDecide;
  dnaDecisionForm?.querySelectorAll("button").forEach(button => {
    button.disabled = !canDecide;
  });
}

function setColorControlsEnabled(enabled) {
  Object.values(colorFields).forEach(field => {
    if (field.picker) field.picker.disabled = !enabled;
    if (field.text) field.text.disabled = !enabled;
  });
  if (colorNotes) colorNotes.disabled = !enabled;
  if (colorComputeSubmit) colorComputeSubmit.disabled = !enabled;
  if (colorRefreshList) colorRefreshList.disabled = !state.liveChatEnabled || !state.workspaceId;
  if (colorProfileSelect) {
    const hasVersions = (state.colorProfiles?.versions || []).length > 0;
    colorProfileSelect.disabled = !state.liveChatEnabled || !hasVersions;
  }
  setColorDecisionEnabled(enabled && state.colorProfile?.status === "PROPOSED");
}

function setColorDecisionEnabled(enabled) {
  if (colorRationale) colorRationale.disabled = !enabled;
  if (colorConfirmApprove) colorConfirmApprove.disabled = !enabled;
  colorDecisionForm?.querySelectorAll("button").forEach(button => {
    button.disabled = !enabled;
  });
}

function setCuratorControlsEnabled(enabled) {
  const fields = [
    curatorTopic, curatorObjective, curatorQuestions,
    curatorGeography, curatorLanguage, curatorAllowedDomains
  ];
  fields.forEach(field => {
    if (field) field.disabled = !enabled;
  });
  if (curatorJobSubmit) curatorJobSubmit.disabled = !enabled;
  if (curatorRefreshLists) curatorRefreshLists.disabled = !state.liveChatEnabled || !state.workspaceId;
  if (curatorJobSelect) {
    const hasJobs = (state.researchJobs || []).length > 0;
    curatorJobSelect.disabled = !state.liveChatEnabled || !hasJobs;
  }
  if (curatorReportSelect) {
    const hasReports = (state.researchReports?.versions || []).length > 0;
    curatorReportSelect.disabled = !state.liveChatEnabled || !hasReports;
  }
  setCuratorDecisionEnabled(enabled && state.researchReport?.status === "PROPOSED");
}

function setCuratorDecisionEnabled(enabled) {
  if (curatorRationale) curatorRationale.disabled = !enabled;
  if (curatorConfirmApprove) curatorConfirmApprove.disabled = !enabled;
  curatorDecisionForm?.querySelectorAll("button").forEach(button => {
    button.disabled = !enabled;
  });
}

async function refreshWorkshopLists() {
  if (!state.workspaceId) return;
  const [jobs, packages] = await Promise.all([
    api(`/api/wedding-planner/workspaces/${state.workspaceId}/workshop-jobs`),
    api(`/api/wedding-planner/workspaces/${state.workspaceId}/concept-packages`)
  ]);
  state.workshopJobs = Array.isArray(jobs) ? jobs : [];
  state.conceptPackages = packages;
  const selectedJob =
    state.workshopJobs.find(x => x.workshopJobId === state.workshopJob?.workshopJobId) ||
    state.workshopJobs[0] ||
    null;
  state.workshopJob = selectedJob;
  renderWorkshopJobList(state.workshopJobs, selectedJob);
  renderWorkshopJob(selectedJob);

  const versions = packages?.versions || [];
  const selectedPackage =
    versions.find(x => x.conceptPackageVersionId === state.conceptPackage?.conceptPackageVersionId) ||
    versions.find(x => x.status === "PROPOSED") ||
    versions.find(x => x.conceptPackageVersionId === packages.currentApprovedConceptPackageVersionId) ||
    versions[0] ||
    null;
  state.conceptPackage = selectedPackage;
  renderConceptPackageList(packages, selectedPackage);
  await inspectConceptPackage(selectedPackage);
  renderWorkshopPrerequisite();
  setWorkshopControlsEnabled(canSubmitWorkshopJobs());
}

async function submitWorkshopJob() {
  if (!canSubmitWorkshopJobs() || state.busy) return;
  const objective = String(workshopObjective?.value || "").trim();
  const campaignGoal = String(workshopCampaignGoal?.value || "").trim();
  const audienceFocus = String(workshopAudienceFocus?.value || "").trim();
  const channelFormat = String(workshopChannelFormat?.value || "").trim();
  const cta = String(workshopCta?.value || "").trim();
  const deliverables = linesToList(workshopDeliverables?.value, 1, 6);
  const constraints = linesToList(workshopConstraints?.value, 0, 12);

  if (!objective || !campaignGoal || !audienceFocus || !channelFormat || !cta) {
    setTurnStatus("error", "Objective, campaign goal, audience focus, channel format, and CTA are required.");
    return;
  }
  if (!WORKSHOP_CHANNEL_FORMATS.includes(channelFormat)) {
    setTurnStatus("error", "Select a valid channel format enum value.");
    return;
  }
  if (!deliverables) {
    setTurnStatus("error", "Provide 1–6 non-empty deliverables, one per line.");
    return;
  }
  if (!constraints && String(workshopConstraints?.value || "").trim()) {
    setTurnStatus("error", "Constraints must be 0–12 non-empty lines.");
    return;
  }

  const payload = {
    objective,
    campaignGoal,
    audienceFocus,
    channelFormat,
    deliverables,
    cta,
    constraints: constraints || [],
    sourceSystem: SOURCE_SYSTEM,
    idempotencyKey: `workshop-${crypto.randomUUID()}`
  };

  state.busy = true;
  setWorkshopControlsEnabled(false);
  setTurnStatus("loading", "Submitting concept workshop job…");
  try {
    const job = await api(`/api/wedding-planner/workspaces/${state.workspaceId}/workshop-jobs`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    await refreshWorkshopLists();
    state.workshopJob = job;
    renderWorkshopJobList(state.workshopJobs, job);
    renderWorkshopJob(job);
    if (job.outputConceptPackageVersionId) {
      const versions = state.conceptPackages?.versions || [];
      const pkg = versions.find(x => x.conceptPackageVersionId === job.outputConceptPackageVersionId) || null;
      if (pkg) {
        state.conceptPackage = pkg;
        renderConceptPackageList(state.conceptPackages, pkg);
        await inspectConceptPackage(pkg);
      }
    }
    setTurnStatus(job.isReplay ? "replay" : "loading", workshopJobStatusMessage(job));
    if (!job.isReplay && job.status === "SUCCEEDED") {
      setTimeout(() => clearTurnStatus(), 4200);
    }
  } finally {
    state.busy = false;
    setWorkshopControlsEnabled(canSubmitWorkshopJobs());
  }
}

function workshopJobStatusMessage(job) {
  if (job.isReplay && job.status === "FAILED") {
    return "Replayed an existing FAILED workshop job. The failed job was returned unchanged — this is not a retry and providers were not called again.";
  }
  if (job.isReplay && job.status === "SUCCEEDED") {
    return "Replayed an existing SUCCEEDED workshop job. Existing job and concept package linkage returned with no provider or AI calls.";
  }
  if (job.isReplay) {
    return `Replayed an existing workshop job in status ${job.status}.`;
  }
  if (job.status === "FAILED") {
    return `Workshop job failed: ${job.errorMessage || job.errorCode || "unknown error"}. No concept package was created.`;
  }
  return `Workshop job ${job.status}. ${FOUR_TO_THREE_EXPLANATION}. Concept direction only — no generated image/assets.`;
}

async function decideConceptPackage(decision) {
  if (!state.liveChatEnabled || !state.conceptPackage || state.busy) return;
  const rationale = String(workshopRationale?.value || "").trim();
  if (!rationale) {
    setTurnStatus("error", "A rationale is required for concept package decisions.");
    return;
  }
  if (decision === "APPROVE" && !workshopConfirmApprove?.checked) {
    setTurnStatus("error", "Confirm the APPROVE checkbox before approving a concept package.");
    return;
  }

  const selectedRadio = workshopDecisionForm?.querySelector('input[name="selectedConceptId"]:checked');
  const selectedConceptId = selectedRadio ? String(selectedRadio.value || "").trim() : "";

  if (decision === "APPROVE") {
    if (!selectedConceptId || !["concept_1", "concept_2", "concept_3"].includes(selectedConceptId)) {
      setTurnStatus("error", "APPROVE requires a radio-selected concept id (concept_1, concept_2, or concept_3).");
      return;
    }
  }
  if (decision === "REJECT" && selectedConceptId) {
    setTurnStatus("error", "REJECT forbids concept selection. Clear the selected concept before rejecting.");
    return;
  }

  const payload = {
    decision,
    rationale,
    sourceSystem: SOURCE_SYSTEM,
    idempotencyKey: `workshop-decision-${crypto.randomUUID()}`
  };
  if (decision === "APPROVE") {
    payload.selectedConceptId = selectedConceptId;
  } else {
    payload.selectedConceptId = null;
  }

  state.busy = true;
  setWorkshopDecisionEnabled(false);
  setTurnStatus("loading", `Recording concept package ${decision} decision…`);
  try {
    const result = await api(`/api/wedding-planner/concept-packages/${state.conceptPackage.conceptPackageVersionId}/decisions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    state.workshopSelectedConceptId = result?.selectedConceptId || null;
    await refreshWorkshopLists();
    if (result?.version) {
      state.conceptPackage = result.version;
      renderConceptPackageList(state.conceptPackages, result.version);
      await inspectConceptPackage(result.version);
    }
    if (workshopRationale) workshopRationale.value = "";
    if (workshopConfirmApprove) workshopConfirmApprove.checked = false;
    clearWorkshopConceptSelection();
    setTurnStatus(
      result?.isReplay ? "replay" : "loading",
      result?.isReplay
        ? `Replayed concept package ${decision} decision.`
        : `${decision} recorded as concept-direction approval only — not campaign-ready, QA, matching, or legal/claim approval.`
    );
    if (!result?.isReplay) {
      setTimeout(() => clearTurnStatus(), 4200);
    }
  } finally {
    state.busy = false;
    setWorkshopControlsEnabled(canSubmitWorkshopJobs());
  }
}

function renderWorkshopJobList(jobs, selected) {
  if (!workshopJobSelect) return;
  if (!jobs.length) {
    workshopJobSelect.innerHTML = `<option value="">No workshop jobs yet</option>`;
    workshopJobSelect.disabled = true;
    return;
  }
  workshopJobSelect.disabled = false;
  workshopJobSelect.innerHTML = jobs.map(job => {
    const markers = [job.status || "UNKNOWN"];
    if (job.isReplay) markers.push("REPLAY");
    return `<option value="${escapeHtml(job.workshopJobId)}"${selected?.workshopJobId === job.workshopJobId ? " selected" : ""}>${escapeHtml(markers.join(" · "))} · ${escapeHtml(job.channelFormat || shortId(job.workshopJobId))}</option>`;
  }).join("");
}

function renderWorkshopJob(job) {
  if (!workshopJobMeta) return;
  if (!job) {
    workshopJobMeta.hidden = true;
    workshopJobMeta.innerHTML = "";
    return;
  }
  workshopJobMeta.hidden = false;
  workshopJobMeta.dataset.status = job.status || "";
  workshopJobMeta.innerHTML = `
    <p><strong>${escapeHtml(job.status || "UNKNOWN")}</strong>${job.isReplay ? " · replay" : ""} · ${escapeHtml(job.channelFormat || "—")} · canvas ${escapeHtml(String(job.canvasWidth ?? "—"))}×${escapeHtml(String(job.canvasHeight ?? "—"))}</p>
    <p>Objective: ${escapeHtml(job.objective || "—")}</p>
    <p>Campaign goal (planning text): ${escapeHtml(job.campaignGoal || "—")}</p>
    <p>Input SHA <code>${escapeHtml(job.inputSha256 || "—")}</code></p>
    <p>Provenance Brand DNA v${escapeHtml(String(job.approvedBrandDnaVersionNumber ?? "—"))} <code>${escapeHtml(shortId(job.approvedBrandDnaVersionId))}</code>
      · Color v${escapeHtml(String(job.approvedColorProfileVersionNumber ?? "—"))} <code>${escapeHtml(shortId(job.approvedColorProfileVersionId))}</code>
      · Research v${escapeHtml(String(job.approvedResearchReportVersionNumber ?? "—"))} <code>${escapeHtml(shortId(job.approvedResearchReportVersionId))}</code></p>
    <p>Package <code>${escapeHtml(job.outputConceptPackageVersionId || "—")}</code>${job.errorMessage ? ` · error: ${escapeHtml(job.errorMessage)}` : ""}</p>
  `;
}

function renderConceptPackageList(list, selected) {
  if (!workshopPackageSelect) return;
  const versions = list?.versions || [];
  const currentId = list?.currentApprovedConceptPackageVersionId || null;
  if (workshopCurrentPointer) {
    workshopCurrentPointer.hidden = !currentId;
  }
  if (!versions.length) {
    workshopPackageSelect.innerHTML = `<option value="">No concept packages yet</option>`;
    workshopPackageSelect.disabled = true;
    return;
  }
  workshopPackageSelect.disabled = false;
  workshopPackageSelect.innerHTML = versions.map(version => {
    const markers = [];
    if (version.status) markers.push(version.status);
    if (version.conceptPackageVersionId === currentId || version.isCurrentApproved) markers.push("CURRENT");
    return `<option value="${escapeHtml(version.conceptPackageVersionId)}"${selected?.conceptPackageVersionId === version.conceptPackageVersionId ? " selected" : ""}>v${escapeHtml(String(version.versionNumber))} · ${escapeHtml(markers.join(" · "))}</option>`;
  }).join("");
}

async function inspectConceptPackage(version) {
  if (!workshopPackageView) return;
  if (!version) {
    workshopPackageView.hidden = true;
    if (workshopDecisionForm) workshopDecisionForm.hidden = true;
    state.workshopContributions = [];
    state.workshopAgentRuns = [];
    state.workshopPinnedColorDocument = null;
    return;
  }

  workshopPackageView.hidden = false;
  const currentId = state.conceptPackages?.currentApprovedConceptPackageVersionId || null;
  const isCurrent = !!currentId && (currentId === version.conceptPackageVersionId || version.isCurrentApproved === true);

  if (workshopPackageStatusLabel) {
    workshopPackageStatusLabel.textContent = version.status || "UNKNOWN";
    workshopPackageStatusLabel.dataset.status = version.status || "";
  }
  if (workshopPackageCurrentBadge) workshopPackageCurrentBadge.hidden = !isCurrent;
  if (workshopPackageVersionNumber) workshopPackageVersionNumber.textContent = String(version.versionNumber ?? "—");
  if (workshopPackageSummaryText) workshopPackageSummaryText.textContent = version.summary || "No summary returned by the server.";
  if (workshopPackageSchema) workshopPackageSchema.textContent = version.schemaVersion || "—";
  if (workshopPackageChannel) workshopPackageChannel.textContent = version.channelFormat || "—";
  if (workshopPackageCanvas) {
    workshopPackageCanvas.textContent = `${version.canvasWidth ?? "—"}×${version.canvasHeight ?? "—"}`;
  }
  if (workshopPackageCost) workshopPackageCost.textContent = formatCost(version.estimatedTotalCostUsd);

  if (workshopSelectedConceptLine && workshopSelectedConceptIdEl) {
    const selectedId = state.workshopSelectedConceptId;
    workshopSelectedConceptLine.hidden = !selectedId;
    workshopSelectedConceptIdEl.textContent = selectedId || "—";
  }

  const document = parseConceptPackageDocument(version.documentJson);
  renderConceptPackageDocument(document, version);

  state.workshopContributions = [];
  state.workshopAgentRuns = [];
  try {
    const [contributions, runs] = await Promise.all([
      api(`/api/wedding-planner/concept-packages/${version.conceptPackageVersionId}/contributions`),
      api(`/api/wedding-planner/concept-packages/${version.conceptPackageVersionId}/agent-runs`)
    ]);
    state.workshopContributions = Array.isArray(contributions) ? contributions : [];
    state.workshopAgentRuns = Array.isArray(runs) ? runs : [];
  } catch {
    state.workshopContributions = [];
    state.workshopAgentRuns = [];
  }
  renderWorkshopContributions(state.workshopContributions, document);
  renderWorkshopAgentRuns(state.workshopAgentRuns);

  if (version.approvedColorProfileVersionId) {
    try {
      const colorVersion = await api(`/api/wedding-planner/color-profiles/${version.approvedColorProfileVersionId}`);
      state.workshopPinnedColorDocument = parseColorDocument(colorVersion?.documentJson);
    } catch {
      state.workshopPinnedColorDocument = null;
    }
  } else {
    state.workshopPinnedColorDocument = null;
  }

  if (document) {
    renderWorkshopConcepts(document, state.workshopPinnedColorDocument);
  }

  const canDecide = state.liveChatEnabled && version.status === "PROPOSED";
  if (workshopDecisionForm) workshopDecisionForm.hidden = !canDecide;
  setWorkshopDecisionEnabled(canDecide);
  syncWorkshopConceptRadioLabels(document);
}

function parseConceptPackageDocument(documentJson) {
  if (!documentJson) return null;
  try {
    return typeof documentJson === "string" ? JSON.parse(documentJson) : documentJson;
  } catch {
    return null;
  }
}

function renderConceptPackageDocument(document, version) {
  const disclaimer = document?.disclaimer || CONCEPT_PACKAGE_DISCLAIMER;
  if (workshopDisclaimerBlock && workshopDisclaimerText) {
    workshopDisclaimerText.textContent = disclaimer;
    workshopDisclaimerBlock.hidden = !disclaimer;
  }

  const marker = String(document?.marker || "");
  const hasSynthetic =
    marker === SYNTHETIC_DEVELOPMENT_PROTOTYPE ||
    documentJsonContainsSynthetic(document) ||
    String(version?.summary || "").includes(SYNTHETIC_DEVELOPMENT_PROTOTYPE);
  if (workshopSyntheticWarning) {
    workshopSyntheticWarning.hidden = !hasSynthetic;
  }
}

function documentJsonContainsSynthetic(document) {
  try {
    return JSON.stringify(document || {}).includes(SYNTHETIC_DEVELOPMENT_PROTOTYPE);
  } catch {
    return false;
  }
}

function renderWorkshopConcepts(document, colorDocument) {
  if (!workshopConcepts) return;
  const concepts = Array.isArray(document?.concepts) ? document.concepts : [];
  if (concepts.length !== 3) {
    workshopConcepts.innerHTML = `<p class="seed-hint">Expected exactly 3 concepts in server documentJson; found ${escapeHtml(String(concepts.length))}. No client concept invention.</p>`;
    return;
  }
  workshopConcepts.innerHTML = concepts.map(concept => {
    const copy = concept.copy || {};
    const claims = Array.isArray(concept.factualClaims) ? concept.factualClaims : [];
    const paletteRefs = Array.isArray(concept.paletteRoleRefs) ? concept.paletteRoleRefs : [];
    const preview = renderSafePrototypePreview(concept, colorDocument);
    return `
      <div class="workshop-concept-card" data-concept-id="${escapeHtml(concept.id || "")}">
        <strong>${escapeHtml(concept.id || "—")} · ${escapeHtml(concept.name || "—")}</strong>
        <span>Rationale: ${escapeHtml(concept.rationale || "—")}</span>
        <span>Visual direction: ${escapeHtml(concept.visualDirection || "—")}</span>
        <span>Palette role refs: <code>${escapeHtml(paletteRefs.join(", ") || "—")}</code></span>
        <span>Copy kind <code>${escapeHtml(copy.kind || "—")}</code></span>
        <span>Headline: ${escapeHtml(copy.headline || "—")}</span>
        <span>Body: ${escapeHtml(copy.body || "—")}</span>
        <span>CTA: ${escapeHtml(copy.cta || "—")}</span>
        ${claims.length ? claims.map(claim => `
          <div class="workshop-claim">
            <strong>Factual claim</strong>
            <span>${escapeHtml(claim.statement || "—")}</span>
            <span>Source IDs: <code>${escapeHtml((claim.sourceIds || []).join(", ") || "—")}</code></span>
          </div>`).join("") : `<span>Factual claims: none</span>`}
        ${preview}
      </div>`;
  }).join("");
}

function renderSafePrototypePreview(concept, colorDocument) {
  const prototype = concept?.prototype;
  if (!prototype || typeof prototype !== "object") {
    return `<p class="seed-hint">No prototype-spec.v1 in server documentJson for this concept.</p>`;
  }
  const template = String(prototype.template || "");
  if (!ALLOWED_PROTOTYPE_TEMPLATES.includes(template)) {
    return `<p class="seed-hint">Unsupported or missing prototype template. Safe renderer fails closed.</p>`;
  }
  const canvas = prototype.canvas || {};
  const canvasW = Number(canvas.width);
  const canvasH = Number(canvas.height);
  if (!Number.isFinite(canvasW) || !Number.isFinite(canvasH) || canvasW <= 0 || canvasH <= 0) {
    return `<p class="seed-hint">Invalid canvas bounds in prototype-spec. Fail closed.</p>`;
  }
  const regions = Array.isArray(prototype.regions) ? prototype.regions : [];
  if (regions.length < 2 || regions.length > 12) {
    return `<p class="seed-hint">Prototype regions must be 2–12 items from server JSON. Fail closed.</p>`;
  }

  const maxDisplay = 360;
  const scale = Math.min(1, maxDisplay / canvasW);
  const displayW = Math.round(canvasW * scale);
  const displayH = Math.round(canvasH * scale);
  const palette = colorDocument?.palette || {};
  const copy = concept.copy || {};

  const regionHtml = regions.map(region => {
    const bounds = sanitizeBounds(region?.bounds, canvasW, canvasH);
    if (!bounds) return "";
    const type = String(region.type || "");
    if (!ALLOWED_REGION_TYPES.includes(type)) return "";

    const left = Math.round(bounds.x * scale);
    const top = Math.round(bounds.y * scale);
    const width = Math.max(1, Math.round(bounds.w * scale));
    const height = Math.max(1, Math.round(bounds.h * scale));
    const color = resolvePaletteHex(palette, region.paletteRoleRef);
    const styleParts = [
      `left:${left}px`,
      `top:${top}px`,
      `width:${width}px`,
      `height:${height}px`
    ];
    if (color) styleParts.push(`background:${color}`);

    if (region.assetPlaceholder) {
      const kind = String(region.assetPlaceholder.kind || "");
      const label = String(region.assetPlaceholder.label || "Placeholder");
      if (!ALLOWED_PLACEHOLDER_KINDS.includes(kind)) {
        return `<div class="workshop-prototype-region" style="${styleParts.join(";")}"><div class="workshop-placeholder">INVALID PLACEHOLDER</div></div>`;
      }
      return `<div class="workshop-prototype-region" style="${styleParts.join(";")}" data-region-type="${escapeHtml(type)}"><div class="workshop-placeholder">${escapeHtml(kind)} · ${escapeHtml(label)}</div></div>`;
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

    return `<div class="workshop-prototype-region" style="${styleParts.join(";")}" data-region-type="${escapeHtml(type)}" data-template="${escapeHtml(template)}">${escapeHtml(text)}</div>`;
  }).filter(Boolean).join("");

  return `
    <div class="workshop-prototype-frame" data-template="${escapeHtml(template)}" style="width:${displayW}px;height:${displayH}px;max-width:100%;" role="img" aria-label="Low-fi prototype preview placeholder frame">
      ${regionHtml || `<div class="workshop-placeholder">No valid regions</div>`}
    </div>
    <p class="seed-hint">${escapeHtml(SYNTHETIC_DEVELOPMENT_PROTOTYPE)} · placeholders only · no &lt;img&gt; · concept direction only</p>`;
}

function sanitizeBounds(bounds, canvasW, canvasH) {
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

function resolvePaletteHex(palette, roleRef) {
  if (!roleRef) return null;
  const role = String(roleRef);
  const entry = palette?.[role];
  const hex = entry?.hex || (typeof entry === "string" ? entry : null);
  if (!hex || !/^#[0-9A-Fa-f]{6}$/.test(String(hex))) return null;
  return String(hex).toUpperCase();
}

function renderWorkshopContributions(contributions, document) {
  if (!workshopContributions) return;
  const fromApi = Array.isArray(contributions) ? contributions : [];
  const fromDoc = Array.isArray(document?.contributions) ? document.contributions : [];
  if (!fromApi.length && !fromDoc.length) {
    workshopContributions.innerHTML = `<p class="seed-hint">No role contributions returned. Expected exactly 4 durable roles.</p>`;
    return;
  }

  const ordered = WORKSHOP_LOGICAL_ROLES.map(role => {
    const apiRow = fromApi.find(x => x.logicalRole === role);
    const docRow = fromDoc.find(x => x.logicalRole === role);
    return { role, apiRow, docRow };
  });

  const recognized = ordered.filter(x => x.apiRow || x.docRow).length;
  if (recognized !== 4) {
    workshopContributions.innerHTML = `<p class="seed-hint">Expected exactly 4 role contributions; found ${escapeHtml(String(recognized))} recognized roles. No client concept invention.</p>`;
  }

  workshopContributions.innerHTML = ordered.map(({ role, apiRow, docRow }) => {
    let summary = docRow?.summary || "";
    if (apiRow?.contributionJson) {
      try {
        const parsed = typeof apiRow.contributionJson === "string"
          ? JSON.parse(apiRow.contributionJson)
          : apiRow.contributionJson;
        if (parsed?.summary) summary = parsed.summary;
      } catch {
        /* keep document summary */
      }
    }
    return `
      <div class="workshop-contribution-card">
        <strong>${escapeHtml(role)}</strong>
        <span>${escapeHtml(summary || "—")}</span>
        <span>Producing run <code>${escapeHtml(apiRow?.producingAgentRunId || "—")}</code></span>
      </div>`;
  }).join("");
}

function renderWorkshopAgentRuns(runs) {
  if (!workshopAgentRuns) return;
  if (!runs.length) {
    workshopAgentRuns.innerHTML = `<p class="seed-hint">No agent-run receipts returned for this package.</p>`;
    return;
  }
  workshopAgentRuns.innerHTML = `
    <p class="workshop-roles-note">${escapeHtml(FOUR_TO_THREE_EXPLANATION)}. Showing ${escapeHtml(String(runs.length))} receipt(s) from the package endpoint — exactly 3 workers expected, never 4 fake agent runs.</p>
    ${runs.map(run => `
      <div class="workshop-run-card">
        <strong>${escapeHtml(run.logicalRole || "—")} · ${escapeHtml(run.status || "—")}</strong>
        <span>Worker profile <code>${escapeHtml(run.workerProfileVersion || "—")}</code> · prompt <code>${escapeHtml(run.promptPackVersion || "—")}</code></span>
        <span>Assigned roles <code>${escapeHtml(run.assignedRolesJson || "—")}</code></span>
        <span>Tokens prompt/completion/total: ${escapeHtml(String(run.promptTokens ?? "—"))} / ${escapeHtml(String(run.completionTokens ?? "—"))} / ${escapeHtml(String(run.totalTokens ?? "—"))}</span>
        <span>Estimated cost USD <code>${escapeHtml(formatCost(run.estimatedCostUsd))}</code> · provider <code>${escapeHtml(run.providerKey || "—")}</code> · model <code>${escapeHtml(run.modelId || "—")}</code></span>
      </div>`).join("")}
  `;
}

function syncWorkshopConceptRadioLabels(packageDocument) {
  const concepts = Array.isArray(packageDocument?.concepts) ? packageDocument.concepts : [];
  workshopConceptSelect?.querySelectorAll('input[name="selectedConceptId"]').forEach(input => {
    const concept = concepts.find(c => c.id === input.value);
    const label = input.closest("label");
    if (!label) return;
    const name = concept?.name ? ` · ${concept.name}` : "";
    const textNode = Array.from(label.childNodes).find(node => node.nodeType === Node.TEXT_NODE && String(node.textContent || "").trim());
    if (textNode) {
      textNode.textContent = ` ${input.value}${name}`;
    } else {
      label.appendChild(window.document.createTextNode(` ${input.value}${name}`));
    }
  });
}

function clearWorkshopConceptSelection() {
  workshopDecisionForm?.querySelectorAll('input[name="selectedConceptId"]').forEach(input => {
    input.checked = false;
  });
}

function setWorkshopControlsEnabled(enabled) {
  const fields = [
    workshopObjective, workshopCampaignGoal, workshopAudienceFocus,
    workshopChannelFormat, workshopDeliverables, workshopCta, workshopConstraints
  ];
  fields.forEach(field => {
    if (field) field.disabled = !enabled;
  });
  if (workshopJobSubmit) workshopJobSubmit.disabled = !enabled;
  if (workshopRefreshLists) workshopRefreshLists.disabled = !state.liveChatEnabled || !state.workspaceId;
  if (workshopJobSelect) {
    const hasJobs = (state.workshopJobs || []).length > 0;
    workshopJobSelect.disabled = !state.liveChatEnabled || !hasJobs;
  }
  if (workshopPackageSelect) {
    const hasPackages = (state.conceptPackages?.versions || []).length > 0;
    workshopPackageSelect.disabled = !state.liveChatEnabled || !hasPackages;
  }
  setWorkshopDecisionEnabled(enabled && state.conceptPackage?.status === "PROPOSED");
}

function setWorkshopDecisionEnabled(enabled) {
  if (workshopRationale) workshopRationale.disabled = !enabled;
  if (workshopConfirmApprove) workshopConfirmApprove.disabled = !enabled;
  workshopConceptSelect?.querySelectorAll('input[name="selectedConceptId"]').forEach(input => {
    input.disabled = !enabled;
  });
  workshopDecisionForm?.querySelectorAll("button").forEach(button => {
    button.disabled = !enabled;
  });
}

function setSessionStatus(title, detail) {
  if (!statusEl) return;
  statusEl.innerHTML = `<span>${escapeHtml(title)}</span><p>${escapeHtml(detail)}</p>`;
}

function setTurnStatus(stateName, message) {
  if (!turnStatusEl) return;
  turnStatusEl.hidden = false;
  turnStatusEl.dataset.state = stateName;
  turnStatusEl.textContent = message;
}

function clearTurnStatus() {
  if (!turnStatusEl) return;
  turnStatusEl.hidden = true;
  turnStatusEl.textContent = "";
  delete turnStatusEl.dataset.state;
}

async function api(path, options = {}) {
  const method = String(options.method || "GET").toUpperCase();
  const headers = { Accept: "application/json", ...(options.headers || {}) };
  if (!["GET", "HEAD", "OPTIONS", "TRACE"].includes(method) && state.csrfToken) {
    headers["X-CSRF-TOKEN"] = state.csrfToken;
  }
  const response = await fetch(path, {
    ...options,
    headers,
    credentials: "same-origin"
  });
  const body = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    const error = new Error(body?.error || `Request failed with HTTP ${response.status}`);
    error.status = response.status;
    throw error;
  }
  return body;
}

function stableIdempotencyKey(kind, advertiserId) {
  const storageKey = `bliss-wp-${kind}-${advertiserId}`;
  let value = localStorage.getItem(storageKey);
  if (!value) {
    value = `${kind}-${crypto.randomUUID()}`;
    localStorage.setItem(storageKey, value);
  }
  return value;
}

function initials(value = "?") {
  return String(value).trim().split(/\s+/).slice(0, 2).map(part => part[0] || "").join("").toUpperCase() || "?";
}

function formatTime(value) {
  if (!value) return "";
  try {
    return new Intl.DateTimeFormat(undefined, { hour: "numeric", minute: "2-digit" }).format(new Date(value));
  } catch {
    return "";
  }
}

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, char => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    '"': "&quot;",
    "'": "&#039;"
  }[char]));
}
