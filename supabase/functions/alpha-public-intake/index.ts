import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2.95.0";
import { deliverToGhl } from "./ghl-sync.mjs";
import { supportNotice, supportCategories, supportCrmEligible } from "./support-contract.mjs";

// Public interest requests are not account enrollment, advertiser purchases,
// creator verification, or a pathway into protected Railway workspaces.
// All writes use a server-side credential, never shipped to browser code.
const allowed = new Set([
  "https://alphacreativecontent.com",
  "https://www.alphacreativecontent.com",
  "https://bliss-bot.vercel.app"
]);
const notice = "AGM-INTEREST-2026-11-08-GHL-01";
const supportedNotices = new Set(["AGM-INTEREST-2026-11-05", notice, supportNotice]);
const jsonHeaders = { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" };
const out = (origin: string | null, payload: unknown, status = 200): Response => {
  const headers = new Headers(jsonHeaders);
  if (origin && allowed.has(origin)) {
    headers.set("Access-Control-Allow-Origin", origin);
    headers.set("Vary", "Origin");
  }
  return new Response(JSON.stringify(payload), { status, headers });
};
const trimmed = (value: unknown, max: number) =>
  typeof value === "string" && value.length <= max * 2 ? value.trim() : null;
const validEmail = (email: string): boolean =>
  email.length <= 254 && /^[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}$/i.test(email);
const validUrl = (url: string): boolean => {
  if (url.length > 500) return false;
  try {
    const parsed = new URL(url);
    return parsed.protocol === "https:" && !!parsed.hostname.includes(".")
      && !parsed.username && !parsed.password
      && !/^(localhost|127\.|10\.|192\.168\.|169\.254\.)/i.test(parsed.hostname);
  } catch { return false; }
};
const validUuid = (value: unknown): value is string =>
  typeof value === "string" && /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);

