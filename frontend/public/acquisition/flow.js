const notice = document.querySelector("#notice");
const capacityNotice = document.querySelector("#capacity-notice");
const rows = document.querySelector("#rows");
const unchanged = document.querySelector("#unchanged");
const form = document.querySelector("#flow-form");

function cell(text) {
  const item = document.createElement("td");
  item.textContent = text;
  return item;
}

async function names() {
  const response = await fetch("/api/demonstrations/library");
  const library = await response.json();
  return (library.demonstrations || []).map(item => item.businessName).filter(Boolean).sort();
}

form.addEventListener("submit", async event => {
  event.preventDefault();
  const before = await names();
  const capacity = Number(new FormData(form).get("capacity"));
  const response = await fetch("/api/demonstrations/flow", {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({ capacity })
  });
  const board = await response.json();
  if (!response.ok) {
    notice.textContent = board.error || "The flow was not regulated.";
    return;
  }
  notice.textContent = board.notice + " Preserved " + board.preserved + ". Released " + board.released + ". Held " + board.held + ". Withheld " + board.withheld + ". Discarded " + board.discarded + ".";
  capacityNotice.textContent = board.capacityNotice;
  rows.replaceChildren();
  (board.assignments || []).forEach(item => {
    const row = document.createElement("tr");
    row.append(cell(item.businessName), cell(item.label));
    rows.append(row);
  });
  const after = await names();
  unchanged.textContent = after.join(", ") + " remain. None was discarded. Delivery remains " + board.delivery + ".";
  unchanged.textContent += before.join("|") === after.join("|")
    ? " The stored prospects are unchanged."
    : "";
});

names().then(stored => {
  notice.textContent = stored.length
    ? stored.join(", ") + " are already stored. A downstream opening count regulates flow. None is discarded. Green does not send. Delivery remains NOT_SENT."
    : "No prospects are stored. None is invented.";
});
