const notice = document.querySelector("#notice");
const stored = document.querySelector("#stored");
const rotation = document.querySelector("#rotation");
const contents = document.querySelector("#contents");
const pairResult = document.querySelector("#pair-result");
const stackResult = document.querySelector("#stack-result");
const unchanged = document.querySelector("#unchanged");
const form = document.querySelector("#inventory-form");

async function readBoard() {
  const response = await fetch("/api/demonstrations/creative-inventory");
  return response.json();
}

function showBoard(board) {
  notice.textContent = board.notice || "";
  stored.textContent = board.stored || "";
  rotation.textContent = board.rotation || "";
  contents.textContent = (board.contents || []).map(item => item.line).join(" ");
}

async function post(body) {
  const response = await fetch("/api/demonstrations/creative-inventory", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });
  const payload = await response.json();
  if (!response.ok) {
    throw new Error(payload.error || "The pair was refused.");
  }
  return payload;
}

form.addEventListener("submit", async event => {
  event.preventDefault();
  const data = new FormData(form);
  const before = await readBoard();
  const decision = await post({
    pair: Number(data.get("pair")),
    creatorApproved: data.get("creatorApproved") === "yes",
    stack: false
  });
  const after = await readBoard();
  pairResult.textContent = decision.result + " " + decision.notice;
  unchanged.textContent = before.slotCount === after.slotCount
    ? "Stored slots remain " + after.slotCount + ". The stored slots are unchanged. Delivery remains NOT_SENT."
    : "The stored slots changed.";
});

document.querySelector("#stack-button").addEventListener("click", async () => {
  const before = await readBoard();
  const decision = await post({ stack: true });
  const after = await readBoard();
  stackResult.textContent = decision.result + " " + decision.notice;
  unchanged.textContent = before.slotCount === after.slotCount
    ? "Stored slots remain " + after.slotCount + ". The stored slots are unchanged. Delivery remains NOT_SENT."
    : "The stored slots changed.";
});

readBoard().then(showBoard);
