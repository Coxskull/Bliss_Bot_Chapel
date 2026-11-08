// Server-side contact mapping for consented ALPHA public inquiries.
export const ghlNotice = "AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1";
const advertiserFields = Object.freeze([
  ["budget_amount", "budget_amount"],
  ["budget_currency", "budget_currency"],
  ["market_code", "market_code"],
  ["campaign_objective", "campaign_objective"],
  ["preferred_media_formats", "preferred_media_formats"],
  ["audience_requirements", "audience_requirements"],
  ["preferred_languages", "preferred_languages"]
]);

export function mapGhlContact(record, locationId, fieldIds = {}) {
  if (!record || !["creator", "advertiser"].includes(record.kind) ||
      record.notice_version !== ghlNotice ||
      record.contact_consent !== true || record.contact_consent_version !== ghlNotice ||
      record.processing_acknowledged !== true || record.adult_confirmed !== true ||
      typeof record.email !== "string" ||
      /@example\.invalid$/i.test(record.email))
    throw Error("Not eligible for GoHighLevel transmission");
  if (typeof locationId !== "string" || !/^[A-Za-z0-9]{14,64}$/.test(locationId))
    throw Error("Invalid HighLevel location");
  const payload = {
    locationId,
    name: record.full_name,
    email: record.email,
    source: record.kind === "creator"
      ? "ALPHA GLOBAL MEDIA · Creator Interest"
      : "ALPHA GLOBAL MEDIA · Advertiser Interest",
    createNewIfDuplicateAllowed: false
  };
  if (record.website) payload.website = record.website;
  if (record.kind === "advertiser") {
    payload.companyName = record.organization;
    const customFields = advertiserFields.map(([field, column]) => {
      const id = fieldIds[field];
      const value = record[column];
      if (typeof id !== "string" || !/^[A-Za-z0-9_-]{6,128}$/.test(id) ||
          (value === null && field !== "audience_requirements") || value === undefined)
        throw Error("Advertiser CRM custom-field mapping is incomplete");
      return { id, field_value: value === null ? "" : Array.isArray(value) ? value.join(", ") : String(value) };
    });
    if (new Set(customFields.map(field => field.id)).size !== customFields.length)
      throw Error("Advertiser CRM custom-field mapping is ambiguous");
    payload.customFields = customFields;
  }
  // No tags or DND mutations: existing contact state is preserved.
  return payload;
}
