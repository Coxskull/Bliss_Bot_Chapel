const SOURCE_SYSTEM = "PUBLIC_WEDDING_PLANNER";

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
    renderAuthGate(session);
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
  setComposerEnabled(true);
  setBrandDnaControlsEnabled(true);
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
  }
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
