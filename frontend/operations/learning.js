async function loadLearning(prospect) {
  const query = prospect ? `?prospect=${encodeURIComponent(prospect)}` : "";
  const board = await api(`/api/operations/learning${query}`);
  $("#learning-notice").textContent = "A research note is appended only after the laboratory graduates. No authorized traffic is on file. The note does not change production. Research does not control production. A model is not the record. Seven grooming models are not configured. This is not a traffic count. Green does not send. Delivery remains NOT_SENT.";
  const scenarios = `${board.laboratoryPassedCount ?? 0} of ${board.laboratoryScenarios ?? 0}`;
  $("#learning-delivery").textContent = board.laboratoryPassed
    ? `Laboratory ${scenarios} graduated. Production was not changed. Delivery ${board.delivery || "NOT_SENT"}. Model calls ${board.modelCalls ?? 0}.`
    : `Laboratory ${scenarios} has not graduated. Production was not changed. Delivery ${board.delivery || "NOT_SENT"}.`;
  const prospects = board.prospects || [];
  const selected = board.prospect || prospect || prospects[0]?.slug || "";
  if (!board.prospect && selected && selected !== prospect) {
    return loadLearning(selected);
  }
  const select = document.getElementById("learning-prospect");
  if (select) {
    select.innerHTML = prospects.map(item =>
      `<option value="${escapeHtml(item.slug)}">${escapeHtml(item.businessName)}</option>`).join("");
    if (selected) select.value = selected;
  }
  const notes = board.notes || [];
  $("#learning-rows").innerHTML = notes.length
    ? `<table><thead><tr><th>Prospect</th><th>Research note</th><th>Laboratory</th><th>Authorized traffic</th><th>Production changed</th><th>Behavior changed</th><th>Model calls</th><th>Delivery</th></tr></thead><tbody>${notes.map(item =>
        `<tr><td>${escapeHtml(item.prospectSlug)}</td><td>${escapeHtml(item.body)}</td><td>${item.laboratoryGraduated ? "Graduated" : "Not graduated"}</td><td>${item.authorizedTraffic ? "Yes" : "No"}</td><td>${item.productionChanged ? "Yes" : "No"}</td><td>${item.behaviorChanged ? "Yes" : "No"}</td><td>${escapeHtml(String(item.modelCalls))}</td><td>${escapeHtml(item.delivery)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No research note is recorded for this prospect.");
  return board;
}

document.addEventListener("DOMContentLoaded", () => {
  const select = document.getElementById("learning-prospect");
  const button = document.getElementById("learning-append");
  if (select) {
    select.addEventListener("change", async () => {
      $("#learning-result").textContent = "";
      try {
        await loadLearning(select.value);
      } catch (error) {
        toast(error.message, true);
      }
    });
  }
  if (button) {
    button.addEventListener("click", async () => {
      const prospectSlug = document.getElementById("learning-prospect")?.value || "";
      button.disabled = true;
      try {
        const decision = await api("/api/operations/learning", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ prospectSlug, idempotencyKey: `learning-${prospectSlug}` })
        });
        $("#learning-result").textContent = decision.duplicate
          ? "That research note is already recorded. Production was not read again. Delivery remains NOT_SENT."
          : (decision.notice || "Delivery remains NOT_SENT.");
        await loadLearning(prospectSlug);
      } catch (error) {
        $("#learning-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        button.disabled = false;
      }
    });
  }
});
