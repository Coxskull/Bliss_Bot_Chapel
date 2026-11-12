import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2.95.0";
import { reconcileContact } from "./logic.mjs";

const json = (payload: unknown, status = 200) => new Response(JSON.stringify(payload), {
  status,
  headers: { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" }
});
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

Deno.serve(async (req: Request) => {
  if (req.method !== "POST") return json({ error: "Method not allowed" }, 405);
  const adminKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  if (!adminKey || req.headers.get("Authorization") !== "Bearer " + adminKey)
    return json({ error: "Forbidden" }, 403);

  const token = Deno.env.get("ALPHA_GHL_PRIVATE_INTEGRATION_TOKEN");
  const locationId = Deno.env.get("ALPHA_GHL_LOCATION_ID");
  const dbUrl = Deno.env.get("SUPABASE_URL");
  if (!token || !locationId || !/^[A-Za-z0-9]{14,64}$/.test(locationId) || !dbUrl)
    return json({ error: "Read-only CRM reconciliation is not configured" }, 503);

  let requestedIds: string[] | null = null;
  try {
    const body = await req.json();
    if (body?.request_ids !== undefined) {
      if (!Array.isArray(body.request_ids) || body.request_ids.length < 1 ||
          body.request_ids.length > 20 || body.request_ids.some((id: unknown) => typeof id !== "string" || !UUID.test(id)))
        return json({ error: "request_ids must contain 1 to 20 UUIDs" }, 400);
      requestedIds = [...new Set(body.request_ids)];
    }
  } catch { return json({ error: "Invalid JSON request" }, 400); }

  const db = createClient(dbUrl, adminKey, { auth: { autoRefreshToken: false, persistSession: false } });
  let query = db.from("alpha_ghl_delivery")
    .select("request_id,state,remote_contact_id,delivered_at")
    .eq("state", "DELIVERED")
    .not("remote_contact_id", "is", null)
    .order("delivered_at", { ascending: true })
    .limit(20);
  if (requestedIds) query = query.in("request_id", requestedIds);
  const { data: deliveries, error: deliveryError } = await query;
  if (deliveryError) return json({ error: "Unable to read CRM delivery evidence" }, 503);
  const rows = deliveries || [];
  if (!rows.length) return json({ runId: crypto.randomUUID(), processed: 0, results: [] }, 200);

  const ids = rows.map((row: any) => row.request_id);
  const [{ data: requests, error: requestError }, { data: allEmails, error: duplicateError }] = await Promise.all([
    db.from("alpha_public_intake_requests")
      .select("id,kind,email,notice_version,contact_consent,marketing_opt_in,contact_consent_version,contact_consent_at,business_identity_verified,decision_authority_verified,followup_gate,followup_approved_at")
      .in("id", ids),
    db.from("alpha_public_intake_requests")
      .select("id,email")
      .not("email", "is", null)
      .limit(5000)
  ]);
  if (requestError || duplicateError) return json({ error: "Unable to read restricted review evidence" }, 503);

  const emailCounts = new Map<string, number>();
  for (const item of allEmails || []) {
    const email = typeof item.email === "string" ? item.email.trim().toLowerCase() : "";
    if (email) emailCounts.set(email, (emailCounts.get(email) || 0) + 1);
  }
  const requestById = new Map((requests || []).map((request: any) => [request.id, request]));
  const runId = crypto.randomUUID();
  const results: any[] = [];
  const audit: any[] = [];
  for (const delivery of rows) {
    const request: any = requestById.get(delivery.request_id);
    let result: any;
    let errorClass: string | null = null;
    if (!request || !request.email || !delivery.remote_contact_id) {
      result = { classification: "HOLD_SOURCE_RECORD_INCOMPLETE", crmMatchCount: 0,
        internalDuplicateCount: 0, deliveredContactMatch: false, emailIdentityMatch: false,
        suppressionStatus: "UNKNOWN", consentValid: false, businessIdentityVerified: false,
        decisionAuthorityVerified: false, followupGate: request?.followup_gate || "UNKNOWN" };
      errorClass = "source_record_incomplete";
    } else {
      try {
        const contactId = String(delivery.remote_contact_id).trim();
        if (!/^[A-Za-z0-9_-]{6,128}$/.test(contactId)) {
          errorClass = "invalid_stored_contact_id";
          result = { classification: "HOLD_CRM_IDENTITY_MISMATCH", crmMatchCount: 0,
            internalDuplicateCount: emailCounts.get(request.email.trim().toLowerCase()) || 0,
            deliveredContactMatch: false, emailIdentityMatch: false, suppressionStatus: "UNKNOWN",
            consentValid: false, businessIdentityVerified: request.business_identity_verified === true,
            decisionAuthorityVerified: request.decision_authority_verified === true,
            followupGate: request.followup_gate || "UNKNOWN", remoteDuplicateCheck: "NOT_PERFORMED" };
        } else {
          // Read only the already linked CRM record by its stored provider ID.
          // Do not send the private intake email to the CRM search endpoint.
          const response = await fetch("https://services.leadconnectorhq.com/contacts/" + encodeURIComponent(contactId), {
            method: "GET",
            headers: { Authorization: "Bearer " + token, Version: "v3", Accept: "application/json" },
            signal: AbortSignal.timeout(6500)
          });
          if (!response.ok) {
            errorClass = response.status === 401 || response.status === 403 ? "auth_or_scope_failure"
              : response.status === 404 ? "stored_contact_not_found"
              : response.status === 429 ? "rate_limited" : "provider_failure";
            result = { classification: "HOLD_CRM_READBACK_FAILED", crmMatchCount: 0,
              internalDuplicateCount: emailCounts.get(request.email.trim().toLowerCase()) || 0,
              deliveredContactMatch: false, emailIdentityMatch: false, suppressionStatus: "UNKNOWN",
              consentValid: false, businessIdentityVerified: request.business_identity_verified === true,
              decisionAuthorityVerified: request.decision_authority_verified === true,
              followupGate: request.followup_gate || "UNKNOWN", remoteDuplicateCheck: "NOT_PERFORMED" };
          } else {
            const payload = await response.json();
            const contact = payload?.contact;
            const contacts = contact && contact.id === contactId ? [contact] : [];
            const internalDuplicateCount = emailCounts.get(request.email.trim().toLowerCase()) || 0;
            result = reconcileContact({
              request, delivery, contacts, providerTotal: contacts.length,
              internalDuplicateCount, locationId
            });
            result.remoteDuplicateCheck = "NOT_PERFORMED";
            if (result.classification === "RECONCILED_ALREADY_CLEARED")
              result.classification = "HOLD_CRM_DUPLICATE_REVIEW_REQUIRED";
          }
        }
      } catch {
        errorClass = "network_or_invalid_response";
        result = { classification: "HOLD_CRM_READBACK_FAILED", crmMatchCount: 0,
          internalDuplicateCount: emailCounts.get(request.email.trim().toLowerCase()) || 0,
          deliveredContactMatch: false, emailIdentityMatch: false, suppressionStatus: "UNKNOWN",
          consentValid: false, businessIdentityVerified: request.business_identity_verified === true,
          decisionAuthorityVerified: request.decision_authority_verified === true,
          followupGate: request.followup_gate || "UNKNOWN" };
      }
    }

    results.push({ requestId: delivery.request_id, ...result });
    audit.push({
      run_id: runId, request_id: delivery.request_id, source_delivery_state: delivery.state,
      crm_match_count: result.crmMatchCount, delivered_contact_match: result.deliveredContactMatch,
      email_identity_match: result.emailIdentityMatch, suppression_status: result.suppressionStatus,
      consent_valid: result.consentValid, business_identity_verified: result.businessIdentityVerified,
      decision_authority_verified: result.decisionAuthorityVerified,
      followup_gate: result.followupGate, classification: result.classification, error_class: errorClass
    });
  }

  const { error: auditError } = await db.from("alpha_ghl_review_audit").insert(audit);
  if (auditError) return json({ error: "Audit receipt could not be persisted; no review result accepted" }, 503);
  console.log(JSON.stringify({ event: "crm_readback_completed", runId, processed: results.length }));
  return json({ runId, processed: results.length, results }, 200);
});
