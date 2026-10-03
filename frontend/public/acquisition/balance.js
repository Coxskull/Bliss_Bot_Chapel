const notice = document.querySelector("#notice");
const counts = document.querySelector("#counts");
const pressure = document.querySelector("#pressure");
const revenue = document.querySelector("#revenue");
const inventory = document.querySelector("#inventory");
const skipped = document.querySelector("#skipped");
const rows = document.querySelector("#rows");
const prospects = document.querySelector("#prospects");

function cell(text) {
  const item = document.createElement("td");
  item.textContent = text;
  return item;
}

function addRow(side, name) {
  const row = document.createElement("tr");
  row.append(cell(side), cell(name));
  rows.append(row);
}

async function prospectNames() {
  const response = await fetch("/api/demonstrations/library");
  const library = await response.json();
  return (library.demonstrations || []).map(item => item.businessName).filter(Boolean).sort();
}

async function load() {
  const before = await prospectNames();
  const response = await fetch("/api/demonstrations/balance");
  const board = await response.json();
  notice.textContent = board.notice || "";
  counts.textContent = board.counts || "";
  pressure.textContent = board.pressure || "";
  revenue.textContent = board.revenue || "";
  inventory.textContent = board.inventory || "";
  skipped.textContent = board.skipped || "";
  rows.replaceChildren();
  (board.advertisers || []).forEach(name => addRow("Advertiser", name));
  (board.creators || []).forEach(name => addRow("Creator", name));
  const after = await prospectNames();
  const same = before.join("|") === after.join("|");
  prospects.textContent = (after.join(", ") || "No stored prospect") +
    (same ? " remain. The stored prospects are unchanged. Delivery remains NOT_SENT." : " changed.");
}

load();
