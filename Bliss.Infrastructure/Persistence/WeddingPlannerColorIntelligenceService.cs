using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record WeddingPlannerColorProfileVersionResult(
    Guid ColorProfileVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string AlgorithmVersion,
    Guid ApprovedBrandDnaVersionId,
    string DocumentJson,
    string Summary,
    string InputJson,
    string InputSha256,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime CreatedAt,
    bool IsCurrentApproved,
    bool IsReplay);

public sealed record WeddingPlannerColorProfileListResult(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? CurrentApprovedColorProfileVersionId,
    IReadOnlyList<WeddingPlannerColorProfileVersionResult> Versions);

public sealed record WeddingPlannerColorProfileDecisionResult(
    Guid DecisionId,
    Guid ColorProfileVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Decision,
    string ActorType,
    string ActorLabel,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerColorProfileVersionResult Version,
    bool IsReplay);

/// <summary>
/// Deterministic Color Intelligence application service. Separate from AI orchestration;
/// must not resolve or call any AI provider interface or create agent runs.
/// </summary>
public sealed class WeddingPlannerColorIntelligenceService
{
    private readonly BlissDbContext _db;
    private readonly WeddingPlannerService _planner;

    public WeddingPlannerColorIntelligenceService(BlissDbContext db, WeddingPlannerService planner)
    {
        _db = db;
        _planner = planner;
    }

