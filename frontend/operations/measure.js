async function loadMeasure() {
  const board = await api("/api/operations/measure");
  $("#measure-notice").textContent = board.notice || "";
  $("#measure-delivery").textContent = `Delivery ${board.delivery || "NOT_SENT"}. Green does not send.`;
  const latest = board.latest;
  const facts = latest
    ? [
        ["Stored prospects", latest.storedProspects],
        ["Elapsed milliseconds", latest.elapsedMilliseconds],
        ["Working set", latest.resourceLine],
        ["Cost", latest.costLine],
        ["Retries", latest.retries],
        ["Partial failures", latest.partialFailures],
        ["Recovered", latest.recovered ? "Yes. The other prospects remain." : "No"],
        ["Leakage", latest.leakage ? "Yes. A message names another prospect." : "No"],
        ["Hosted acceptance claimed", latest.hostedAcceptanceClaimed ? "Yes" : "No"],
        ["Factory target claimed", latest.factoryTargetClaimed ? "Yes" : "No"],
        ["Census claimed", latest.censusClaimed ? "Yes" : "No. This is not a stored census."]
      ]
    : [];
  $("#measure-facts").innerHTML = facts.length
    ? `<table><thead><tr><th>Reading</th><th>Measured value</th></tr></thead><tbody>${facts.map(([label, value]) =>
        `<tr><td>${escapeHtml(label)}</td><td>${escapeHtml(String(value))}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No local batch is measured. Hosted acceptance is not claimed. None was invented.");
  const failures = latest?.failures || [];
  $("#measure-failures").innerHTML = failures.length
    ? `<table><thead><tr><th>Failure</th></tr></thead><tbody>${failures.map(item =>
        `<tr><td>${escapeHtml(item)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No partial failure or leaked name is recorded.");
  const rows = board.measurements || [];
  $("#measure-rows").innerHTML = rows.length
    ? `<table><thead><tr><th>Key</th><th>Stored</th><th>Elapsed ms</th><th>Retries</th><th>Leakage</th><th>Delivery</th></tr></thead><tbody>${rows.map(item =>
        `<tr><td>${escapeHtml(item.idempotencyKey)}</td><td>${escapeHtml(String(item.storedProspects))}</td><td>${escapeHtml(String(item.elapsedMilliseconds))}</td><td>${escapeHtml(String(item.retries))}</td><td>${item.leakage ? "Yes" : "No"}</td><td>${escapeHtml(item.delivery)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No measurement row is stored.");
}

async function submitMeasure(attempt, key) {
  const decision = await api("/api/operations/measure", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ idempotencyKey: key, attempt })
  });
  $("#measure-result").textContent = decision.duplicate
    ? "That measurement is already recorded. The clock was not read again. Delivery remains NOT_SENT."
    : "The local database was measured. Hosted acceptance is not claimed. Delivery remains NOT_SENT.";
  await loadMeasure();
}

document.addEventListener("DOMContentLoaded", () => {
  const once = document.getElementById("measure-once");
  const retry = document.getElementById("measure-retry");
  if (once) {
    once.addEventListener("click", async () => {
      once.disabled = true;
      try {
        await submitMeasure(1, "local-batch-1");
      } catch (error) {
        $("#measure-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        once.disabled = false;
      }
    });
  }
  if (retry) {
    retry.addEventListener("click", async () => {
      retry.disabled = true;
      try {
        await submitMeasure(2, "local-batch-retry-1");
      } catch (error) {
        $("#measure-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        retry.disabled = false;
      }
    });
  }
});
