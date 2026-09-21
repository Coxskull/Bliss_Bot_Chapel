using System.Text.Json;
using Bliss.Api.Contracts;
using Bliss.Api.Runtime;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/audit")]
public sealed class AuditExportController(
    BlissDbContext db,
    IWebHostEnvironment environment,
    OperationalEventStore events) : ControllerBase
{
    public const int RecordLimit = 250;

    [HttpGet("export/matches/{id:guid}")]
    public async Task<IActionResult> ExportMatchCase(Guid id, CancellationToken cancellationToken)
    {
        var match = await db.BlissMatches.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new BlissMatchSummaryDto(
                x.Id, x.CreatorId, x.AdvertiserOpportunityId, x.RuleVersionId,
                x.Status, x.OverallScore, x.ConfidenceScore, x.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        if (match is null)
        {
            return NotFound();
        }

        var scoreComponents = await db.MatchScoreComponents.AsNoTracking()
            .Where(x => x.BlissMatchId == id)
            .OrderBy(x => x.ComponentName)
            .Select(x => new MatchScoreComponentDto(x.Id, x.ComponentName, x.Score, x.Weight, x.Explanation))
            .ToListAsync(cancellationToken);

        var eligibility = await db.EligibilityChecks.AsNoTracking()
            .Where(x => x.BlissMatchId == id)
            .OrderBy(x => x.CheckType)
            .Select(x => new EligibilityCheckDto(x.Id, x.CheckType, x.Result, x.ReasonCode, x.Explanation))
            .ToListAsync(cancellationToken);

        var evaluations = await db.MatchEvaluationRuns.AsNoTracking()
            .Where(x => x.BlissMatchId == id)
            .OrderByDescending(x => x.CompletedAt ?? x.StartedAt)
            .ThenBy(x => x.Id)
            .Take(RecordLimit)
            .Select(x => new MatchEvaluationRunSummaryDto(
                x.Id, x.BlissMatchId, x.CreatorId, x.RuleVersionId, x.AlgorithmVersion,
                x.Status, x.MatchStatus, x.OverallScore, x.ConfidenceScore, x.StartedAt, x.CompletedAt))
            .ToListAsync(cancellationToken);

        var formations = await db.MatchFormationRuns.AsNoTracking()
            .Where(x => x.BlissMatchId == id)
            .OrderByDescending(x => x.CompletedAt)
            .ThenBy(x => x.Id)
            .Take(RecordLimit)
            .Select(x => new MatchFormationRunSummaryDto(
                x.Id, x.BlissMatchId, x.CreatorId, x.AdvertiserOpportunityId, x.RuleVersionId,
                x.SourceSystem, x.IdempotencyKey, x.Status, x.Outcome, x.EvaluateOnCreate, x.CompletedAt))
            .ToListAsync(cancellationToken);

        var reviews = await db.MatchReviewDecisions.AsNoTracking()
            .Where(x => x.BlissMatchId == id)
            .OrderByDescending(x => x.CompletedAt)
            .ThenBy(x => x.Id)
            .Take(RecordLimit)
            .Select(x => new MatchReviewDecisionSummaryDto(
                x.Id, x.BlissMatchId, x.CreatorId, x.MatchEvaluationRunId, x.SourceSystem,
                x.IdempotencyKey, x.ReviewerLabel, x.Decision, x.ResultingMatchStatus, x.CompletedAt))
            .ToListAsync(cancellationToken);

        var placements = await db.CampaignPlacementRuns.AsNoTracking()
            .Where(x => x.BlissMatchId == id)
            .OrderByDescending(x => x.CompletedAt)
            .ThenBy(x => x.Id)
            .Take(RecordLimit)
            .Select(x => new CampaignPlacementRunSummaryDto(
                x.Id, x.CampaignPlacementId, x.BlissMatchId, x.CampaignId, x.CreatorId,
                x.AdvertiserOpportunityId, x.ContentItemId, x.AdInventorySlotId, x.SourceSystem,
                x.IdempotencyKey, x.OperatorLabel, x.Status, x.Outcome, x.CompletedAt))
            .ToListAsync(cancellationToken);

        var requestId = RequestCorrelation.Resolve(HttpContext);
        var exportedAt = DateTime.UtcNow;
        var contentSha256 = ExportIntegrity.Sha256Hex(new Dictionary<string, object?>
        {
            ["match"] = match,
            ["scoreComponents"] = scoreComponents,
            ["eligibilityChecks"] = eligibility,
            ["evaluations"] = evaluations,
            ["formations"] = formations,
            ["reviews"] = reviews,
            ["placements"] = placements
        });
        var payload = new MatchCaseExportDto(
            exportedAt,
            environment.EnvironmentName,
            "match-case",
            requestId,
            contentSha256,
            id,
            match,
            scoreComponents,
            eligibility,
            evaluations,
            formations,
            reviews,
            placements);

        return Pack(payload, $"bliss-match-{id:N}-{exportedAt:yyyyMMddHHmmss}Z.json", requestId, exportedAt, contentSha256);
    }

    [HttpGet("export/creators/{id:guid}")]
    public async Task<IActionResult> ExportCreatorCase(Guid id, CancellationToken cancellationToken)
    {
        var creator = await db.Creators.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CreatorListDto(
                x.Id, x.Name, x.CountryCode, x.PrimaryLanguage,
                x.AudienceSize, x.FemalePercentage, x.MalePercentage))
            .SingleOrDefaultAsync(cancellationToken);

        if (creator is null)
        {
            return NotFound();
        }

        var platforms = await db.CreatorPlatforms.AsNoTracking()
            .Where(x => x.CreatorId == id)
            .OrderBy(x => x.Platform)
            .Take(RecordLimit)
            .Select(x => new CreatorPlatformDto(
                x.Id, x.Platform, x.ExternalProfileId, x.ProfileUrl, x.Followers, x.LastCollectedAt))
            .ToListAsync(cancellationToken);

        var relatedIds = platforms.Select(x => x.Id).Append(id).ToList();

        var content = await db.ContentItems.AsNoTracking()
            .Where(x => x.CreatorId == id)
            .OrderBy(x => x.Title)
            .Take(RecordLimit)
            .Select(x => new ContentItemListDto(
                x.Id, x.CreatorId, x.ContentType, x.Title, x.ExternalContentId, x.Url))
            .ToListAsync(cancellationToken);

        var matches = await db.BlissMatches.AsNoTracking()
            .Where(x => x.CreatorId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Take(RecordLimit)
            .Select(x => new BlissMatchSummaryDto(
                x.Id, x.CreatorId, x.AdvertiserOpportunityId, x.RuleVersionId,
                x.Status, x.OverallScore, x.ConfidenceScore, x.CreatedAt))
            .ToListAsync(cancellationToken);

        var ingestions = await db.CreatorIngestionRuns.AsNoTracking()
            .Where(x => x.CreatorId == id)
            .OrderByDescending(x => x.CompletedAt)
            .ThenBy(x => x.Id)
            .Take(RecordLimit)
            .Select(x => new CreatorIngestionRunSummaryDto(
                x.Id, x.CreatorId, x.CreatorPlatformId, x.SourceSystem, x.IdempotencyKey,
                x.IdentityKey, x.Status, x.Outcome, x.CompletedAt))
            .ToListAsync(cancellationToken);

        var provenances = await db.DataProvenances.AsNoTracking()
            .Where(x => relatedIds.Contains(x.EntityId))
            .OrderByDescending(x => x.CollectedAt)
            .ThenBy(x => x.Id)
            .Take(RecordLimit)
            .Select(x => new DataProvenanceDto(
                x.Id, x.EntityType, x.EntityId, x.FieldName, x.SourceType, x.SourceName,
                x.SourceUrl, x.ConfidenceLevel, x.CollectedAt, x.Notes))
            .ToListAsync(cancellationToken);

        var requestId = RequestCorrelation.Resolve(HttpContext);
        var exportedAt = DateTime.UtcNow;
        var contentSha256 = ExportIntegrity.Sha256Hex(new Dictionary<string, object?>
        {
            ["creator"] = creator,
            ["platforms"] = platforms,
            ["content"] = content,
            ["matches"] = matches,
            ["ingestions"] = ingestions,
            ["provenances"] = provenances
        });
        var payload = new CreatorCaseExportDto(
            exportedAt,
            environment.EnvironmentName,
            "creator-case",
            requestId,
            contentSha256,
            id,
            creator,
            platforms,
            content,
            matches,
            ingestions,
            provenances);

        return Pack(payload, $"bliss-creator-{id:N}-{exportedAt:yyyyMMddHHmmss}Z.json", requestId, exportedAt, contentSha256);
    }

    [HttpGet("export/campaigns/{id:guid}")]
    public async Task<IActionResult> ExportCampaignCase(Guid id, CancellationToken cancellationToken)
    {
        var campaign = await db.Campaigns.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CampaignListDto(x.Id, x.AdvertiserOpportunityId, x.Name, x.Status, x.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        if (campaign is null)
        {
            return NotFound();
        }

        var placements = await db.CampaignPlacements.AsNoTracking()
            .Where(x => x.CampaignId == id)
            .OrderBy(x => x.Id)
            .Take(RecordLimit)
            .Select(x => new CampaignPlacementDto(
                x.Id, x.CampaignId, x.ContentItemId, x.AdInventorySlotId,
                x.BlissMatchId, x.Status, x.StartAt, x.EndAt))
            .ToListAsync(cancellationToken);

        var matchIds = placements
            .Where(x => x.BlissMatchId.HasValue)
            .Select(x => x.BlissMatchId!.Value)
            .Distinct()
            .ToList();

        var matches = matchIds.Count == 0
            ? new List<BlissMatchSummaryDto>()
            : await db.BlissMatches.AsNoTracking()
                .Where(x => matchIds.Contains(x.Id))
                .OrderByDescending(x => x.CreatedAt)
                .Take(RecordLimit)
                .Select(x => new BlissMatchSummaryDto(
                    x.Id, x.CreatorId, x.AdvertiserOpportunityId, x.RuleVersionId,
                    x.Status, x.OverallScore, x.ConfidenceScore, x.CreatedAt))
                .ToListAsync(cancellationToken);

        var placementRuns = await db.CampaignPlacementRuns.AsNoTracking()
            .Where(x => x.CampaignId == id)
            .OrderByDescending(x => x.CompletedAt)
            .ThenBy(x => x.Id)
            .Take(RecordLimit)
            .Select(x => new CampaignPlacementRunSummaryDto(
                x.Id, x.CampaignPlacementId, x.BlissMatchId, x.CampaignId, x.CreatorId,
                x.AdvertiserOpportunityId, x.ContentItemId, x.AdInventorySlotId, x.SourceSystem,
                x.IdempotencyKey, x.OperatorLabel, x.Status, x.Outcome, x.CompletedAt))
            .ToListAsync(cancellationToken);

        var requestId = RequestCorrelation.Resolve(HttpContext);
        var exportedAt = DateTime.UtcNow;
        var contentSha256 = ExportIntegrity.Sha256Hex(new Dictionary<string, object?>
        {
            ["campaign"] = campaign,
            ["placements"] = placements,
            ["placementRuns"] = placementRuns,
            ["matches"] = matches
        });
        var payload = new CampaignCaseExportDto(
            exportedAt,
            environment.EnvironmentName,
            "campaign-case",
            requestId,
            contentSha256,
            id,
            campaign,
            placements,
            placementRuns,
            matches);

        return Pack(payload, $"bliss-campaign-{id:N}-{exportedAt:yyyyMMddHHmmss}Z.json", requestId, exportedAt, contentSha256);
    }

    [HttpGet("export/{ledger}")]
    public async Task<IActionResult> Export(string ledger, CancellationToken cancellationToken)
    {
        var kind = (ledger ?? string.Empty).Trim().ToLowerInvariant();
        object records;
        switch (kind)
        {
            case "evaluations":
                records = await db.MatchEvaluationRuns.AsNoTracking()
                    .OrderByDescending(x => x.CompletedAt ?? x.StartedAt)
                    .ThenBy(x => x.Id)
                    .Take(RecordLimit)
                    .Select(x => new MatchEvaluationRunSummaryDto(
                        x.Id, x.BlissMatchId, x.CreatorId, x.RuleVersionId, x.AlgorithmVersion,
                        x.Status, x.MatchStatus, x.OverallScore, x.ConfidenceScore, x.StartedAt, x.CompletedAt))
                    .ToListAsync(cancellationToken);
                break;
            case "formations":
                records = await db.MatchFormationRuns.AsNoTracking()
                    .OrderByDescending(x => x.CompletedAt)
                    .ThenBy(x => x.Id)
                    .Take(RecordLimit)
                    .Select(x => new MatchFormationRunSummaryDto(
                        x.Id, x.BlissMatchId, x.CreatorId, x.AdvertiserOpportunityId, x.RuleVersionId,
                        x.SourceSystem, x.IdempotencyKey, x.Status, x.Outcome, x.EvaluateOnCreate, x.CompletedAt))
                    .ToListAsync(cancellationToken);
                break;
            case "reviews":
                records = await db.MatchReviewDecisions.AsNoTracking()
                    .OrderByDescending(x => x.CompletedAt)
                    .ThenBy(x => x.Id)
                    .Take(RecordLimit)
                    .Select(x => new MatchReviewDecisionSummaryDto(
                        x.Id, x.BlissMatchId, x.CreatorId, x.MatchEvaluationRunId, x.SourceSystem,
                        x.IdempotencyKey, x.ReviewerLabel, x.Decision, x.ResultingMatchStatus, x.CompletedAt))
                    .ToListAsync(cancellationToken);
                break;
            case "placements":
                records = await db.CampaignPlacementRuns.AsNoTracking()
                    .OrderByDescending(x => x.CompletedAt)
                    .ThenBy(x => x.Id)
                    .Take(RecordLimit)
                    .Select(x => new CampaignPlacementRunSummaryDto(
                        x.Id, x.CampaignPlacementId, x.BlissMatchId, x.CampaignId, x.CreatorId,
                        x.AdvertiserOpportunityId, x.ContentItemId, x.AdInventorySlotId, x.SourceSystem,
                        x.IdempotencyKey, x.OperatorLabel, x.Status, x.Outcome, x.CompletedAt))
                    .ToListAsync(cancellationToken);
                break;
            case "ingestions":
                records = await db.CreatorIngestionRuns.AsNoTracking()
                    .OrderByDescending(x => x.CompletedAt)
                    .ThenBy(x => x.Id)
                    .Take(RecordLimit)
                    .Select(x => new CreatorIngestionRunSummaryDto(
                        x.Id, x.CreatorId, x.CreatorPlatformId, x.SourceSystem, x.IdempotencyKey,
                        x.IdentityKey, x.Status, x.Outcome, x.CompletedAt))
                    .ToListAsync(cancellationToken);
                break;
            case "provenance":
                records = await db.DataProvenances.AsNoTracking()
                    .OrderByDescending(x => x.CollectedAt)
                    .ThenBy(x => x.Id)
                    .Take(RecordLimit)
                    .Select(x => new DataProvenanceDto(
                        x.Id, x.EntityType, x.EntityId, x.FieldName, x.SourceType, x.SourceName,
                        x.SourceUrl, x.ConfidenceLevel, x.CollectedAt, x.Notes))
                    .ToListAsync(cancellationToken);
                break;
            default:
                return BadRequest(new { error = "Unsupported ledger." });
        }

        var list = (System.Collections.ICollection)records;
        var total = kind switch
        {
            "evaluations" => await db.MatchEvaluationRuns.CountAsync(cancellationToken),
            "formations" => await db.MatchFormationRuns.CountAsync(cancellationToken),
            "reviews" => await db.MatchReviewDecisions.CountAsync(cancellationToken),
            "placements" => await db.CampaignPlacementRuns.CountAsync(cancellationToken),
            "ingestions" => await db.CreatorIngestionRuns.CountAsync(cancellationToken),
            _ => await db.DataProvenances.CountAsync(cancellationToken)
        };

        var requestId = RequestCorrelation.Resolve(HttpContext);
        var exportedAt = DateTime.UtcNow;
        var contentSha256 = ExportIntegrity.Sha256Hex(records);
        var payload = new AuditExportDto(
            exportedAt,
            environment.EnvironmentName,
            kind,
            requestId,
            contentSha256,
            list.Count,
            RecordLimit,
            total > RecordLimit,
            records);

        return Pack(payload, $"bliss-{kind}-{exportedAt:yyyyMMddHHmmss}Z.json", requestId, exportedAt, contentSha256);
    }

    [HttpPost("verify")]
    [RequestSizeLimit(ExportIntegrity.VerifyLimitBytes)]
    public async Task<IActionResult> Verify(CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > ExportIntegrity.VerifyLimitBytes)
        {
            return BadRequest(new { error = "Pack exceeds the 512 KB verification limit." });
        }

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "Pack must be valid JSON." });
        }

        using (document)
        {
            if (!ExportIntegrity.TryVerify(document.RootElement, out var result, out var error))
            {
                return BadRequest(new { error });
            }

            var requestId = RequestCorrelation.Resolve(HttpContext);
            var verifiedAt = DateTime.UtcNow;
            var receipt = result with { RequestId = requestId, VerifiedAt = verifiedAt };
            events.Record(new OperationalEvent(
                verifiedAt,
                "AuditVerified",
                HttpContext.Request.Method,
                RequestCorrelation.SafePath(HttpContext),
                StatusCodes.Status200OK,
                requestId,
                ExportIntegrity.ReceiptDetail(receipt)));
            return Ok(receipt);
        }
    }

    private FileContentResult Pack(object payload, string fileName, string requestId, DateTime exportedAt, string contentSha256)
    {
        events.Record(new OperationalEvent(
            exportedAt,
            "AuditExported",
            HttpContext.Request.Method,
            RequestCorrelation.SafePath(HttpContext),
            StatusCodes.Status200OK,
            requestId));
        Response.Headers[ExportIntegrity.HeaderName] = contentSha256;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, ExportIntegrity.JsonOptions);
        return File(bytes, "application/json", fileName);
    }
}

