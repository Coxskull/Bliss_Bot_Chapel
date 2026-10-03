const notice = document.querySelector("#notice");
const rows = document.querySelector("#rows");

function cell(text) {
  const item = document.createElement("td");
  item.textContent = text;
  return item;
}

async function load() {
  const response = await fetch("/api/demonstrations/conversation-laboratory");
  const report = await response.json();
  if (!response.ok) {
    notice.textContent = report.error || "The laboratory did not run.";
    return;
  }
  notice.textContent = report.voice + ". " + report.passedCount + " of " + report.scenarioCount
    + " scenarios passed. " + report.notice + " AI calls " + report.aiCalls + ".";
  rows.replaceChildren();
  report.scenarios.forEach(scenario => {
    const row = document.createElement("tr");
    row.append(
      cell(scenario.persona),
      cell(scenario.prompt),
      cell(scenario.passed ? "Passed" : "Failed"),
      cell(scenario.reply));
    rows.append(row);
  });
}

load();
