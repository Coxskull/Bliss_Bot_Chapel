import { mapGhlContact } from "./ghl-payload.mjs";

// Private server-to-server bridge. Never call this from a browser.
// Disabled unless the owner has configured all secrets and approved contact
// creation workflows in the intended HighLevel sub-account.
export async function deliverToGhl(client, requestId) {
  if (Deno.env.get("ALPHA_GHL_SYNC_ENABLED") !== "true") return "DISABLED";
  const locationId = Deno.env.get("ALPHA_GHL_LOCATION_ID");
  const token = Deno.env.get("ALPHA_GHL_PRIVATE_INTEGRATION_TOKEN");
  if (!token || !locationId || !/^[A-Za-z0-9]{14,64}$/.test(locationId))
    return "CONFIG_MISSING";

  const claimed = await client.rpc("alpha_ghl_claim", { p_request_id: requestId });
  if (claimed.error || !Array.isArray(claimed.data) || !claimed.data.length)
    return "NOT_DUE";
  const lease = claimed.data[0];
  if (!lease?.claim_token) return "NOT_DUE";
  const finish = async (state, httpStatus, errorClass, contactId = null) => {
    // Only the exact claim token can complete delivery; after an unexpected
    // process crash the lease becomes eligible for a bounded retry.
    await client.rpc("alpha_ghl_finish", {
      p_request_id: requestId, p_claim_token: lease.claim_token,
      p_new_state: state, p_http_status: httpStatus,
      p_error_class: errorClass, p_contact_id: contactId
    });
  };

  try {
    const lookup = await client.from("alpha_public_intake_requests")
      .select("kind,full_name,email,organization,website,notice_version,processing_acknowledged,adult_confirmed")
      .eq("id", requestId).single();
    if (lookup.error || !lookup.data) {
      await finish("BLOCKED", null, "invalid_record");
      return "BLOCKED";
    }
    let payload;
    try {
      payload = mapGhlContact(lookup.data, locationId);
    } catch {
      await finish("BLOCKED", null, "invalid_record");
      return "BLOCKED";
    }
    const response = await fetch("https://services.leadconnectorhq.com/contacts/upsert", {
      method: "POST",
      headers: {
        Authorization: "Bearer " + token,
        Version: "v3",
        Accept: "application/json",
        "Content-Type": "application/json"
      },
      body: JSON.stringify(payload),
      signal: AbortSignal.timeout(6500)
    });
    if (!response.ok) {
      const status = response.status;
      const category = [401,403].includes(status) ? "auth_failure"
        : [400,422].includes(status) ? "invalid_record"
        : status === 429 ? "rate_limited" : "provider_failure";
      const final = category === "auth_failure" || category === "invalid_record"
        ? "BLOCKED" : "RETRY";
      // Deliberately never read or store response body: may contain PII or tokens.
      await finish(final, status, category);
      return final;
    }
    let remote;
    try {
      const reply = await response.json();
      remote = reply?.contact?.id;
    } catch { /* invalid provider response */ }
    if (typeof remote !== "string" || !/^[A-Za-z0-9_-]{6,128}$/.test(remote)) {
      await finish("RETRY", response.status, "invalid_response");
      return "RETRY";
    }
    await finish("DELIVERED", response.status, "", remote);
    return "DELIVERED";
  } catch {
    await finish("RETRY", null, "network_failure");
    return "RETRY";
  }
}
