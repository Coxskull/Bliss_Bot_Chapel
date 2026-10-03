const notice = document.querySelector("#notice");
const period = document.querySelector("#period");
const counts = document.querySelector("#counts");
const abundance = document.querySelector("#abundance");
const names = document.querySelector("#names");
const result = document.querySelector("#result");
const unchanged = document.querySelector("#unchanged");
const form = document.querySelector("#rotation-form");

function show(board) {
  notice.textContent = board.notice || "";
  period.textContent = board.period || "";
  counts.textContent = board.counts || "";
  abundance.textContent = board.abundance || "";
  const stored = board.advertisers || [];
  names.textContent = stored.length
    ? "Stored advertisers: " + stored.join(", ") + "."
    : "No stored advertiser is on file.";
  if (board.result) result.textContent = board.result;
}

async function readBoard() {
  const response = await fetch("/api/demonstrations/rotation");
  return response.json();
}

form.addEventListener("submit", async event => {
  event.preventDefault();
  const before = await readBoard();
  const data = new FormData(form);
  const response = await fetch("/api/demonstrations/rotation", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      pair: Number(data.get("pair")),
      creatorApproved: data.get("creatorApproved") === "yes"
    })
  });
  const board = await response.json();
  if (!response.ok) {
    result.textContent = board.error || "The rotation was refused.";
    return;
  }
  show(board);
  result.textContent = board.result + " " + board.notice;
  const after = await readBoard();
  unchanged.textContent = before.slotCount === after.slotCount
    ? "Stored slots remain " + after.slotCount + ". The stored slots are unchanged. Delivery remains NOT_SENT."
    : "The stored slots changed.";
});

readBoard().then(show);
