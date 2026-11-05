// ALPHA creator/advertiser form → minimal GoHighLevel CRM contact.
// This module is plain ESM so its mapping contract can be unit tested with Node.
// Only server-side Supabase code imports it; never publish this module to Vercel.
export const ghlNotice = "AGM-INTEREST-2026-11-05-GHL";
export function mapGhlContact(record, locationId) {
  if (!record || !["creator", "advertiser"].includes(record.kind) ||
      record.notice_version !== ghlNotice ||
      record.processing_acknowledged !== true || record.adult_confirmed !== true ||
      typeof record.email !== "string" ||
      /@example\\.invalid$/i.test(record.email))
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
  // Do not send free-text descriptions, marketing consent records, identity
  // tokens or timestamps. GHL marketing automation must be verified separately.
  if (record.website) payload.website = record.website;
  if (record.kind === "advertiser") payload.companyName = record.organization;
  // Deliberately no 'tags' key (would overwrite an existing contact's tags),
  // and no dnd mutation (could change permissions of existing contacts).
  return payload;
}