public sealed record AuditExportDto(
    DateTime ExportedAt,
    string Environment,
    string Ledger,
    string RequestId,
    string ContentSha256,
    int RecordCount,
    int Limit,
    bool Truncated,
    object Records);

public sealed record MatchCaseExportDto(
    DateTime ExportedAt,
    string Environment,
    string Kind,
    string RequestId,
    string ContentSha256,
    Guid MatchId,
    BlissMatchSummaryDto Match,
    IReadOnlyList<MatchScoreComponentDto> ScoreComponents,
    IReadOnlyList<EligibilityCheckDto> EligibilityChecks,
    IReadOnlyList<MatchEvaluationRunSummaryDto> Evaluations,
    IReadOnlyList<MatchFormationRunSummaryDto> Formations,
    IReadOnlyList<MatchReviewDecisionSummaryDto> Reviews,
    IReadOnlyList<CampaignPlacementRunSummaryDto> Placements);

public sealed record CreatorCaseExportDto(
    DateTime ExportedAt,
    string Environment,
    string Kind,
    string RequestId,
    string ContentSha256,
    Guid CreatorId,
    CreatorListDto Creator,
    IReadOnlyList<CreatorPlatformDto> Platforms,
    IReadOnlyList<ContentItemListDto> Content,
    IReadOnlyList<BlissMatchSummaryDto> Matches,
    IReadOnlyList<CreatorIngestionRunSummaryDto> Ingestions,
    IReadOnlyList<DataProvenanceDto> Provenances);

public sealed record CampaignCaseExportDto(
    DateTime ExportedAt,
    string Environment,
    string Kind,
    string RequestId,
    string ContentSha256,
    Guid CampaignId,
    CampaignListDto Campaign,
    IReadOnlyList<CampaignPlacementDto> Placements,
    IReadOnlyList<CampaignPlacementRunSummaryDto> PlacementRuns,
    IReadOnlyList<BlissMatchSummaryDto> Matches);
