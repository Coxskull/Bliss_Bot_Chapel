// ALPHA #330 — Pure, read-only intake-to-review projection. No database or network I/O.
// The public submission is NOT proof of identity, sponsorship rights or account eligibility.
export const NOTICE = "AGM-INTEREST-2026-11-05-GHL";
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const kinds = new Set(["creator", "advertiser"]);
const deliveryStates = new Set(["PENDING", "CLAIMED", "RETRY", "DELIVERED", "FAILED"]);
const validId = value => typeof value === "string" && UUID.test(value);

export function authorizedReview({ userId, verifiedUserId, allowlistedUserIds, operatorFlag, aal, sessionId, liveSession }) {
  return validId(userId) && userId === verifiedUserId &&
    Array.isArray(allowlistedUserIds) && allowlistedUserIds.includes(userId) &&
    operatorFlag === true && aal === "aal2" && validId(sessionId) && liveSession === true;
}

const indexBy = (rows, column) => {
  if (!Array.isArray(rows)) throw new Error("REVIEW_SOURCE_UNAVAILABLE");
  const out = new Map();
  for (const row of rows) {
    if (!row || !validId(row[column])) throw new Error("REVIEW_SOURCE_INVALID");
    const existing = out.get(row[column]) || [];
    existing.push(row);
    out.set(row[column], existing);
  }
  return out;
};

export function buildReviewQueue({ requests, receipts, deliveries }) {
  if (!Array.isArray(requests) || requests.length > 50) throw new Error("REVIEW_PAGE_INVALID");
  const receiptMap = indexBy(receipts, "request_id");
  const deliveryMap = indexBy(deliveries, "request_id");
  const seen = new Set();
  const items = [];
  for (const request of requests) {
    if (!request || !validId(request.id) || seen.has(request.id)) throw new Error("REVIEW_REQUEST_INVALID");
    seen.add(request.id);
    if (!kinds.has(request.kind)) continue; // Privacy rights requests never appear in this queue.
    const matches = receiptMap.get(request.id) || [];
    const deliveriesForRequest = deliveryMap.get(request.id) || [];
    const verifiedReceipt = matches.length === 1 &&
      validId(matches[0].id) &&
      matches[0].kind === request.kind &&
      matches[0].notice_version === request.notice_version &&
      Number.isFinite(Date.parse(request.created_at)) &&
      Number.isFinite(Date.parse(matches[0].recorded_at)) &&
      Date.parse(matches[0].recorded_at) >= Date.parse(request.created_at);
    const consentValid = request.processing_acknowledged === true &&
      request.adult_confirmed === true &&
      request.notice_version === NOTICE;
    const receipt = verifiedReceipt ? matches[0] : null;
    const linked = deliveriesForRequest.length === 1 ? deliveriesForRequest[0] : null;
    const crm = linked && deliveryStates.has(linked.state) ? linked.state : "UNCONFIRMED";
    let reviewState = "REVIEW_READY";
    if (!consentValid) reviewState = "HOLD_CONSENT";
    else if (request.intake_status !== "NOT_YET_VERIFIED") reviewState = "HOLD_STATUS";
    else if (!verifiedReceipt) reviewState = "HOLD_RECEIPT";
    else if (crm !== "DELIVERED") reviewState = "WAIT_CRM";
    items.push(Object.freeze({
      request_id: request.id,
      kind: request.kind,
      received_at: request.created_at,
      receipt_id: receipt?.id ?? null,
      crm_delivery_state: crm,
      review_state: reviewState,
      manual_identity_and_rights_review_required: true,
      invitation_authorized: false,
      campaign_authorized: false,
      payment_authorized: false
    }));
  }
  items.sort((a, b) => b.received_at.localeCompare(a.received_at));
  return Object.freeze({
    source: "BLISS_PUBLIC_INTEREST_ONLY",
    readonly: true,
    count: items.length,
    review_ready: items.filter(x => x.review_state === "REVIEW_READY").length,
    items
  });
}
