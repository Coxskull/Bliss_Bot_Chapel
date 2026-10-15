using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Financials;

namespace Bliss.Api.Financials;

// Inspection only: this is NOT a payment receipt, approved compensation or permission to deliver.
public sealed record MockCampaignDossier(
    Guid OperationId, Guid CampaignId, Guid HandoffId, Guid QuoteVersionId,
    Guid CandidateId, Guid CampaignPlacementId, Guid CampaignPlacementRunId,
    string Currency, string AmountMinor, string SourceEvidenceSha256,
    string CandidateImageSha256, int ImageWidth, int ImageHeight,
    string HandoffSnapshotSha256, string CorrelationStatus = "STORED_PLANNED_HANDOFF_CORRELATED",
    string AskAlphaTranscriptStatus = "NOT_LINKED",
    string PlacementPreviewStatus = "NOT_INDEPENDENTLY_VERIFIED",
    string CreativeQualityStatus = "HISTORICAL_FIXTURE_REVIEW_ONLY",
    string AlphaCommissionStatus = "NO_APPROVED_ALLOCATION",
    string CreatorCompensationStatus = "NO_APPROVED_ALLOCATION",
    bool PaymentCompleted = false, bool ProviderCalled = false,
    bool CreatorPayoutAuthorized = false, bool CampaignReady = false,
    bool CampaignDeliveryAuthorized = false, string Delivery = "NOT_SENT");

// Require exact IDs, source amounts, stored bytes and unsent states from both contexts.
// No acceptance of owner authority, legal rights, current approval or visual aesthetics is inferred.
public static class MockCampaignDossierVerifier
{
    public static MockCampaignDossier Correlate(
        MockCampaignFinancialEvidence funding, FundingBinding binding,
        CreativeCampaignHandoff handoff, WeddingPlannerWorkspace workspace,
        Campaign campaign, CampaignPlacement placement, CampaignPlacementRun run,
        Quote quote, QuoteVersion version, QuoteLineItem line, int quoteLineCount,
        AcademyProductionBrief brief, AcademyProductionCandidate candidate,
        CandidateApprovalDecision fit)
    {
        if (funding.Mode != "MOCK" || funding.ProviderCalled || funding.PaymentCompleted ||
            funding.CreatorPayoutAuthorized || funding.CampaignDeliveryAuthorized ||
            funding.AlphaCommissionStatus != "NO_APPROVED_ALLOCATION" ||
            funding.CreatorCompensationStatus != "NO_APPROVED_ALLOCATION" ||
            funding.Currency != "USD" || funding.AmountMinor <= 0 ||
            binding.Scope != funding.Scope || binding.SourceVersionId != funding.QuoteVersionId ||
            binding.AdvertiserId != workspace.AdvertiserId || binding.SourceQuoteId != quote.Id)
            throw new InvalidOperationException("Mock funding identity/authority is not correlated.");

        if (handoff.Status != "PLANNED" || handoff.Delivery != "NOT_SENT" ||
            campaign.Id != funding.Scope.CampaignId || campaign.Status != "DRAFT" ||
            placement.Id != handoff.CampaignPlacementId ||
            placement.CampaignId != campaign.Id || placement.Status != "PLANNED" ||
            placement.StartAt is not null || placement.EndAt is not null ||
            run.Id != handoff.CampaignPlacementRunId ||
            run.CampaignPlacementId != placement.Id || run.CampaignId != campaign.Id ||
            run.SourceSystem != "CREATIVE_HANDOFF_V1" || run.Outcome != "PLANNED" ||
            run.AdInventorySlotId != placement.AdInventorySlotId ||
            quote.Id != version.QuoteId || quote.Status != "ACCEPTED" ||
            quote.CurrentVersionNumber != version.VersionNumber ||
            version.Id != funding.QuoteVersionId || quote.CurrencyCode != "USD" ||
            version.CurrencyCode != "USD" || line.QuoteVersionId != version.Id ||
            line.Id != handoff.QuoteLineItemId || line.CurrencyCode != "USD" ||
            line.Quantity != 1m || quoteLineCount != 1 ||
            version.TotalAmount * 100m != funding.AmountMinor ||
            line.LineAmount != version.TotalAmount ||
            brief.Id != candidate.BriefId || brief.WorkspaceId != workspace.Id ||
            handoff.WorkspaceId != workspace.Id || handoff.CandidateId != candidate.Id ||
            fit.Id != handoff.InventoryFitId || fit.CandidateId != candidate.Id ||
            fit.Stage != "INVENTORY_FIT" || fit.Decision != "APPROVE" ||
            fit.AdInventorySlotId != placement.AdInventorySlotId ||
            !fit.DimensionsVerified || !fit.FormatVerified || !fit.CreatorSafeAreaVerified)
            throw new InvalidOperationException("Creative handoff and frozen mock quote do not agree.");

        var bytes = candidate.ImageBytes;
        if (bytes is null || bytes.Length < 24 || bytes.Length > 1_048_576 ||
            !bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}))
            throw new InvalidOperationException("Stored fictional creative image is missing or malformed.");
        var width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)));
        var height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)));
        var actualHash = Convert.ToHexString(SHA256.HashData(bytes));
        if (width <= 1 || height <= 1 || width != fit.ImageWidth ||
            height != fit.ImageHeight || !actualHash.Equals(candidate.ImageSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stored creative bytes do not match recorded geometry/hash.");

        try
        {
            if (handoff.SnapshotJson.Length is < 2 or > 32768)
                throw new InvalidOperationException("Handoff snapshot outside bounded size.");
            using var snapshot = JsonDocument.Parse(handoff.SnapshotJson);
            var value = snapshot.RootElement;
            if (value.GetProperty("campaignId").GetGuid() != campaign.Id ||
                value.GetProperty("quoteId").GetGuid() != quote.Id ||
                value.GetProperty("quoteVersionId").GetGuid() != version.Id ||
                value.GetProperty("quoteLineId").GetGuid() != line.Id ||
                value.GetProperty("lineAmount").GetDecimal() != line.LineAmount ||
                value.GetProperty("currencyCode").GetString() != "USD" ||
                value.GetProperty("id").GetGuid() != candidate.Id ||
                !actualHash.Equals(value.GetProperty("imageSha256").GetString(), StringComparison.OrdinalIgnoreCase) ||
                value.GetProperty("inventoryFit").GetGuid() != fit.Id ||
                value.GetProperty("slotId").GetGuid() != placement.AdInventorySlotId ||
                value.GetProperty("matchId").GetGuid() != run.BlissMatchId ||
                value.GetProperty("contentItemId").GetGuid() != run.ContentItemId ||
                value.GetProperty("imageWidth").GetInt32() != width ||
                value.GetProperty("imageHeight").GetInt32() != height)
                throw new InvalidOperationException("Handoff snapshot identity or amount changed.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or OverflowException)
        {
            throw new InvalidOperationException("Stored handoff snapshot is invalid.", ex);
        }

        return new MockCampaignDossier(
            funding.OperationId, campaign.Id, handoff.Id, version.Id,
            candidate.Id, placement.Id, run.Id, "USD", funding.AmountMinor.ToString(),
            funding.SourceEvidenceSha256, actualHash, width, height,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(handoff.SnapshotJson))));
    }
}
