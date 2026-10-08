async function loadMissionControl(readyMessage) {
  const reading = await api("/api/operations/mission-control/evidence");
  const target = document.getElementById("mission-control-packages");
  const notice = document.getElementById("mission-control-notice");
  if (!target || !notice) return;
  const status = "Folder " + (reading.folder || "UNRECORDED") + ". Drive " + (reading.driveStatus || "NOT_CONNECTED") + ". ChatGPT retrieval " + (reading.chatgptRetrieval || "NOT_RUN") + ".";
  notice.textContent = readyMessage ? readyMessage + " " + status : status;
  const packages = reading.packages || [];
  target.innerHTML = packages.length
    ? `<table><thead><tr><th>Evidence ID</th><th>Test</th><th>System Area</th><th>Submitted</th><th>Current Result</th><th>Correction Required</th><th>Retest Required</th><th>Owner Decision</th><th>Reviewer</th><th>Age</th><th>Cost</th><th>Linked Parent/Retest</th><th>Evidence Location</th></tr></thead><tbody>${packages.map(row).join("")}</tbody></table>`
    : "<p>No evidence package is stored.</p>";
}

function row(item) {
  const result = item.finalReviewResult || "NOT_REVIEWED";
  const submitted = item.submittedAt ? `${item.submittedBy} ${item.submittedAt}` : (item.submittedBy || "UNRECORDED");
  return `<tr><td><code>${escapeHtml(item.evidenceId)}</code></td><td>${escapeHtml(item.testName)}</td><td>${escapeHtml(item.testCategory)}</td><td>${escapeHtml(submitted)}</td><td>${escapeHtml(result)}</td><td>${result === "CORRECTION_REQUIRED" ? "YES" : "NO"}</td><td>${result === "RETEST_REQUIRED" ? "YES" : "NO"}</td><td>${item.ownerDecisionRequired ? "YES" : "NO"}</td><td>${escapeHtml(item.reviewer || "—")}</td><td>${escapeHtml(age(item.createdAt))}</td><td>${escapeHtml(item.costStatus || "UNRECORDED")}</td><td>${escapeHtml(item.parentEvidenceId || "—")}</td><td>${escapeHtml(item.driveLocation || "UNRECORDED")}</td></tr>`;
}

function age(createdAt) {
  if (!createdAt) return "UNRECORDED";
  const started = Date.parse(createdAt);
  if (Number.isNaN(started)) return "UNRECORDED";
  const minutes = Math.max(0, Math.floor((Date.now() - started) / 60000));
  if (minutes < 1) return "under 1 minute";
  if (minutes < 60) return minutes + " min";
  const hours = Math.floor(minutes / 60);
  return hours + " h " + (minutes % 60) + " min";
}

document.addEventListener("DOMContentLoaded", () => {
  bind("mission-control-probe", "/api/operations/mission-control/retrieval-probe");
  bind("mission-control-validation", "/api/operations/mission-control/validation-catalog/run");
});

function bind(id, path) {
  const button = document.getElementById(id);
  if (!button) return;
  button.addEventListener("click", async () => {
    button.disabled = true;
    try {
      const result = await api(path, { method: "POST", headers: { "Content-Type": "application/json" }, body: "{}" });
      await loadMissionControl(result.message || "Evidence id issued.");
    } catch (error) {
      toast(error.message, true);
    } finally {
      button.disabled = false;
    }
  });
}
