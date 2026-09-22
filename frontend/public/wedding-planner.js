const SOURCE_SYSTEM = "PUBLIC_WEDDING_PLANNER";
const COLOR_INTELLIGENCE_LABEL = "PHASE 3 · DETERMINISTIC COLOR INTELLIGENCE";
const CURATOR_LABEL = "PHASE 4 · THE CURATOR";
const RESEARCH_DISCLAIMER =
  "Approval of this report is research approval only. It is not creative, campaign, claim, legal, matching, accessibility, or compliance approval. Source verification checks metadata and internal consistency only; live URL content is not fetched or certified.";
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
const RESEARCH_JOB_STATUSES = ["RUNNING", "SUCCEEDED", "FAILED"];
const RESEARCH_REPORT_STATUSES = ["PROPOSED", "APPROVED", "REJECTED", "SUPERSEDED", "CURRENT"];
const EIGHT_TO_THREE_EXPLANATION =
  "Eight logical roles map to 3 workers/runs, not 8 subscriptions";

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
    renderColorPrerequisite();
    renderCuratorPrerequisite();
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
    renderAuthGate(session);
    renderColorPrerequisite();
    renderCuratorPrerequisite();
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
  setComposerEnabled(true);
  setBrandDnaControlsEnabled(true);
  renderColorPrerequisite();
  setColorControlsEnabled(canComputeColorProfiles());
  renderCuratorPrerequisite();
  setCuratorControlsEnabled(canSubmitResearchJobs());
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
