// Restricted support contract, used by the public intake handler and offline CI.
export const supportNotice = "AGM-SUPPORT-2026-11-08";
export const supportCategories = Object.freeze([
  "general", "advertiser", "creator", "billing", "refund",
  "failed_campaign", "privacy", "security_incident"
]);
export const nonSensitiveCrmCategories = Object.freeze(["general", "advertiser", "creator"]);
export function supportRoute(category) {
  if (!supportCategories.includes(category)) throw Error("Unknown support category");
  if (category === "security_incident") return {severity:"CRITICAL",assigned_role:"SECURITY_REVIEW",human_required:true};
  if (category === "privacy") return {severity:"HIGH",assigned_role:"PRIVACY_REVIEW",human_required:true};
  if (["billing","refund","failed_campaign"].includes(category))
    return {severity:"HIGH",assigned_role:"BILLING_REVIEW",human_required:true};
  if (category === "advertiser") return {severity:"NORMAL",assigned_role:"ADVERTISER_SUPPORT",human_required:false};
  if (category === "creator") return {severity:"NORMAL",assigned_role:"CREATOR_SUPPORT",human_required:false};
  return {severity:"NORMAL",assigned_role:"GENERAL_SUPPORT",human_required:false};
}
export const supportCrmEligible = category => nonSensitiveCrmCategories.includes(category);
