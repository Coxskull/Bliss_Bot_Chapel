import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2.95.0";
import { authorizedReview, buildReviewQueue } from "./review-core.mjs";

// Proposed private operator-only read-only endpoint. NOT DEPLOYED. Fail closed
// until reviewed auth/session RPC and owner-controlled environment gates exist.
// No PII fields are selected; never return contacts, private messages or tokens.
const uuid = /^[0-9a-f]{8}-[0-9a-f-]{27,}$/i;
const out = (status: number, body: unknown, origin: string | null = null): Response => {
  const headers = new Headers({
    "Content-Type": "application/json; charset=utf-8",
    "Cache-Control": "no-store, max-age=0",
    "Pragma": "no-cache",
    "X-Content-Type-Options": "nosniff",
    "Vary": "Origin"
  });
  if (origin) headers.set("Access-Control-Allow-Origin", origin);
  return new Response(JSON.stringify(body), { status, headers });
};
function jwtClaims(token: string): Record<string, unknown> | null {
  try {
    const parts = token.split(".");
    if (parts.length !== 3) return null;
    const piece = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const claims = JSON.parse(atob(piece.padEnd(Math.ceil(piece.length / 4) * 4, "=")));
    return claims && typeof claims === "object" && !Array.isArray(claims) ? claims : null;
  } catch {
    return null;
  }
}
const allowed = (key: string): string[] => (Deno.env.get(key) || "")
  .split(",").map(x => x.trim()).filter(Boolean);

Deno.serve(async (req: Request): Promise<Response> => {
  const origin = req.headers.get("Origin");
  const approvedOrigin = origin && allowed("ALPHA_INTAKE_REVIEW_ORIGINS").includes(origin) ? origin : null;
  if (!approvedOrigin) return out(403, { error: "Review access denied" });
  if (Deno.env.get("ALPHA_INTAKE_REVIEW_ENABLED") !== "true")
    return out(503, { error: "Operator review is not enabled" }, approvedOrigin);
  if (req.method === "OPTIONS") {
    const reply = out(204, null, approvedOrigin);
    reply.headers.set("Access-Control-Allow-Methods", "GET, OPTIONS");
    reply.headers.set("Access-Control-Allow-Headers", "Authorization");
    return reply;
  }
  if (req.method !== "GET" || new URL(req.url).search)
    return out(405, { error: "Only an unfiltered read-only queue is supported" }, approvedOrigin);
  const bearer = req.headers.get("Authorization") || "";
  if (!/^Bearer [A-Za-z0-9._~-]+$/.test(bearer))
    return out(401, { error: "Authenticated operator required" }, approvedOrigin);
  const token = bearer.slice(7);
  const url = Deno.env.get("SUPABASE_URL");
  const key = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  const allowlist = allowed("ALPHA_INTAKE_REVIEW_USER_IDS");
  if (!url || !key || !allowlist.length)
    return out(503, { error: "Operator review is not configured" }, approvedOrigin);

  try {
    const db = createClient(url, key, {
      auth: { autoRefreshToken: false, persistSession: false }
    });
    // The signed user and server-owned app_metadata come from GoTrue,
    // not untrusted browser state, user_metadata or a caller-supplied role.
    const { data: auth, error: authError } = await db.auth.getUser(token);
    if (authError || !auth.user) return out(401, { error: "Invalid session" }, approvedOrigin);
    // Read JWT claims only AFTER GoTrue validates this token. Even a valid
    // bearer is not sufficient; the matching session must still be live.
    const claims = jwtClaims(token);
    const sessionId = typeof claims?.session_id === "string" ? claims.session_id : "";
    const { data: live, error: liveError } = await db.rpc("alpha_intake_operator_session_active", {
      p_user_id: auth.user.id, p_session_id: sessionId
    });
    // RPC missing/permission denied => FAIL CLOSED. Never replace with a
    // guessed session, client-side app_metadata or a JWT-only shortcut.
    if (liveError || live !== true)
      return out(403, { error: "Current MFA session required" }, approvedOrigin);
    if (!authorizedReview({
      userId: auth.user.id,
      verifiedUserId: typeof claims?.sub === "string" ? claims.sub : "",
      allowlistedUserIds: allowlist,
      operatorFlag: auth.user.app_metadata?.alpha_intake_operator === true,
      aal: claims?.aal,
      sessionId,
      liveSession: live
    })) return out(403, { error: "Operator authorization required" }, approvedOrigin);

    const { data: requests, error: requestError } = await db
      .from("alpha_public_intake_requests")
      .select("id,kind,created_at,processing_acknowledged,adult_confirmed,notice_version,intake_status")
      .order("created_at", { ascending: false }).limit(50);
    if (requestError || !requests) throw Error("request-read");
    const ids = requests.map(row => row.id);
    if (ids.length === 0) return out(200, buildReviewQueue({
      requests: [], receipts: [], deliveries: []
    }), approvedOrigin);
    const [received, delivered] = await Promise.all([
      db.from("alpha_public_intake_receipts")
        .select("id,request_id,kind,notice_version,recorded_at").in("request_id", ids),
      db.from("alpha_ghl_delivery")
        .select("request_id,state").in("request_id", ids)
    ]);
    if (received.error || delivered.error || !received.data || !delivered.data)
      throw Error("linked-record-read");
    const projection = buildReviewQueue({
      requests, receipts: received.data, deliveries: delivered.data
    });
    return out(200, projection, approvedOrigin);
  } catch {
    // Never disclose personal data, raw SQL details, service credentials or JWTs.
    return out(503, { error: "Review unavailable; no changes were made" }, approvedOrigin);
  }
});
