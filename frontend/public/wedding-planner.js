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

// Existing guide is preserved. AI is enabled ONLY by an explicit Vercel
// release flag, explicit per-visitor processing consent, and a separately
// enabled server-side Supabase budget/receipt gateway. Never touch CRM here.
const askAlphaInput = document.querySelector("#ask-alpha-input");
const askAlphaSend = document.querySelector("#ask-alpha-send");
const askAlphaThread = document.querySelector("#ask-alpha-thread");
let askAlphaTurns = 0;
const askAlphaLive = document.querySelector("#ask-alpha")?.dataset.askAlphaAi === "true";
const askAlphaAiPanel = document.querySelector("#ask-alpha-ai-consent-panel");
const askAlphaAiConsent = document.querySelector("#ask-alpha-ai-consent");
const askAlphaMarketingConsent = document.querySelector("#ask-alpha-marketing-consent");
const askAlphaAiStatus = document.querySelector("#ask-alpha-ai-status");
const askAlphaSessionId = window.crypto?.randomUUID?.() ?? null;
const askAlphaHistory = [];
let askAlphaPending = false;
if (askAlphaLive && askAlphaAiPanel && askAlphaAiConsent) {
  askAlphaAiPanel.hidden = false;
  if (askAlphaSend) askAlphaSend.textContent = "Ask AI";
}


function askAlphaAnswer(question) {
  const q = question.toLowerCase();
  const has = (...terms) => terms.some(term => q.includes(term));
  if (question.includes("@") || /(?:\+?\d[ -]?){8,}/.test(question)) {
    return "Please do not enter email addresses or phone numbers here. This informational guide does not collect leads or send messages. A verified contact form will be needed for a private follow-up.";
  }
  if (has("privacy", "personal", "store", "save", "data", "record", "transcript")) {
    return "This guided conversation runs inside your current browser tab. It does not send your questions to a server, save a transcript, or contact anyone. Do not enter private business or personal details.";
  }
  if (has("price", "pricing", "cost", "quote", "budget", "fee", "payment")) {
    return "To request a standalone advertising plan, submit the advertiser inquiry with your all-in budget and currency, objective, market, preferred media and language. A human must confirm the planning deliverables, delivery capacity, price, currency and applicable terms in writing before paid work begins. This guide cannot issue a binding quote or collect payment. The inquiry form does not reserve inventory or promise a creator placement.";
  }
  if (has("human", "person", "representative", "contact", "call", "email", "support", "meeting")) {
    return "For a custom advertising-planning request, use the advertiser inquiry form. It records an inquiry for human review; it is not a purchase, support ticket or marketing opt-in, and response timing is not guaranteed.";
  }
  if (has("creator", "podcaster", "streamer", "influencer", "channel", "earn", "monetiz")) {
    return "Alpha welcomes emerging creators through established personalities and major media, including podcasts, livestreams, video, blogs, radio, television, newspapers and magazines. There is no follower-count minimum in this guide. Audience evidence, content suitability, rights and availability must be reviewed before a match; creator compensation is agreed voluntarily and is never assumed to be discounted. A placement or earning is not guaranteed.";
  }
  if (has("advertis", "sponsor", "brand", "campaign", "restaurant", "product", "business", "ad placement")) {
    return "Alpha accepts advertiser inquiries for human review. Use the advertiser form to describe your budget, objective, target market, preferred media, audience requirements and language. A planning proposal depends on confirmed scope, delivery capacity, costs, price, currency and written terms. This page does not book inventory or place or launch ads.";
  }
  if (has("result", "guarantee", "performance", "reach", "sales", "conversion")) {
    return "Campaign outcomes depend on the creator, audience and execution. Alpha does not guarantee sales, reach, conversions or a return on investment. Reported results must be supported by real measurement evidence.";
  }
  if (has("where", "city", "country", "philippine", "manila", "market", "location")) {
    return "Alpha’s launch markets include Santo Domingo, Panama City, Medellín, Manila, Kuala Lumpur and Jakarta. A proposal should use the local market, currency, audience evidence and the advertiser’s preferred language and communication style. This guide cannot confirm current availability or provide a translated human follow-up; those require the connected intake and local review.";
  }
  if (has("what", "how", "work", "alpha", "help", "hello", "hi", "start", "join")) {
    return "Ask Ask Alpha is a general information guide. For an advertising-planning request, use the public advertiser inquiry form; it does not purchase a service or promise a response. You can ask general questions about advertising, creators, pricing or privacy here.";
  }
  return "I can help with general questions about Alpha advertising, creator partnerships, pricing, campaign expectations and human assistance. For an individualized answer, a human review will be needed once intake is available.";
}

