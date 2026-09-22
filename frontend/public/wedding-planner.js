const SOURCE_SYSTEM = "PUBLIC_WEDDING_PLANNER";
const COLOR_INTELLIGENCE_LABEL = "PHASE 3 · DETERMINISTIC COLOR INTELLIGENCE";

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
    renderColorPrerequisite();
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
    renderAuthGate(session);
    renderColorPrerequisite();
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
  setComposerEnabled(true);
  setBrandDnaControlsEnabled(true);
  renderColorPrerequisite();
  setColorControlsEnabled(canComputeColorProfiles());
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
}

function hasCurrentApprovedBrandDna() {
  return !!state.brandDnaList?.currentApprovedBrandDnaVersionId;
}

function canComputeColorProfiles() {
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