Deno.serve(async (req: Request) => {
  const origin = req.headers.get("Origin");
  if (!origin || !allowed.has(origin)) return out(null, { error: "This form is available from ALPHA's official website only." }, 403);
  if (req.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: {
      "Access-Control-Allow-Origin": origin,
      "Access-Control-Allow-Methods": "POST, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type",
      "Access-Control-Max-Age": "600",
      "Vary": "Origin"
    } });
  }
  if (req.method !== "POST") return out(origin, { error: "Method not allowed" }, 405);
  if (!(req.headers.get("Content-Type") || "").toLowerCase().startsWith("application/json"))
    return out(origin, { error: "Expected JSON" }, 415);
  if (Number(req.headers.get("Content-Length") || "0") > 6500)
    return out(origin, { error: "Submission too large" }, 413);

  let payload: Record<string, unknown>;
  try {
    const body = await req.text();
    if (body.length > 6000) return out(origin, { error: "Submission too large" }, 413);
    const parsed = JSON.parse(body);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) throw Error("Invalid body");
    payload = parsed;
  } catch { return out(origin, { error: "Invalid submission" }, 400); }

  // Honeypot: no submission or personal-data write for automated fills.
  if (payload.middle_name) return out(origin, { error: "Unable to accept this submission" }, 400);
  const kind = payload.kind;
  const full_name = trimmed(payload.full_name, 120);
  const rawEmail = trimmed(payload.email, 254);
  const email = rawEmail?.toLowerCase() ?? null;
  const organization = trimmed(payload.organization ?? "", 160);
  const website = trimmed(payload.website ?? "", 500);
  const market = trimmed(payload.market ?? "", 120);
  const category = trimmed(payload.category ?? "", 80);
  const description = trimmed(payload.description ?? "", 800);
  const ack = payload.processing_acknowledged === true;
  const adult = payload.adult_confirmed === true;
  const marketing = payload.marketing_opt_in === true;
  const key = payload.request_key;
  const expected = supportedNotices.has(String(payload.notice_version));

  if (!["creator", "advertiser", "privacy", "support"].includes(String(kind)) ||
      !full_name || full_name.length < 2 || !email || !validEmail(email) ||
      organization === null || website === null || market === null ||
      category === null || description === null ||
      !validUuid(key) || !expected || !ack || !adult ||
      (kind === "creator" && (!website || !validUrl(website) || !market || !category || organization !== "")) ||
      (kind === "advertiser" && (!organization || !market || (website !== "" && !validUrl(website)))) ||
      (kind === "privacy" && !description) ||
      (kind === "support" && (!description || !supportCategories.includes(String(category)) ||
        organization !== "" || website !== "" || market !== "" || marketing ||
        payload.notice_version !== supportNotice)) ||
      (kind !== "support" && payload.notice_version === supportNotice) ||
      (website && !validUrl(website))) {
    return out(origin, { error: "Please complete the required fields, review the privacy notice, and try again." }, 422);
  }

  const dbUrl = Deno.env.get("SUPABASE_URL");
  const keySecret = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  if (!dbUrl || !keySecret) return out(origin, { error: "Intake is temporarily unavailable; nothing was submitted." }, 503);

  const client = createClient(dbUrl, keySecret, {
    auth: { autoRefreshToken: false, persistSession: false }
  });
  const row = {
    request_key: key, kind, full_name, email, organization,
    website, market, category, description,
    processing_acknowledged: true,
    adult_confirmed: true,
    marketing_opt_in: marketing,
    notice_version: payload.notice_version
  };
  try {
    const { data, error } = await client.from("alpha_public_intake_requests")
      .insert(row).select("id,created_at").single();
    let record = data;
    if (error) {
      if (error.code === "23505") {
        // Same random request key and the identical submitted information only.
        // Never disclose another person's previously stored request.
        const old = await client.from("alpha_public_intake_requests")
          .select("id,created_at,kind,email,full_name,organization,website,market,category,description,marketing_opt_in,notice_version")
          .eq("request_key", key).maybeSingle();
        const existing = old.data;
        if (!existing || old.error ||
            existing.kind !== kind || existing.email !== email ||
            existing.full_name !== full_name || existing.organization !== organization ||
            existing.website !== website || existing.market !== market ||
            existing.category !== category || existing.description !== description ||
            existing.marketing_opt_in !== marketing ||
            existing.notice_version !== payload.notice_version)
          return out(origin, { error: "A request already exists for these details." }, 409);
        record = { id: existing.id, created_at: existing.created_at };
      } else if (error.code === "P0001") {
        return out(origin, { error: "The form is temporarily at capacity. Please try again later." }, 429);
      } else {
        return out(origin, { error: "We could not save your request. Please try again." }, 503);
      }
    }
    if (!record) return out(origin, { error: "We could not confirm a saved request." }, 503);
    const receipt = await client.from("alpha_public_intake_receipts")
      .select("id,recorded_at").eq("request_id", record.id).single();
    if (receipt.error || !receipt.data)
      return out(origin, { error: "We could not verify your receipt. Please use the same page to retry." }, 503);
    let caseRecord: {case_id: string; status: string} | null = null;
    if (kind === "support") {
      // Triggered atomically with the intake row; never report a case without
      // durable case and intake receipts.
      const saved = await client.from("alpha_support_cases")
        .select("case_id,status").eq("request_id", record.id).single();
      if (saved.error || !saved.data)
        return out(origin, { error: "We could not confirm the support case. Please retry with the same request." }, 503);
      caseRecord = saved.data;
    }
    // The authoritative Supabase receipt remains the source of truth.
    // HighLevel delivery may be pending, disabled or temporarily unavailable.
    // Only NEW notice-version creator/advertiser submissions are eligible;
    // privacy requests and legacy-consent records are NEVER forwarded.
    if (((kind === "creator" || kind === "advertiser") &&
        payload.notice_version === notice) ||
        (kind === "support" && supportCrmEligible(String(category)))) {
      try { await deliverToGhl(client, record.id); } catch { /* never lose intake */ }
    }
    return out(origin, {
      status: "RECEIVED_NOT_YET_VERIFIED",
      receipt_id: receipt.data.id,
      received_at: receipt.data.recorded_at,
      ...(caseRecord ? {case_id: caseRecord.case_id,case_status: caseRecord.status} : {})
    }, 201);
  } catch {
    return out(origin, { error: "We could not confirm the request. Please retry." }, 503);
  }
});
