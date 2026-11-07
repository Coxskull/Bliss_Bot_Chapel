// ALPHA Task #21 — private operator authorization contract.
// Roster is a server-only value; never take roles/permissions from client input
// or editable JWT user_metadata. No configured operators means NO access.
export const operatorRoles = Object.freeze([
  "GENERAL_SUPPORT","ADVERTISER_SUPPORT","CREATOR_SUPPORT",
  "BILLING_REVIEW","PRIVACY_REVIEW","SECURITY_REVIEW"
]);
export const sensitiveRoles = Object.freeze([
  "BILLING_REVIEW","PRIVACY_REVIEW","SECURITY_REVIEW"
]);
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
export function isUuid(value) {
  return typeof value === "string" && uuid.test(value);
}
export function parseOperatorRoster(raw) {
  if (typeof raw !== "string" || raw.length > 8192 || !raw.trim())
    throw Error("ROSTER_NOT_CONFIGURED");
  let decoded;
  try { decoded = JSON.parse(raw); } catch { throw Error("INVALID_ROSTER"); }
  if (!decoded || Array.isArray(decoded) || typeof decoded !== "object"
      || Object.keys(decoded).length === 0)
    throw Error("INVALID_ROSTER");
  const roster = Object.create(null);
  for (const [role,users] of Object.entries(decoded)) {
    if (!operatorRoles.includes(role) || !Array.isArray(users)
        || users.length < 1 || users.length > 20
        || !users.every(isUuid) || new Set(users).size !== users.length)
      throw Error("INVALID_ROSTER");
    roster[role] = Object.freeze([...users]);
  }
  return Object.freeze(roster);
}
export function rolesForUser(roster,userId,aal) {
  if (!isUuid(userId)) return [];
  return operatorRoles.filter(role =>
    Array.isArray(roster[role]) && roster[role].includes(userId)
      && (!sensitiveRoles.includes(role) || aal === "aal2"));
}
export function mayAssign(roster,role,ownerId) {
  return isUuid(ownerId) && operatorRoles.includes(role) &&
    Array.isArray(roster[role]) && roster[role].includes(ownerId);
}
export const validStatuses = Object.freeze([
  "OPEN","TRIAGED","IN_REVIEW","AWAITING_CUSTOMER","RESOLVED","CLOSED"
]);
export function parseTransition(value) {
  if (!value || typeof value !== "object" || Array.isArray(value)
      || !isUuid(value.case_id) || !validStatuses.includes(value.status)
      || !isUuid(value.owner_user_id)
      || typeof value.resolution !== "string" || value.resolution.length > 1600)
    throw Error("INVALID_TRANSITION");
  if (["RESOLVED","CLOSED"].includes(value.status) && !value.resolution.trim())
    throw Error("RESOLUTION_REQUIRED");
  return {
    case_id:value.case_id, status:value.status,
    owner_user_id:value.owner_user_id, resolution:value.resolution
  };
}
