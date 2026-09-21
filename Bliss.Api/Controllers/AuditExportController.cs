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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

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
        var payload = new AuditExportDto(
            exportedAt,
            environment.EnvironmentName,
            kind,
            requestId,
            list.Count,
            RecordLimit,
            total > RecordLimit,
            records);

        events.Record(new OperationalEvent(
            exportedAt,
            "AuditExported",
            HttpContext.Request.Method,
            RequestCorrelation.SafePath(HttpContext),
            StatusCodes.Status200OK,
            requestId));

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var fileName = $"bliss-{kind}-{exportedAt:yyyyMMddHHmmss}Z.json";
        return File(bytes, "application/json", fileName);
    }
}

public sealed record AuditExportDto(
    DateTime ExportedAt,
    string Environment,
    string Ledger,
    string RequestId,
    int RecordCount,
    int Limit,
    bool Truncated,
    object Records);
