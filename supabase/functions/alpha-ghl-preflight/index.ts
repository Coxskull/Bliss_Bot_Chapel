import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2.95.0";

const json = (payload: unknown, status = 200) => new Response(JSON.stringify(payload), {
  status,
  headers: { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" }
});
const fieldNames = [
  "ALPHA_GHL_FIELD_BUDGET_AMOUNT", "ALPHA_GHL_FIELD_BUDGET_CURRENCY",
  "ALPHA_GHL_FIELD_MARKET_CODE", "ALPHA_GHL_FIELD_CAMPAIGN_OBJECTIVE",
  "ALPHA_GHL_FIELD_MEDIA_FORMATS", "ALPHA_GHL_FIELD_AUDIENCE_REQUIREMENTS",
  "ALPHA_GHL_FIELD_LANGUAGES"
];

Deno.serve(async (req: Request) => {
  if (req.method !== "POST") return json({ error: "Method not allowed" }, 405);
  const adminKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  if (!adminKey || req.headers.get("Authorization") !== "Bearer " + adminKey)
    return json({ error: "Forbidden" }, 403);

  const dbUrl = Deno.env.get("SUPABASE_URL");
  if (!dbUrl) return json({ readyForReadback: false, databaseConnected: false, reason: "DATABASE_URL_MISSING" }, 503);
  const db = createClient(dbUrl, adminKey, { auth: { autoRefreshToken: false, persistSession: false } });
  const { data: delivery, error } = await db.from("alpha_ghl_delivery")
    .select("remote_contact_id")
    .eq("state", "DELIVERED")
    .not("remote_contact_id", "is", null)
    .order("delivered_at", { ascending: false })
    .limit(1)
    .maybeSingle();
  if (error) return json({ readyForReadback: false, databaseConnected: false, reason: "DATABASE_PREFLIGHT_FAILED" }, 503);

  const token = Deno.env.get("ALPHA_GHL_PRIVATE_INTEGRATION_TOKEN");
  const locationId = Deno.env.get("ALPHA_GHL_LOCATION_ID");
  const configured = Boolean(token && locationId && /^[A-Za-z0-9]{14,64}$/.test(locationId));
  const mappingValues = fieldNames.map(name => Deno.env.get(name) || "");
  const fieldMappingsConfigured = mappingValues.every(value => /^[A-Za-z0-9_-]{6,128}$/.test(value)) &&
    new Set(mappingValues).size === fieldNames.length;
  if (!configured) return json({
    readyForReadback: false, databaseConnected: true, providerReadOnlyCheck: "NOT_RUN",
    writesEnabled: Deno.env.get("ALPHA_GHL_SYNC_ENABLED") === "true",
    fieldMappingsConfigured, reason: "HIGHLEVEL_READ_CREDENTIALS_OR_LOCATION_MISSING"
  }, 503);
  if (!delivery?.remote_contact_id) return json({
    readyForReadback: false, databaseConnected: true, providerReadOnlyCheck: "NOT_RUN",
    writesEnabled: Deno.env.get("ALPHA_GHL_SYNC_ENABLED") === "true",
    fieldMappingsConfigured, reason: "NO_DELIVERED_CONTACT_FOR_READ_ONLY_PROBE"
  }, 503);

  const contactId = String(delivery.remote_contact_id);
  if (!/^[A-Za-z0-9_-]{6,128}$/.test(contactId))
    return json({ readyForReadback: false, databaseConnected: true, providerReadOnlyCheck: "INVALID_STORED_ID" }, 503);
  try {
    const response = await fetch("https://services.leadconnectorhq.com/contacts/" + encodeURIComponent(contactId), {
      method: "GET",
      headers: { Authorization: "Bearer " + token, Version: "v3", Accept: "application/json" },
      signal: AbortSignal.timeout(6500)
    });
    if (!response.ok) return json({
      readyForReadback: false, databaseConnected: true,
      providerReadOnlyCheck: response.status === 401 || response.status === 403 ? "AUTH_OR_READ_SCOPE_FAILURE"
        : response.status === 404 ? "STORED_CONTACT_NOT_FOUND"
        : response.status === 429 ? "RATE_LIMITED" : "PROVIDER_FAILURE",
      httpStatus: response.status, writesEnabled: Deno.env.get("ALPHA_GHL_SYNC_ENABLED") === "true",
      fieldMappingsConfigured
    }, 503);
    const payload = await response.json();
    const contact = payload?.contact;
    const sameRecord = contact?.id === contactId && contact?.locationId === locationId;
    return json({
      readyForReadback: sameRecord, databaseConnected: true,
      providerReadOnlyCheck: sameRecord ? "CONNECTED_AND_SCOPED" : "RECORD_OR_LOCATION_MISMATCH",
      writesEnabled: Deno.env.get("ALPHA_GHL_SYNC_ENABLED") === "true",
      fieldMappingsConfigured
    }, sameRecord ? 200 : 503);
  } catch {
    return json({
      readyForReadback: false, databaseConnected: true, providerReadOnlyCheck: "NETWORK_FAILURE",
      writesEnabled: Deno.env.get("ALPHA_GHL_SYNC_ENABLED") === "true", fieldMappingsConfigured
    }, 503);
  }
});
