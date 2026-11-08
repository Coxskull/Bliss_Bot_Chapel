import { MARKET_CURRENCIES, parseBudgetToMinor } from "./marketplace-budget.mjs";

export const BUDGET_NOTICE = "AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1";
export const MARKET_LABELS = Object.freeze({
  SANTO_DOMINGO: "Santo Domingo, Dominican Republic",
  PANAMA_CITY: "Panama City, Panama",
  MEDELLIN: "Medellín, Colombia",
  MANILA: "Manila, Philippines",
  KUALA_LUMPUR: "Kuala Lumpur, Malaysia",
  JAKARTA: "Jakarta, Indonesia"
});
const objectives = new Set([
  "awareness", "engagement", "website_visits", "leads", "sales",
  "community_reach", "other"
]);
const formats = new Set([
  "podcast", "digital_creator", "radio", "television",
  "newspaper", "magazine", "other"
]);
const text = (value, max, allowEmpty = false) => {
  if (typeof value !== "string") return null;
  const normalized = value.trim();
  if ((!allowEmpty && normalized.length === 0) || normalized.length > max) return null;
  return normalized;
};
function stringList(value, maxItems, maxLength) {
  if (!Array.isArray(value) || value.length < 1 || value.length > maxItems) return null;
  const normalized = value.map(item => text(item, maxLength));
  if (normalized.some(item => item === null)) return null;
  const unique = [...new Map(normalized.map(item => [item.toLocaleLowerCase(), item])).values()];
  return unique.length === normalized.length ? unique : null;
}

export function normalizeStructuredAdvertiser(payload) {
  const marketCode = text(payload?.market_code, 32)?.toUpperCase();
  const currency = text(payload?.budget_currency, 3)?.toUpperCase();
  const objective = text(payload?.campaign_objective, 40);
  const audience = text(payload?.audience_requirements ?? "", 500, true);
  const amount = typeof payload?.budget_amount === "string"
    ? payload.budget_amount.trim() : "";
  if (!marketCode || !Object.hasOwn(MARKET_LABELS, marketCode) ||
      MARKET_CURRENCIES[marketCode] !== currency ||
      !objective || !objectives.has(objective) ||
      !audience || payload?.contact_consent !== true)
    throw new Error("INVALID_OR_UNCONSENTED_ADVERTISER_INTAKE");
  const preferredMedia = stringList(payload.preferred_media_formats, 7, 32);
  const languages = stringList(payload.preferred_languages, 12, 80);
  if (!preferredMedia || preferredMedia.some(value => !formats.has(value)) || !languages)
    throw new Error("INVALID_ADVERTISER_MEDIA_OR_LANGUAGE");
  const budgetMinor = parseBudgetToMinor(amount, currency);
  return Object.freeze({
    market: MARKET_LABELS[marketCode],
    market_code: marketCode,
    budget_amount: amount,
    budget_amount_minor: budgetMinor,
    budget_currency: currency,
    campaign_objective: objective,
    preferred_media_formats: preferredMedia,
    audience_requirements: audience || null,
    preferred_languages: languages
  });
}
