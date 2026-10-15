using System.Text.Json;
using Bliss.Api.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

// A provenance diagnostic, NOT a consent, transcript export, campaign activation
// or evidence of business approval. Never returns the visitor's conversation text.
public sealed record AskAlphaLineagePreflightReading(
    Guid HandoffId, Guid CampaignId, string Status,
    int VisitorTurnCount = 0, int AssistantTurnCount = 0,
    string ConsentStatus = "UNRECORDED",
    bool SourceMetadataMatched = false,
    bool TranscriptLinked = false,
    bool TranscriptExportAuthorized = false,
    bool ProspectOutreachAuthorized = false,
    bool PaymentAuthorized = false,
    bool CreatorPayoutAuthorized = false,
    bool CampaignReady = false,
    bool CampaignDeliveryAuthorized = false,
    string Delivery = "NOT_SENT");

public static class AskAlphaLineagePolicy
{
    public static AskAlphaLineagePreflightReading Inspect(
        CreativeCampaignHandoff handoff, Campaign campaign,
        CampaignPlacement placement, CampaignPlacementRun run,
        Quote quote, QuoteVersion version, QuoteLineItem line,
        WeddingPlannerWorkspace workspace, WeddingPlannerPlanningSession? session,
        DemonstrationRecord? prospect)
    {
        if (handoff.WorkspaceId != workspace.Id || campaign.Status != "DRAFT" ||
            handoff.Status != "PLANNED" || handoff.Delivery != "NOT_SENT" ||
            handoff.CampaignPlacementId != placement.Id ||
            handoff.CampaignPlacementRunId != run.Id ||
            run.CampaignId != campaign.Id || placement.CampaignId != campaign.Id ||
            placement.Status != "PLANNED" || placement.StartAt.HasValue || placement.EndAt.HasValue ||
            run.SourceSystem != "CREATIVE_HANDOFF_V1" || run.Outcome != "PLANNED" ||
            run.CampaignPlacementId != placement.Id ||
            run.BlissMatchId != placement.BlissMatchId ||
            run.AdInventorySlotId != placement.AdInventorySlotId ||
            run.ContentItemId != placement.ContentItemId ||
            line.Id != handoff.QuoteLineItemId || line.QuoteVersionId != version.Id ||
            quote.Id != version.QuoteId ||
            quote.AdvertiserOpportunityId != campaign.AdvertiserOpportunityId ||
            quote.AdvertiserOpportunityId is null ||
            quote.CurrencyCode != version.CurrencyCode ||
            version.CurrencyCode != line.CurrencyCode)
            throw new InvalidOperationException("Stored campaign handoff lineage disagrees.");

        try
        {
            if (handoff.SnapshotJson is null || handoff.SnapshotJson.Length is < 2 or > 32768)
                throw new InvalidOperationException("Stored handoff snapshot cannot be verified.");
            using var json = JsonDocument.Parse(handoff.SnapshotJson);
            var snap = json.RootElement;
            if (snap.GetProperty("campaignId").GetGuid() != campaign.Id ||
                snap.GetProperty("quoteId").GetGuid() != quote.Id ||
                snap.GetProperty("quoteVersionId").GetGuid() != version.Id ||
                snap.GetProperty("quoteLineId").GetGuid() != line.Id ||
                snap.GetProperty("lineAmount").GetDecimal() != line.LineAmount ||
                snap.GetProperty("currencyCode").GetString() != quote.CurrencyCode ||
                snap.GetProperty("slotId").GetGuid() != placement.AdInventorySlotId ||
                snap.GetProperty("matchId").GetGuid() != run.BlissMatchId)
                throw new InvalidOperationException("Stored handoff snapshot cannot be verified.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or OverflowException)
        {
            throw new InvalidOperationException("Stored handoff snapshot cannot be verified.", ex);
        }

        var unavailable = new AskAlphaLineagePreflightReading(handoff.Id, campaign.Id, "NOT_LINKED");
        // Do not expose even a turn count from a different advertiser, workspace,
        // campaign or purported quote; source metadata is not an authorization.
        if (prospect is null || session is null || prospect.Suppressed ||
            !prospect.Illustrative ||
            session.WorkspaceId != workspace.Id || session.AdvertiserId != workspace.AdvertiserId ||
            !Guid.TryParse(prospect.WeddingPlannerWorkspaceId, out var wp) || wp != workspace.Id ||
            !Guid.TryParse(prospect.WeddingPlannerSessionId, out var sid) || sid != session.Id ||
            !Guid.TryParse(prospect.EconomicsQuoteId, out var qid) || qid != quote.Id)
            return unavailable;

        var messages = prospect.Messages;
        if (messages is null || messages.Count > 10000) return unavailable;
        var visitor = messages.Count(x => x.Role == "VISITOR");
        var assistant = messages.Count(x => x.Role == "ASSISTANT");
        if (visitor < 1 || assistant < 1) return unavailable;
        // Historical file-backed Ask Alpha turns are mutable and carry no
        // authenticated consent provenance. They stay unlinked and unexportable.
        return unavailable with
        {
            Status = "SOURCE_REFERENCES_MATCH_CONSENT_REQUIRED",
            VisitorTurnCount = visitor,
            AssistantTurnCount = assistant,
            SourceMetadataMatched = true
        };
    }
}

public sealed class AskAlphaLineagePreflightService(
    BlissDbContext db, ProspectDemonstrationService demonstrations)
{
    public async Task<AskAlphaLineagePreflightReading> Inspect(
        Guid workspaceId, Guid handoffId, string slug, CancellationToken ct)
    {
        if (handoffId == Guid.Empty || string.IsNullOrWhiteSpace(slug) ||
            slug.Length > 96 || !System.Text.RegularExpressions.Regex.IsMatch(
                slug, @"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Exact bounded source slug and handoff UUID required.");

        // Controller must check authenticated workspace access BEFORE this method.
        var handoff = await db.CreativeCampaignHandoffs.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handoffId && x.WorkspaceId == workspaceId, ct)
            ?? throw new WeddingPlannerNotFoundException("This workspace's handoff was not found.");
        var workspace = await db.WeddingPlannerWorkspaces.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == workspaceId, ct)
            ?? throw new WeddingPlannerNotFoundException("This workspace was not found.");
        var run = await db.CampaignPlacementRuns.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handoff.CampaignPlacementRunId, ct)
            ?? throw new InvalidOperationException("Planned placement run missing.");
        var placement = await db.CampaignPlacements.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handoff.CampaignPlacementId, ct)
            ?? throw new InvalidOperationException("Planned placement missing.");
        var campaign = await db.Campaigns.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == run.CampaignId, ct)
            ?? throw new InvalidOperationException("Draft campaign missing.");
        var line = await db.QuoteLineItems.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handoff.QuoteLineItemId, ct)
            ?? throw new InvalidOperationException("Economics quote line missing.");
        var version = await db.QuoteVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == line.QuoteVersionId, ct)
            ?? throw new InvalidOperationException("Economics quote version missing.");
        var quote = await db.Quotes.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == version.QuoteId, ct)
            ?? throw new InvalidOperationException("Economics quote missing.");

        // Untrusted prospect metadata is read only AFTER workspace ownership
        // and the exact campaign IDs have been independently established.
        var prospect = demonstrations.Find(slug);
        Guid? candidateSession = prospect is not null &&
            Guid.TryParse(prospect.WeddingPlannerWorkspaceId, out var foreignWorkspace) &&
            foreignWorkspace == workspaceId &&
            Guid.TryParse(prospect.WeddingPlannerSessionId, out var id) ? id : null;
        var session = candidateSession is null ? null : await db.WeddingPlannerPlanningSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == candidateSession.Value &&
                x.WorkspaceId == workspaceId && x.AdvertiserId == workspace.AdvertiserId, ct);
        return AskAlphaLineagePolicy.Inspect(handoff, campaign, placement, run,
            quote, version, line, workspace, session, prospect);
    }
}
