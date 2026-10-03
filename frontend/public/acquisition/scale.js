const notice = document.querySelector("#notice");
const rungs = document.querySelector("#rungs");
const cost = document.querySelector("#cost");
const factory = document.querySelector("#factory");
const unchanged = document.querySelector("#unchanged");

function cell(text) {
  const item = document.createElement("td");
  item.textContent = text;
  return item;
}

async function load() {
  const response = await fetch("/api/demonstrations/scale-proof");
  const report = await response.json();
  if (!response.ok) {
    notice.textContent = report.error || "The scale proof did not run.";
    return;
  }
  notice.textContent = report.notice + " AI calls " + report.aiCalls + ".";
  cost.textContent = report.cost;
  factory.textContent = report.factoryTarget;
  rungs.replaceChildren();
  (report.rungs || []).forEach(item => {
    const row = document.createElement("tr");
    row.append(
      cell(String(item.target)),
      cell(String(item.measured)),
      cell(item.passed ? "Passed" : "Failed"),
      cell(item.claimed ? "Claimed" : "Not claimed"),
      cell(String(item.elapsedMilliseconds)));
    rungs.append(row);
  });
  const prospect = await fetch("/api/demonstrations/mesa-norte");
  const current = await prospect.json();
  unchanged.textContent = current.businessName
    + " stays " + current.prospectState
    + ". Delivery remains " + current.delivery
    + ". Production was not changed.";
}

load();