function appendAskAlphaMessage(who, body) {
  const bubble = document.createElement("p");
  bubble.className = "ask-alpha-bubble " + (who === "visitor" ? "ask-alpha-visitor" : "ask-alpha-guide");
  const heading = document.createElement("strong");
  heading.textContent = who === "visitor" ? "You" :
    who === "ai" ? "Ask Alpha · AI response (informational)" : "Ask Alpha · guided answer";
  const text = document.createElement("span");
  text.textContent = body;
  bubble.append(heading, text);
  askAlphaThread.append(bubble);
  askAlphaThread.scrollTop = askAlphaThread.scrollHeight;
}

async function sendAskAlpha(question) {
  const cleaned = String(question || "").trim().slice(0,400);
  if (!cleaned || askAlphaTurns >= 12 || askAlphaPending) return;
  // Never send contact data into an informal chat: public forms handle it.
  if (cleaned.includes("@") || /(?:\+?\d[ -]?){8,}/.test(cleaned) ||
      /\b(?:password|passcode|one[- ]?time(?: code| password)?|otp|cvv|cvc|pin|secret(?: key)?|api[- ]?key)\s*[:=]\s*\S{2,}/i.test(cleaned)) {
    appendAskAlphaMessage("guide",
      "Please avoid personal contact or payment details in chat. Use the appropriate public inquiry form.");
    askAlphaInput.value = "";
    return;
  }
  appendAskAlphaMessage("visitor",cleaned);
  askAlphaInput.value = "";
  askAlphaTurns++;
  if (!askAlphaLive || !askAlphaAiConsent?.checked || !askAlphaSessionId) {
    appendAskAlphaMessage("guide",askAlphaAnswer(cleaned));
    if (askAlphaLive && askAlphaAiStatus) {
      askAlphaAiStatus.textContent = "No AI request made. You can use guided help without agreeing to AI processing.";
    }
  } else {
    askAlphaPending = true;
    askAlphaSend.disabled = true;
    askAlphaInput.disabled = true;
    const q = cleaned.toLowerCase();
    const audience = /support|refund|dispute|complaint|problem|privacy/.test(q) ? "support" :
      /creator|podcast|livestream|influenc|register/.test(q) ? "creator" :
      /advertis|brand|campaign|sponsor|proposal/.test(q) ? "advertiser" : "general";
    try {
      // Reuse the exact established public-intake Supabase function, not an
      // additional backend. No API key, authorization, or user PII stored here.
      const response = await fetch(
        "https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-public-intake",{
          method:"POST",mode:"cors",credentials:"omit",cache:"no-store",
          headers:{"Content-Type":"application/json"},
          body:JSON.stringify({
            action:"ask_alpha",notice_version:"AGM-ASK-ALPHA-AI-2026-11-08",
            ai_processing_consent:true,
            marketing_opt_in:askAlphaMarketingConsent?.checked === true,
            session_id:askAlphaSessionId,request_key:crypto.randomUUID(),
            audience,message:cleaned,history:askAlphaHistory.slice(-4)
          }),signal:AbortSignal.timeout(20000)
        }
      );
      const result = await response.json();
      if (response.ok && result?.status === "ANSWERED" &&
          typeof result.answer === "string" &&
          /^[0-9a-f-]{36}$/i.test(result.receipt_id || "")) {
        appendAskAlphaMessage("ai",result.answer);
        askAlphaHistory.push({role:"user",content:cleaned},
          {role:"assistant",content:result.answer.slice(0,400)});
        while (askAlphaHistory.length > 4) askAlphaHistory.shift();
        if (askAlphaAiStatus) askAlphaAiStatus.textContent =
          "AI request recorded: " + result.receipt_id + ". Not an official support case or approved proposal.";
      } else {
        appendAskAlphaMessage("guide",
          "AI could not verify an answer or receipt. No commercial action was taken. You can use the public interest or contact forms.");
        if (askAlphaAiStatus) askAlphaAiStatus.textContent =
          "AI unavailable (" + (result?.error || response.status) + "). The scripted guide remains available.";
      }
    } catch {
      appendAskAlphaMessage("guide",
        "The AI reply was not confirmed. Please use the public inquiry/contact forms for human review.");
      if (askAlphaAiStatus) askAlphaAiStatus.textContent =
        "Unable to verify the AI receipt. No automatic retry or submission was made.";
    } finally {
      askAlphaPending = false;
      askAlphaSend.disabled = askAlphaTurns >= 12;
      askAlphaInput.disabled = askAlphaTurns >= 12;
    }
  }
  if (askAlphaTurns >= 12) {
    askAlphaInput.disabled = true;
    askAlphaSend.disabled = true;
    appendAskAlphaMessage("guide",
      "This tab reached its 12-question limit. A new session requires a page reload.");
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
