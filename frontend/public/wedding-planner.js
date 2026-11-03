const menu = document.querySelector(".menu-button");
const nav = document.querySelector(".site-header nav");

menu?.addEventListener("click", () => {
  const open = nav?.classList.toggle("open") ?? false;
  menu.setAttribute("aria-expanded", String(open));
});

document.querySelectorAll(".site-header nav a").forEach(link => {
  link.addEventListener("click", () => {
    nav?.classList.remove("open");
    menu?.setAttribute("aria-expanded", "false");
  });
});

// Public, read-only guided Ask Alpha conversation. No network requests, tracking,
// contact capture, model provider, state persistence, or commercial commands.
const askAlphaInput = document.querySelector("#ask-alpha-input");
const askAlphaSend = document.querySelector("#ask-alpha-send");
const askAlphaThread = document.querySelector("#ask-alpha-thread");
let askAlphaTurns = 0;

function askAlphaAnswer(question) {
  const q = question.toLowerCase();
  const has = (...terms) => terms.some(term => q.includes(term));
  if (question.includes("@") || /(?:\\+?\\d[ -]?){8,}/.test(question)) {
    return "Please do not enter email addresses or phone numbers here. This informational guide does not collect leads or send messages. A verified contact form will be needed for a private follow-up.";
  }
  if (has("privacy", "personal", "store", "save", "data", "record", "transcript")) {
    return "This guided conversation runs inside your current browser tab. It does not send your questions to a server, save a transcript, or contact anyone. Do not enter private business or personal details.";
  }
  if (has("price", "pricing", "cost", "quote", "budget", "fee", "payment")) {
    return "Campaign pricing depends on an approved placement, audience, creative scope and an accepted quote. Ask Alpha cannot invent a price or collect payment. A human will review custom pricing when the commercial intake opens.";
  }
  if (has("human", "person", "representative", "contact", "call", "email", "support", "meeting")) {
    return "A human representative will be required for a custom proposal or binding agreement. This guide cannot book a call, submit your contact details, or promise a reply. Verified human-contact options will appear once the commercial intake is activated.";
  }
  if (has("creator", "podcaster", "streamer", "influencer", "channel", "earn", "monetiz")) {
    return "Alpha aims to connect suitable podcasters, livestreamers and content creators with advertisers. A creator's audience, content, availability and eligibility need review before any proposed campaign. Earnings or placements are not guaranteed.";
  }
  if (has("advertis", "sponsor", "brand", "campaign", "restaurant", "product", "business", "ad placement")) {
    return "Alpha helps brands explore relevant creator and podcast advertising placements. The process is discovery, fit review, creative planning and a separately approved campaign proposal. This page does not place or launch ads.";
  }
  if (has("result", "guarantee", "performance", "reach", "sales", "conversion")) {
    return "Campaign outcomes depend on the creator, audience and execution. Alpha does not guarantee sales, reach, conversions or a return on investment. Reported results must be supported by real measurement evidence.";
  }
  if (has("where", "city", "country", "philippine", "manila", "market", "location")) {
    return "Alpha researches opportunities across multiple markets. Availability, creator eligibility and campaign coverage are not confirmed by this guide. A specific market can be reviewed during a human-led proposal.";
  }
  if (has("what", "how", "work", "alpha", "help", "hello", "hi", "start", "join")) {
    return "Ask Alpha is the information guide for Alpha Global Media's advertiser–creator marketplace. We help explain partnerships, possible ad formats and human-reviewed next steps. You can ask about creators, advertiser campaigns, pricing or privacy.";
  }
  return "I can help with general questions about Alpha advertising, creator partnerships, pricing, campaign expectations and human assistance. For an individualized answer, a human review will be needed once intake is available.";
}

function appendAskAlphaMessage(who, body) {
  const bubble = document.createElement("p");
  bubble.className = "ask-alpha-bubble " + (who === "visitor" ? "ask-alpha-visitor" : "ask-alpha-guide");
  const heading = document.createElement("strong");
  heading.textContent = who === "visitor" ? "You" : "Ask Alpha · guided answer";
  const text = document.createElement("span");
  text.textContent = body;
  bubble.append(heading, text);
  askAlphaThread.append(bubble);
  askAlphaThread.scrollTop = askAlphaThread.scrollHeight;
}

function sendAskAlpha(question) {
  const cleaned = question.trim().slice(0, 400);
  if (!cleaned || askAlphaTurns >= 12) return;
  appendAskAlphaMessage("visitor", cleaned);
  appendAskAlphaMessage("guide", askAlphaAnswer(cleaned));
  askAlphaTurns++;
  askAlphaInput.value = "";
  if (askAlphaTurns >= 12) {
    askAlphaInput.disabled = true;
    askAlphaSend.disabled = true;
    appendAskAlphaMessage("guide", "This guided session has reached its 12-question limit. Reload the page to start a fresh informational session.");
  }
}

if (askAlphaInput && askAlphaSend && askAlphaThread) {
  askAlphaSend.addEventListener("click", () => sendAskAlpha(askAlphaInput.value));
  askAlphaInput.addEventListener("keydown", event => {
    if (event.key === "Enter") {
      event.preventDefault();
      sendAskAlpha(askAlphaInput.value);
    }
  });
  document.querySelectorAll("[data-ask-alpha-prompt]").forEach(button => {
    button.addEventListener("click", () => sendAskAlpha(button.dataset.askAlphaPrompt || ""));
  });
}
