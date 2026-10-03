async function loadModels() {
  const board = await api("/api/operations/models");
  $("#models-notice").textContent = "A closed-model reading stores that seven grooming models are not configured. Configured models stay at zero. Model calls stay at the stored count. No authorized traffic is on file. Production is not changed. This is not a traffic count. The learning page is unchanged. Green does not send. Delivery remains NOT_SENT.";
  $("#models-delivery").textContent = `Delivery ${board.delivery || "NOT_SENT"}. Configured models ${board.configuredModels ?? 0}. Model calls ${board.modelCalls ?? 0}. Notes ${board.noteCount ?? 0}.`;
  $("#models-laboratory").textContent = `Laboratory ${board.passedCount ?? 0} of ${board.scenarioCount ?? 0}. ${board.learningNotice || ""}`;
  const history = board.history || [];
  $("#models-history").innerHTML = history.length
    ? `<table><thead><tr><th>Reading</th><th>Configured</th><th>Model calls</th><th>Notes</th><th>Laboratory</th><th>Models configured</th><th>Traffic</th><th>Production changed</th><th>Delivery</th></tr></thead><tbody>${history.map(item =>
        `<tr><td>${escapeHtml(item.readingKey)}</td><td>${escapeHtml(String(item.configuredModels))}</td><td>${escapeHtml(String(item.modelCalls))}</td><td>${escapeHtml(String(item.noteCount))}</td><td>${escapeHtml(String(item.passedCount))} of ${escapeHtml(String(item.scenarioCount))}</td><td>${item.modelsConfigured ? "Yes" : "No"}</td><td>${item.authorizedTraffic ? "Yes" : "No"}</td><td>${item.productionChanged ? "Yes" : "No"}</td><td>${escapeHtml(item.delivery)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No closed-model reading is stored. A grooming model was not configured.");
  return board;
}

async function storeModels(configureModel) {
  const decision = await api("/api/operations/models", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ readingKey: "models-reading-1", configureModel })
  });
  $("#models-result").textContent = decision.duplicate
    ? "That closed-model reading is already stored. A grooming model was not configured. Delivery remains NOT_SENT."
    : (decision.notice || "Delivery remains NOT_SENT.");
  await loadModels();
}

document.addEventListener("DOMContentLoaded", () => {
  const store = document.getElementById("models-store");
  const configure = document.getElementById("models-configure");
  if (store) {
    store.addEventListener("click", async () => {
      store.disabled = true;
      try {
        await storeModels(false);
      } catch (error) {
        $("#models-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        store.disabled = false;
      }
    });
  }
  if (configure) {
    configure.addEventListener("click", async () => {
      configure.disabled = true;
      try {
        await storeModels(true);
      } catch (error) {
        $("#models-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        configure.disabled = false;
      }
    });
  }
});