    public async Task<WeddingPlannerColorProfileVersionResult> ComputeAsync(
        Guid workspaceId,
        string primaryHex,
        string? secondaryHex,
        string? accentHex,
        string? backgroundHex,
        string? surfaceHex,
        string? notes,
        string sourceSystem,
        string idempotencyKey,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), 128);

        var existing = await _db.WeddingPlannerColorProfileVersions
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            if (existing.WorkspaceId != workspace.Id || existing.AdvertiserId != workspace.AdvertiserId)
            {
                throw new WeddingPlannerNotFoundException("Color profile version was not found.");
            }

            _planner.AddAuditForOrchestration(
                existing.AdvertiserId,
                existing.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.ColorProfileReplayed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Replayed,
                requestId,
                Truncate($"Color profile {existing.Id} replayed.", 2000));
            await _db.SaveChangesAsync(cancellationToken);
            return ToResult(existing, workspace.CurrentApprovedColorProfileVersionId == existing.Id, true);
        }

        if (workspace.CurrentApprovedBrandDnaVersionId is null)
        {
            throw new InvalidOperationException(
                "A current-approved Brand DNA version is required before computing a color profile.");
        }

        var brandDna = await _db.WeddingPlannerBrandDnaVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == workspace.CurrentApprovedBrandDnaVersionId.Value, cancellationToken);
        if (brandDna is null || brandDna.WorkspaceId != workspace.Id)
        {
            throw new InvalidOperationException(
                "A current-approved Brand DNA version is required before computing a color profile.");
        }

        // Brand DNA is provenance only — DocumentJson/Summary are never parsed for colors.
        var computed = AciHslV1.Compute(
            primaryHex,
            secondaryHex,
            accentHex,
            backgroundHex,
            surfaceHex,
            notes,
            brandDna.Id,
            brandDna.VersionNumber);

        var nextVersion = await _db.WeddingPlannerColorProfileVersions
            .Where(x => x.WorkspaceId == workspace.Id)
            .Select(x => (int?)x.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var now = DateTime.UtcNow;
        var version = new WeddingPlannerColorProfileVersion
        {
            Id = Guid.NewGuid(),
            AdvertiserId = workspace.AdvertiserId,
            WorkspaceId = workspace.Id,
            VersionNumber = nextVersion + 1,
            SchemaVersion = WeddingPlannerSchemaVersions.ColorProfileV1,
            AlgorithmVersion = WeddingPlannerAlgorithmVersions.AciHslV1,
            ApprovedBrandDnaVersionId = brandDna.Id,
            DocumentJson = computed.DocumentJson,
            Summary = Truncate(computed.Summary, 2000),
            InputJson = computed.InputJson,
            InputSha256 = computed.InputSha256,
            Status = WeddingPlannerColorProfileStatuses.Proposed,
            SourceSystem = source,
            IdempotencyKey = key,
            ActorType = actorType,
            ActorLabel = actorLabel,
            CreatedAt = now
        };
        _db.WeddingPlannerColorProfileVersions.Add(version);
        _planner.AddAuditForOrchestration(
            version.AdvertiserId,
            version.WorkspaceId,
            null,
            null,
            WeddingPlannerAuditActions.ColorProfileProposed,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Proposed,
            requestId,
            Truncate($"Color profile {version.Id} proposed as v{version.VersionNumber}.", 2000));
        await _db.SaveChangesAsync(cancellationToken);

        return ToResult(version, false, false);
    }

    public async Task<WeddingPlannerColorProfileListResult> ListAsync(
        Guid workspaceId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var versions = await _db.WeddingPlannerColorProfileVersions
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderBy(x => x.VersionNumber)
            .ToListAsync(cancellationToken);

        return new WeddingPlannerColorProfileListResult(
            workspace.Id,
            workspace.AdvertiserId,
            workspace.CurrentApprovedColorProfileVersionId,
            versions.Select(x => ToResult(x, workspace.CurrentApprovedColorProfileVersionId == x.Id, false)).ToList());
    }

    public async Task<WeddingPlannerColorProfileVersionResult> GetAsync(
        Guid colorProfileVersionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerColorProfileVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == colorProfileVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Color profile version was not found.");
        }

        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            version.WorkspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        return ToResult(version, workspace.CurrentApprovedColorProfileVersionId == version.Id, false);
    }

    public async Task<WeddingPlannerColorProfileDecisionResult> DecideAsync(
        Guid colorProfileVersionId,
        string decision,
        string? rationale,
        string sourceSystem,
        string idempotencyKey,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), 128);
        var normalizedDecision = Required(decision, nameof(decision), 32).ToUpperInvariant();
        if (normalizedDecision is not (WeddingPlannerColorProfileDecisions.Approve or WeddingPlannerColorProfileDecisions.Reject))
        {
            throw new InvalidOperationException("Decision must be APPROVE or REJECT.");
        }

        var existing = await _db.WeddingPlannerColorProfileDecisions
            .Include(x => x.ColorProfileVersion)
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            var workspaceReplay = await _planner.RequireWorkspaceForOrchestrationAsync(
                existing.WorkspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
            if (existing.ColorProfileVersionId != colorProfileVersionId)
            {
                throw new WeddingPlannerNotFoundException("Color profile decision was not found.");
            }

            _planner.AddAuditForOrchestration(
                existing.AdvertiserId,
                existing.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.ColorProfileReplayed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Replayed,
                requestId,
                Truncate($"Color profile decision {existing.Id} replayed for profile {existing.ColorProfileVersionId}.", 2000));
            await _db.SaveChangesAsync(cancellationToken);

            return new WeddingPlannerColorProfileDecisionResult(
                existing.Id,
                existing.ColorProfileVersionId,
                existing.WorkspaceId,
                existing.AdvertiserId,
                existing.Decision,
                existing.ActorType,
                existing.ActorLabel,
                existing.Rationale,
                existing.SourceSystem,
                existing.IdempotencyKey,
                existing.OccurredAt,
                ToResult(
                    existing.ColorProfileVersion,
                    workspaceReplay.CurrentApprovedColorProfileVersionId == existing.ColorProfileVersionId,
                    true),
                true);
        }

        var version = await _db.WeddingPlannerColorProfileVersions
            .SingleOrDefaultAsync(x => x.Id == colorProfileVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Color profile version was not found.");
        }

        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            version.WorkspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var decisionRationale = Required(rationale, nameof(rationale), 2000);

        if (!string.Equals(version.Status, WeddingPlannerColorProfileStatuses.Proposed, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Only PROPOSED color profile versions can receive a decision. Current status is {version.Status}.");
        }

        var originalDocument = version.DocumentJson;
        var originalSummary = version.Summary;
        var originalInputJson = version.InputJson;
        var originalInputSha = version.InputSha256;
        var now = DateTime.UtcNow;
        var row = new WeddingPlannerColorProfileDecision
        {
            Id = Guid.NewGuid(),
            AdvertiserId = version.AdvertiserId,
            WorkspaceId = version.WorkspaceId,
            ColorProfileVersionId = version.Id,
            Decision = normalizedDecision,
            ActorType = actorType,
            ActorLabel = actorLabel,
            Rationale = decisionRationale,
            SourceSystem = source,
            IdempotencyKey = key,
            OccurredAt = now
        };
        _db.WeddingPlannerColorProfileDecisions.Add(row);

        if (normalizedDecision == WeddingPlannerColorProfileDecisions.Approve)
        {
            if (workspace.CurrentApprovedColorProfileVersionId is Guid previousId && previousId != version.Id)
            {
                var previous = await _db.WeddingPlannerColorProfileVersions
                    .SingleOrDefaultAsync(x => x.Id == previousId, cancellationToken);
                if (previous is not null
                    && string.Equals(previous.Status, WeddingPlannerColorProfileStatuses.Approved, StringComparison.Ordinal))
                {
                    previous.Status = WeddingPlannerColorProfileStatuses.Superseded;
                    _planner.AddAuditForOrchestration(
                        previous.AdvertiserId,
                        previous.WorkspaceId,
                        null,
                        null,
                        WeddingPlannerAuditActions.ColorProfileSuperseded,
                        actorType,
                        actorLabel,
                        WeddingPlannerOutcomes.Superseded,
                        requestId,
                        Truncate($"Color profile {previous.Id} superseded by {version.Id}.", 2000));
                }
            }

            version.Status = WeddingPlannerColorProfileStatuses.Approved;
            workspace.CurrentApprovedColorProfileVersionId = version.Id;
            workspace.UpdatedAt = now;
            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.ColorProfileApproved,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Approved,
                requestId,
                Truncate($"Color profile {version.Id} approved as v{version.VersionNumber}.", 2000));
        }
        else
        {
            version.Status = WeddingPlannerColorProfileStatuses.Rejected;
            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.ColorProfileRejected,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Rejected,
                requestId,
                Truncate($"Color profile {version.Id} rejected.", 2000));
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (!string.Equals(version.DocumentJson, originalDocument, StringComparison.Ordinal)
            || !string.Equals(version.Summary, originalSummary, StringComparison.Ordinal)
            || !string.Equals(version.InputJson, originalInputJson, StringComparison.Ordinal)
            || !string.Equals(version.InputSha256, originalInputSha, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Color profile DocumentJson, Summary, InputJson, and InputSha256 are immutable.");
        }

        return new WeddingPlannerColorProfileDecisionResult(
            row.Id,
            row.ColorProfileVersionId,
            row.WorkspaceId,
            row.AdvertiserId,
            row.Decision,
            row.ActorType,
            row.ActorLabel,
            row.Rationale,
            row.SourceSystem,
            row.IdempotencyKey,
            row.OccurredAt,
            ToResult(version, workspace.CurrentApprovedColorProfileVersionId == version.Id, false),
            false);
    }

    private static WeddingPlannerColorProfileVersionResult ToResult(
        WeddingPlannerColorProfileVersion version,
        bool isCurrentApproved,
        bool isReplay) =>
        new(
            version.Id,
            version.AdvertiserId,
            version.WorkspaceId,
            version.VersionNumber,
            version.SchemaVersion,
            version.AlgorithmVersion,
            version.ApprovedBrandDnaVersionId,
            version.DocumentJson,
            version.Summary,
            version.InputJson,
            version.InputSha256,
            version.Status,
            version.SourceSystem,
            version.IdempotencyKey,
            version.ActorType,
            version.ActorLabel,
            version.CreatedAt,
            isCurrentApproved,
            isReplay);

    private static string Required(string? value, string name, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException($"{name} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"{name} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
