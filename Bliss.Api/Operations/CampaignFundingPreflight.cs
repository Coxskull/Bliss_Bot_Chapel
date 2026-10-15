using System.Globalization;
using System.Text.Json;
using Bliss.Domain.Entities;

namespace Bliss.Api.Operations;

// Diagnostic of previously stored Economics/creative lineage only.
// Never constructs a financial operation, FX conversion, payout or campaign send.
public sealed record CampaignFundingPreflightReading(
    Guid HandoffId, Guid CampaignId, Guid QuoteId, Guid QuoteVersionId,
    Guid QuoteLineItemId, string SourceCurrency, string SourceAmount,
    string RequiredMockCurrency, string Status, string NextRequirement,
    string FxPolicyStatus = "NOT_APPROVED",
    bool MockPaymentAuthorized = false, bool ProviderCalled = false,
    bool PaymentCompleted = false, bool CommissionAllocated = false,
    bool CreatorPayoutAuthorized = false, bool CampaignReady = false,
    bool CampaignDeliveryAuthorized = false, string Delivery = "NOT_SENT");

public static class CampaignFundingPreflight
{
    public static CampaignFundingPreflightReading Inspect(
        CreativeCampaignHandoff handoff, Campaign campaign,
        CampaignPlacement placement, CampaignPlacementRun run,
        Quote quote, QuoteVersion version, QuoteLineItem line,
        bool latestApprovalMatches, bool latestOutcomeMatches)
    {
        if (handoff is null || campaign is null || placement is null || run is null ||
            quote is null || version is null || line is null)
            throw new InvalidOperationException("Exact stored campaign lineage is required.");
        if (handoff.Status != "PLANNED" || handoff.Delivery != "NOT_SENT" ||
            campaign.Status != "DRAFT" || campaign.Id != placement.CampaignId ||
            placement.Id != handoff.CampaignPlacementId || placement.Status != "PLANNED" ||
            placement.StartAt is not null || placement.EndAt is not null ||
            run.Id != handoff.CampaignPlacementRunId || run.CampaignPlacementId != placement.Id ||
            run.CampaignId != campaign.Id || run.ContentItemId != placement.ContentItemId ||
            run.AdInventorySlotId != placement.AdInventorySlotId ||
            run.BlissMatchId != placement.BlissMatchId ||
            run.SourceSystem != "CREATIVE_HANDOFF_V1" || run.Outcome != "PLANNED" ||
            line.Id != handoff.QuoteLineItemId || line.QuoteVersionId != version.Id ||
            quote.Id != version.QuoteId || quote.AdvertiserOpportunityId is null ||
            quote.AdvertiserOpportunityId != campaign.AdvertiserOpportunityId ||
            line.Quantity != 1m || version.TotalAmount <= 0m ||
            line.LineAmount != version.TotalAmount ||
            string.IsNullOrWhiteSpace(quote.CurrencyCode) ||
            quote.CurrencyCode != version.CurrencyCode || version.CurrencyCode != line.CurrencyCode)
            throw new InvalidOperationException("Stored planned handoff and Economics quote disagree.");

        try
        {
            if (handoff.SnapshotJson is null || handoff.SnapshotJson.Length is < 2 or > 32768)
                throw new InvalidOperationException("Stored handoff snapshot is not bounded.");
            using var json = JsonDocument.Parse(handoff.SnapshotJson);
            var snapshot = json.RootElement;
            if (snapshot.GetProperty("campaignId").GetGuid() != campaign.Id ||
                snapshot.GetProperty("quoteId").GetGuid() != quote.Id ||
                snapshot.GetProperty("quoteVersionId").GetGuid() != version.Id ||
                snapshot.GetProperty("quoteLineId").GetGuid() != line.Id ||
                snapshot.GetProperty("lineAmount").GetDecimal() != line.LineAmount ||
                snapshot.GetProperty("currencyCode").GetString() != quote.CurrencyCode ||
                snapshot.GetProperty("slotId").GetGuid() != placement.AdInventorySlotId ||
                snapshot.GetProperty("matchId").GetGuid() != run.BlissMatchId ||
                snapshot.GetProperty("contentItemId").GetGuid() != run.ContentItemId)
                throw new InvalidOperationException("Frozen handoff snapshot differs from stored quote.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or OverflowException)
        {
            throw new InvalidOperationException("Frozen handoff snapshot could not be verified.", ex);
        }

        var current = quote.Status == "ACCEPTED" &&
            quote.CurrentVersionNumber == version.VersionNumber &&
            latestApprovalMatches && latestOutcomeMatches;
        var currency = quote.CurrencyCode;
        var status = !current ? "QUOTE_AUTHORITY_CHANGED" :
            currency != "USD" ? "SOURCE_CURRENCY_NOT_USD" :
            "USD_QUOTE_STILL_REQUIRES_FINANCIAL_AUTHORITY";
        var requirement = !current
            ? "Refresh Economics approvals, accepted outcome, and current quote version; no funding is authorized."
            : currency != "USD"
                ? "Obtain a separately accepted USD quote or an owner-approved FX/settlement design; never relabel the source amount."
                : "Separately authorize collecting merchant, payee, review scope, mock funding binding and settlement terms.";

        return new CampaignFundingPreflightReading(
            handoff.Id, campaign.Id, quote.Id, version.Id, line.Id,
            currency, version.TotalAmount.ToString("0.############################", CultureInfo.InvariantCulture),
            "USD", status, requirement);
    }
}
