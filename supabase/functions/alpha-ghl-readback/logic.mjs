const normalize = value => typeof value === "string" ? value.trim().toLowerCase() : "";

export function emailSuppressionStatus(contact) {
  if (!contact || typeof contact !== "object") return "UNKNOWN";
  const statuses = [
    contact.dndSettings?.Email?.status,
    contact.inboundDndSettings?.Email?.status,
    contact.inboundDndSettings?.all?.status
  ].filter(value => value !== undefined && value !== null).map(value => String(value).toLowerCase());
  if (contact.dnd === true || statuses.includes("active")) return "SUPPRESSED";
  if (contact.dnd === false && statuses.every(value => value === "inactive")) return "CLEAR";
  return "UNKNOWN";
}

export function reconcileContact({ request, delivery, contacts, providerTotal, internalDuplicateCount, locationId }) {
  const matches = Array.isArray(contacts) ? contacts : [];
  const email = normalize(request?.email);
  const inLocation = matches.filter(contact => contact?.locationId === locationId);
  const identityContact = inLocation.find(contact =>
    contact?.id === delivery?.remote_contact_id && normalize(contact?.email) === email
  );
  const suppressionStatus = identityContact ? emailSuppressionStatus(identityContact) : "UNKNOWN";
  const consentValid = request?.contact_consent === true &&
    request?.contact_consent_version === request?.notice_version &&
    request?.notice_version === "AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1" &&
    Boolean(request?.contact_consent_at);
  const duplicates = Math.max(Number.isSafeInteger(providerTotal) ? providerTotal : matches.length,
    matches.length, Number.isSafeInteger(internalDuplicateCount) ? internalDuplicateCount : 0);
  let classification = "HOLD_MANUAL_REVIEW_REQUIRED";
  if (!consentValid) classification = "HOLD_NO_CURRENT_INQUIRY_CONTACT_CONSENT";
  else if (duplicates > 1) classification = "HOLD_DUPLICATE_REVIEW";
  else if (!identityContact) classification = "HOLD_CRM_IDENTITY_MISMATCH";
  else if (suppressionStatus === "SUPPRESSED") classification = "HOLD_CRM_SUPPRESSED";
  else if (suppressionStatus === "UNKNOWN") classification = "HOLD_CRM_SUPPRESSION_UNKNOWN";
  else if (request?.business_identity_verified !== true || request?.decision_authority_verified !== true)
    classification = "HOLD_IDENTITY_OR_AUTHORITY_UNVERIFIED";
  else if (request?.followup_gate !== "CLEARED" || !request?.followup_approved_at)
    classification = "HOLD_EXPLICIT_APPROVAL_REQUIRED";
  else classification = "RECONCILED_ALREADY_CLEARED";

  return {
    crmMatchCount: Number.isSafeInteger(providerTotal) ? providerTotal : matches.length,
    internalDuplicateCount: Number.isSafeInteger(internalDuplicateCount) ? internalDuplicateCount : 0,
    deliveredContactMatch: Boolean(identityContact),
    emailIdentityMatch: Boolean(identityContact),
    suppressionStatus,
    consentValid,
    businessIdentityVerified: request?.business_identity_verified === true,
    decisionAuthorityVerified: request?.decision_authority_verified === true,
    followupGate: request?.followup_gate || "UNKNOWN",
    classification
  };
}
