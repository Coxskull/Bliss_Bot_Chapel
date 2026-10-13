using System.Text.Json;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Api.Operations;

public sealed record RecordCampaignObservation(Guid HandoffId, string Kind, string SourceLabel, string Evidence, DateTime ObservedAt,
    DateTime? WindowStart, DateTime? WindowEnd, long? Impressions, long? Clicks, long? Conversions, bool AcknowledgeUnverifiedReport, string IdempotencyKey);
public sealed record ProposeCampaignLearning(Guid ObservationId, string Hypothesis, string Experiment, string SuccessCriterion, bool ApplyToProduction, string IdempotencyKey);
public sealed record ReviewCampaignLearning(int ExpectedVersion, string Decision, string Rationale, bool ApplyToProduction, string IdempotencyKey);

public sealed class CampaignMeasurementService(BlissDbContext db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Hash(object value) => ControlledAiService.Hash(JsonSerializer.Serialize(value, Json));
    private static string Text(string? value, int max, string name)
    {
        var text = (value ?? "").Trim();
        if (text.Length is 0 || text.Length > max) throw new InvalidOperationException($"{name} is required and must be at most {max} characters.");
        return text;
    }
    private static string Key(string? value) { var key = Text(value, 80, "Retry key"); CreativeApproval.RequireKey(key); return key; }
    private static void Same(string stored, string current) { if (stored != current) throw new WeddingPlannerConflictException("This key belongs to different measurement input. Use a new key."); }
    private static void ResearchOnly(bool apply) { if (apply) throw new InvalidOperationException("Learning review is research only. Production matching, pricing and creative references cannot be changed here."); }
    private static bool RetryConflict(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException or PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation }) return true;
        return false;
    }
    public async Task<object> History(Guid workspace, CancellationToken ct)
    {
        var observations = await db.CampaignObservations.AsNoTracking().Where(x => x.WorkspaceId == workspace).OrderBy(x => x.CreatedAt).ToListAsync(ct);
        var proposals = await db.CampaignLearningProposals.AsNoTracking().Where(x => x.WorkspaceId == workspace).OrderBy(x => x.CreatedAt).ToListAsync(ct);
        var reviews = await db.CampaignLearningReviews.AsNoTracking().Where(x => db.CampaignLearningProposals.Any(p => p.Id == x.ProposalId && p.WorkspaceId == workspace))
            .OrderBy(x => x.Version).ToListAsync(ct);
        return new { observations, proposals, reviews, productionChanged = false, delivery = "NOT_SENT" };
    }
    public async Task<(CampaignObservation Row, bool Replay)> Record(WeddingPlannerWorkspaceResult workspace, RecordCampaignObservation request,
        string subject, string label, CancellationToken ct)
    {
        var key = Key(request.IdempotencyKey); var kind = Text(request.Kind, 128, "Observation kind").ToUpperInvariant();
        if (kind is not ("LOCAL_VALIDATION" or "ADVERTISER_FEEDBACK" or "EXTERNAL_REPORT")) throw new InvalidOperationException("Choose a supported observation kind.");
        var source = Text(request.SourceLabel, 128, "Source label"); var evidence = Text(request.Evidence, 4000, "Evidence");
        if (request.ObservedAt.Kind != DateTimeKind.Utc || request.ObservedAt > DateTime.UtcNow.AddMinutes(5))
            throw new InvalidOperationException("Observation time must be UTC and cannot be in the future.");
        if (kind == "EXTERNAL_REPORT")
        {
            if (!request.AcknowledgeUnverifiedReport || request.WindowStart?.Kind != DateTimeKind.Utc || request.WindowEnd?.Kind != DateTimeKind.Utc
                || request.WindowStart >= request.WindowEnd || request.WindowEnd > request.ObservedAt
                || new[] { request.Impressions, request.Clicks, request.Conversions }.All(x => x is null)
                || new[] { request.Impressions, request.Clicks, request.Conversions }.Any(x => x < 0)
                || request.Clicks.HasValue && request.Impressions.HasValue && request.Clicks > request.Impressions)
                throw new InvalidOperationException("External metrics require an acknowledged unverified report, a valid UTC window, and nonnegative counts; clicks cannot exceed impressions.");
        }
        else if (request.WindowStart.HasValue || request.WindowEnd.HasValue || request.Impressions.HasValue || request.Clicks.HasValue || request.Conversions.HasValue || request.AcknowledgeUnverifiedReport)
            throw new InvalidOperationException("Local validation and advertiser feedback cannot establish campaign traffic or accept metrics.");
        var fingerprint = Hash(new { request.HandoffId, kind, source, evidence, request.ObservedAt, request.WindowStart, request.WindowEnd,
            request.Impressions, request.Clicks, request.Conversions, request.AcknowledgeUnverifiedReport, subject });
        async Task<CampaignObservation?> Replay() => await db.CampaignObservations.AsNoTracking().SingleOrDefaultAsync(x => x.WorkspaceId == workspace.WorkspaceId && x.IdempotencyKey == key, ct);
        var prior = await Replay(); if (prior is not null) { Same(prior.RequestSha256, fingerprint); return (prior, true); }
        var handoff = await db.CreativeCampaignHandoffs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.HandoffId && x.WorkspaceId == workspace.WorkspaceId, ct)
            ?? throw new WeddingPlannerNotFoundException("This workspace's creative handoff was not found.");
        var row = new CampaignObservation { Id = Guid.NewGuid(), WorkspaceId = workspace.WorkspaceId, HandoffId = handoff.Id, Kind = kind,
            EvidenceStatus = kind == "EXTERNAL_REPORT" ? "UNVERIFIED_REPORT" : kind == "LOCAL_VALIDATION" ? "LOCAL_TEST_OBSERVATION" : "REPORTED_FEEDBACK",
            SourceLabel = source, Evidence = evidence, ObservedAt = request.ObservedAt, WindowStart = request.WindowStart, WindowEnd = request.WindowEnd,
            Impressions = request.Impressions, Clicks = request.Clicks, Conversions = request.Conversions,
            LineageSnapshotJson = handoff.SnapshotJson, LineageSha256 = ControlledAiService.Hash(handoff.SnapshotJson),
            ActorSubject = Text(subject, 256, "Subject"), ActorLabel = label, IdempotencyKey = key, RequestSha256 = fingerprint, CreatedAt = DateTime.UtcNow };
        return await Commit(row, fingerprint, Replay, x => x.RequestSha256, workspace, "CAMPAIGN_OBSERVATION_RECORDED", label, subject, ct);
    }
    public async Task<(CampaignLearningProposal Row, bool Replay)> Propose(WeddingPlannerWorkspaceResult workspace, ProposeCampaignLearning request,
        string subject, string label, CancellationToken ct)
    {
        ResearchOnly(request.ApplyToProduction); var key = Key(request.IdempotencyKey);
        var hypothesis = Text(request.Hypothesis, 2000, "Hypothesis"); var experiment = Text(request.Experiment, 2000, "Experiment"); var criterion = Text(request.SuccessCriterion, 2000, "Success criterion");
        var fingerprint = Hash(new { request.ObservationId, hypothesis, experiment, criterion, subject });
        async Task<CampaignLearningProposal?> Replay() => await db.CampaignLearningProposals.AsNoTracking().SingleOrDefaultAsync(x => x.WorkspaceId == workspace.WorkspaceId && x.IdempotencyKey == key, ct);
        var prior = await Replay(); if (prior is not null) { Same(prior.RequestSha256, fingerprint); return (prior, true); }
        if (!await db.CampaignObservations.AnyAsync(x => x.Id == request.ObservationId && x.WorkspaceId == workspace.WorkspaceId, ct))
            throw new WeddingPlannerNotFoundException("This workspace's source observation was not found.");
        var row = new CampaignLearningProposal { Id = Guid.NewGuid(), WorkspaceId = workspace.WorkspaceId, ObservationId = request.ObservationId,
            Hypothesis = hypothesis, Experiment = experiment, SuccessCriterion = criterion, ActorSubject = Text(subject, 256, "Subject"), ActorLabel = label,
            IdempotencyKey = key, RequestSha256 = fingerprint, CreatedAt = DateTime.UtcNow };
        return await Commit(row, fingerprint, Replay, x => x.RequestSha256, workspace, "CAMPAIGN_LEARNING_PROPOSED", label, subject, ct);
    }
    public async Task<(CampaignLearningReview Row, bool Replay)> Review(WeddingPlannerWorkspaceResult workspace, Guid proposalId, ReviewCampaignLearning request,
        string subject, string label, bool separatePrincipals, CancellationToken ct)
    {
        ResearchOnly(request.ApplyToProduction); var key = Key(request.IdempotencyKey); var rationale = Text(request.Rationale, 2000, "Rationale");
        var decision = Text(request.Decision, 128, "Decision").ToUpperInvariant();
        if (request.ExpectedVersion < 0 || decision is not ("ACCEPT_FOR_RESEARCH" or "REQUEST_REVISION" or "REJECT")) throw new InvalidOperationException("Choose a supported research review decision and nonnegative expected version.");
        var proposal = await db.CampaignLearningProposals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == proposalId && x.WorkspaceId == workspace.WorkspaceId, ct)
            ?? throw new WeddingPlannerNotFoundException("This workspace's learning proposal was not found.");
        if (separatePrincipals && subject == proposal.ActorSubject) throw new WeddingPlannerForbiddenException("Research proposals require an independent authenticated reviewer.");
        var fingerprint = Hash(new { decision, rationale, request.ExpectedVersion, subject });
        async Task<CampaignLearningReview?> Replay() => await db.CampaignLearningReviews.AsNoTracking().SingleOrDefaultAsync(x => x.ProposalId == proposalId && x.IdempotencyKey == key, ct);
        var prior = await Replay(); if (prior is not null) { Same(prior.RequestSha256, fingerprint); return (prior, true); }
        var latest = await db.CampaignLearningReviews.Where(x => x.ProposalId == proposalId).Select(x => (int?)x.Version).MaxAsync(ct) ?? 0;
        if (latest != request.ExpectedVersion)
        {
            var winner = await Replay(); if (winner is not null) { Same(winner.RequestSha256, fingerprint); return (winner, true); }
            throw new WeddingPlannerConflictException("Research review history changed. Refresh before deciding.");
        }
        var row = new CampaignLearningReview { Id = Guid.NewGuid(), ProposalId = proposalId, Version = latest + 1, Decision = decision, Rationale = rationale,
            ActorSubject = Text(subject, 256, "Subject"), ActorLabel = label, IdempotencyKey = key, RequestSha256 = fingerprint, CreatedAt = DateTime.UtcNow };
        return await Commit(row, fingerprint, Replay, x => x.RequestSha256, workspace, "CAMPAIGN_LEARNING_REVIEWED", label, subject, ct);
    }
    private async Task<(T Row, bool Replay)> Commit<T>(T row, string fingerprint, Func<Task<T?>> replay, Func<T, string> signature,
        WeddingPlannerWorkspaceResult workspace, string action, string label, string subject, CancellationToken ct) where T : class
    {
        var audit = new WeddingPlannerAuditEvent { Id = Guid.NewGuid(), AdvertiserId = workspace.AdvertiserId, WorkspaceId = workspace.WorkspaceId,
            Action = action, ActorType = "HUMAN", ActorLabel = label, Outcome = "RECORDED", Detail = $"{action}: {db.Entry(row).Property("Id").CurrentValue}; subject: {subject}", OccurredAt = DateTime.UtcNow };
        db.Add(row); db.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        catch (Exception ex) when (RetryConflict(ex))
        {
            db.Entry(row).State = EntityState.Detached; db.Entry(audit).State = EntityState.Detached;
            var winner = await replay(); if (winner is not null) { Same(signature(winner), fingerprint); return (winner, true); }
            throw new WeddingPlannerConflictException("Measurement history changed concurrently. Refresh before retrying.");
        }
        return (row, false);
    }
}
