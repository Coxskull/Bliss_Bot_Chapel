const notice = document.querySelector("#notice");
const counts = document.querySelector("#counts");
const friction = document.querySelector("#friction");
const cost = document.querySelector("#cost");
const experiments = document.querySelector("#experiments");
const next = document.querySelector("#next");
const reading = document.querySelector("#reading");
const unchanged = document.querySelector("#unchanged");
const form = document.querySelector("#research-form");

function cell(text) {
  const item = document.createElement("td");
  item.textContent = text;
  return item;
}

async function load() {
  const response = await fetch("/api/demonstrations/mesa-norte/grooming");
  const report = await response.json();
  if (!response.ok) {
    notice.textContent = report.error || "The grooming report did not run.";
    return;
  }
  notice.textContent = report.notice + " AI calls " + report.aiCalls + ".";
  friction.textContent = report.friction;
  cost.textContent = report.cost;
  experiments.textContent = report.experiments;
  next.textContent = report.nextAction;
  counts.replaceChildren();
  (report.counts || []).forEach(item => {
    const row = document.createElement("tr");
    row.append(cell(item.kind), cell(String(item.count)));
    counts.append(row);
  });
}

form.addEventListener("submit", async event => {
  event.preventDefault();
  const excerpt = new FormData(form).get("excerpt");
  const response = await fetch("/api/demonstrations/mesa-norte/grooming/research", {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({ excerpt })
  });
  const body = await response.json();
  reading.textContent = response.ok
    ? body.provenance + " " + body.hypothesis + " " + body.notice + " AI calls " + body.aiCalls + "."
    : body.error;
  const prospect = await fetch("/api/demonstrations/mesa-norte");
  const current = await prospect.json();
  unchanged.textContent = current.businessName
    + " stays " + current.prospectState
    + ". Delivery remains " + current.delivery
    + ". Production was not changed.";
});

load();
