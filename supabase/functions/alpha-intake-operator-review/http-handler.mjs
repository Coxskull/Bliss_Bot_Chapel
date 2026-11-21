// Pure HTTP boundary for ALPHA #330. Every live request still requires a fresh
// GoTrue user lookup and a separate verified-MFA session RPC. No data mutation.
import { authorizedReview, buildReviewQueue } from "./review-core.mjs";

const originList = get => String(get("ALPHA_INTAKE_REVIEW_ORIGINS") || "")
  .split(",").map(s => s.trim()).filter(Boolean);
const userList = get => String(get("ALPHA_INTAKE_REVIEW_USER_IDS") || "")
  .split(",").map(s => s.trim()).filter(Boolean);
const MAX_LINKED_ROWS = 100;

const respond = (status, body, origin = null) => {
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
const claimsFromValidatedJwt = token => {
  try {
    const parts = token.split(".");
    if (parts.length !== 3) return null;
    const encoded = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const obj = JSON.parse(atob(encoded.padEnd(Math.ceil(encoded.length / 4) * 4, "=")));
    return obj && typeof obj === "object" && !Array.isArray(obj) ? obj : null;
  } catch {
    return null;
  }
};

/** Injected dependencies are for deterministic offline contract tests only.
 *  In production, createClient must be the Supabase service-side client,
 *  getEnv must be Deno.env.get, and JWTs are validated with GoTrue getUser.
 */
export function createReviewHandler({ getEnv, createClient }) {
  if (typeof getEnv !== "function" || typeof createClient !== "function")
    throw new Error("REVIEW_CONFIG_INVALID");
  return async function reviewRequest(req) {
    const origin = req.headers.get("Origin");
    const approvedOrigin = origin && originList(getEnv).includes(origin) ? origin : null;
    if (!approvedOrigin) return respond(403, { error: "Review access denied" });
    if (getEnv("ALPHA_INTAKE_REVIEW_ENABLED") !== "true")
      return respond(503, { error: "Operator review is not enabled" }, approvedOrigin);

    if (req.method === "OPTIONS") {
      return new Response(null, { status: 204, headers: {
        "Access-Control-Allow-Origin": approvedOrigin,
        "Access-Control-Allow-Methods": "GET, OPTIONS",
        "Access-Control-Allow-Headers": "Authorization",
        "Access-Control-Max-Age": "0",
        "Cache-Control": "no-store",
        "Vary": "Origin"
      } });
    }
    if (req.method !== "GET" || new URL(req.url).search)
      return respond(405, { error: "Only an unfiltered read-only queue is supported" }, approvedOrigin);

    const bearer = req.headers.get("Authorization") || "";
    if (!/^Bearer [A-Za-z0-9._~-]+$/.test(bearer))
      return respond(401, { error: "Authenticated operator required" }, approvedOrigin);
    const token = bearer.slice(7);
    const url = getEnv("SUPABASE_URL");
    const key = getEnv("SUPABASE_SERVICE_ROLE_KEY");
    const allowlist = userList(getEnv);
    if (!url || !key || !allowlist.length)
      return respond(503, { error: "Operator review is not configured" }, approvedOrigin);

    try {
      const db = createClient(url, key, {
        auth: { autoRefreshToken: false, persistSession: false }
      });
      const { data: auth, error: authError } = await db.auth.getUser(token);
      if (authError || !auth?.user)
        return respond(401, { error: "Invalid session" }, approvedOrigin);
      const claims = claimsFromValidatedJwt(token);
      const sessionId = typeof claims?.session_id === "string" ? claims.session_id : "";
      const verifiedUserId = typeof claims?.sub === "string" ? claims.sub : "";

      // No sensitive queue query until BOTH authorization and revocation checks pass.
      if (!authorizedReview({
        userId: auth.user.id, verifiedUserId, allowlistedUserIds: allowlist,
        operatorFlag: auth.user.app_metadata?.alpha_intake_operator === true,
        aal: claims?.aal, sessionId, liveSession: true
      })) return respond(403, { error: "Operator authorization required" }, approvedOrigin);

      const { data: live, error: liveError } = await db.rpc("alpha_intake_operator_session_active", {
        p_user_id: auth.user.id, p_session_id: sessionId
      });
      // RPC missing, malformed or revoked/expired session => fail closed.
      if (liveError || live !== true)
        return respond(403, { error: "Current MFA session required" }, approvedOrigin);

      const { data: requests, error: requestError } = await db
        .from("alpha_public_intake_requests")
        .select("id,kind,created_at,processing_acknowledged,adult_confirmed,notice_version,intake_status")
        .order("created_at", { ascending: false }).limit(50);
      if (requestError || !Array.isArray(requests)) throw Error("request-read");
      if (!requests.length) return respond(200, buildReviewQueue({
        requests: [], receipts: [], deliveries: []
      }), approvedOrigin);
      const ids = requests.map(r => r.id);
      const [received, delivered] = await Promise.all([
        db.from("alpha_public_intake_receipts")
          .select("id,request_id,kind,notice_version,recorded_at").in("request_id", ids).limit(MAX_LINKED_ROWS + 1),
        db.from("alpha_ghl_delivery")
          .select("request_id,state").in("request_id", ids).limit(MAX_LINKED_ROWS + 1)
      ]);
      if (received.error || delivered.error || !Array.isArray(received.data) ||
          !Array.isArray(delivered.data) ||
          received.data.length > MAX_LINKED_ROWS || delivered.data.length > MAX_LINKED_ROWS)
        throw Error("linked-record-read");
      return respond(200, buildReviewQueue({
        requests, receipts: received.data, deliveries: delivered.data
      }), approvedOrigin);
    } catch {
      // No SQL errors, JWT, private field contents, or provider credentials escape.
      return respond(503, { error: "Review unavailable; no changes were made" }, approvedOrigin);
    }
  };
}
