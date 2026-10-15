namespace Bliss.Api.Security;

// Stage G: read-only review of test-only visitor consent events.
// Owner authorization permits engineering activity, NOT a visitor opt-in.
// This never returns permission to export transcripts or activate commerce.
public sealed record AskAlphaConsentBoundaryReading(
    string Status,
    bool OwnerEngineeringApprovalRecorded,
    bool SyntheticOptInEventPresent = false,
    bool SyntheticRevocationPresent = false,
    bool RealVisitorIdentityVerified = false,
    bool RealVisitorConsentAccepted = false,
    bool HistoricalSessionOwnershipVerified = false,
    bool TranscriptLinked = false,
    bool TranscriptExportAuthorized = false,
    bool PaymentAuthorized = false,
    bool CreatorPayoutAuthorized = false,
    bool CampaignDeliveryAuthorized = false,
    string Delivery = "NOT_SENT");

public static class AskAlphaConsentBoundaryEvaluator
{
    public static AskAlphaConsentBoundaryReading Inspect(
        bool ownerEngineeringApproved, bool syntheticOptIn, bool syntheticRevoked,
        bool exactSyntheticPurposeAndVersion, bool syntheticOwnershipReceiptPresent)
    {
        var baseline = new AskAlphaConsentBoundaryReading(
            ownerEngineeringApproved
                ? "OWNER_ENGINEERING_APPROVAL_ONLY_VISITOR_CONSENT_REQUIRED"
                : "OWNER_ENGINEERING_APPROVAL_NOT_RECORDED",
            ownerEngineeringApproved);
        if (!ownerEngineeringApproved)
            return baseline;

        if (syntheticRevoked)
            return baseline with {
                Status = "SYNTHETIC_CONSENT_REVOKED_NO_EXPORT",
                SyntheticOptInEventPresent = syntheticOptIn,
                SyntheticRevocationPresent = true
            };

        if (!syntheticOptIn || !exactSyntheticPurposeAndVersion ||
            !syntheticOwnershipReceiptPresent)
            return baseline with { Status = "VISITOR_CONSENT_PREREQUISITES_MISSING" };

        return baseline with {
            Status = "SYNTHETIC_CONSENT_INTENT_RECORDED_UNVERIFIED",
            SyntheticOptInEventPresent = true
        };
    }
}
