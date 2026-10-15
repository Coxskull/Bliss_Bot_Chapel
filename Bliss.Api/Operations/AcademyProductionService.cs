using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Api.Operations;

public sealed record CreateProductionBrief(Guid BrandDnaId, string Family, string Headline, string Cta, string IdempotencyKey);
public sealed record SubmitProductionCandidate(int ExpectedVersion, string Label, string Notes, string ImageBase64, string IdempotencyKey);
public sealed record RecordProductionReview(int ExpectedVersion, string Decision, string Customization,
    string QualityParity, string Originality, string GeographicAuthenticity, bool CriticalDefect,
    string Rationale, string Preserve, string Repair, string IdempotencyKey);

public sealed class AcademyProductionService(BlissDbContext db, BlueprintPhaseService references)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Serialize(object value) => JsonSerializer.Serialize(value, Json);
    private static string Text(string? value, int max, bool required = true)
    {
        var text = (value ?? "").Trim();
        if (text.Length > max || required && text.Length == 0) throw new InvalidOperationException($"Text must be {(required ? "1" : "0")} to {max} characters.");
        return text;
    }
    public IReadOnlyList<AcademyReferenceRecord> References() => references.ReadReferences()
        .Where(x => x.Lifecycle == ReferenceLibrary.Active && x.AssetPresent && !string.IsNullOrWhiteSpace(x.Learn) && !string.IsNullOrWhiteSpace(x.DoNotCopy)).ToList();
    public Task<List<AcademyProductionBrief>> ListAsync(Guid workspace, CancellationToken ct) =>
        db.AcademyProductionBriefs.AsNoTracking().Where(x => x.WorkspaceId == workspace).OrderBy(x => x.CreatedAt).ToListAsync(ct);
    public async Task<AcademyProductionBrief> BriefAsync(Guid workspace, Guid id, CancellationToken ct) =>
        await db.AcademyProductionBriefs.AsNoTracking().SingleOrDefaultAsync(x => x.WorkspaceId == workspace && x.Id == id, ct)
        ?? throw new WeddingPlannerNotFoundException("Production brief was not found.");
    public async Task<AcademyProductionCandidate> CandidateAsync(Guid brief, Guid id, CancellationToken ct) =>
        await db.AcademyProductionCandidates.AsNoTracking().SingleOrDefaultAsync(x => x.BriefId == brief && x.Id == id, ct)
        ?? throw new WeddingPlannerNotFoundException("Production candidate was not found.");
    public async Task<object> HistoryAsync(Guid workspace, Guid id, CancellationToken ct)
    {
        var brief = await BriefAsync(workspace, id, ct);
        var candidates = await db.AcademyProductionCandidates.AsNoTracking().Where(x => x.BriefId == id).OrderBy(x => x.Version)
            .Select(x => new { x.Id, x.BriefId, x.Version, x.ExpectedVersion, x.Label, x.Notes, x.ImageSha256, x.ActorLabel, x.CreatedAt }).ToListAsync(ct);
        var reviews = await db.AcademyProductionReviews.AsNoTracking().Where(x => db.AcademyProductionCandidates.Any(c => c.BriefId == id && c.Id == x.CandidateId))
            .OrderBy(x => x.CreatedAt).ToListAsync(ct);
        return new { brief, candidates, reviews, campaignReady = false, delivery = "NOT_SENT" };
    }
    public async Task<(AcademyProductionBrief Row, bool Replay)> CreateAsync(WeddingPlannerWorkspaceResult workspace,
        CreateProductionBrief request, string actorType, string actorLabel, string? requestId, CancellationToken ct)
    {
        var key = Text(request.IdempotencyKey, 128); var family = Text(request.Family, 128).ToLowerInvariant();
        var headline = Text(request.Headline, 500); var cta = Text(request.Cta, 500);
        var fingerprint = Hash(Serialize(new { request.BrandDnaId, family, headline, cta }));
        async Task<AcademyProductionBrief?> Replay() => await db.AcademyProductionBriefs.AsNoTracking().SingleOrDefaultAsync(x => x.WorkspaceId == workspace.WorkspaceId && x.IdempotencyKey == key, ct);
        var prior = await Replay();
        if (prior is not null) { Same(prior.RequestSha256, fingerprint); return (prior, true); }
        var brand = await db.WeddingPlannerBrandDna.AsNoTracking().Where(x => x.WorkspaceId == workspace.WorkspaceId)
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (brand is null || brand.Status != "CONFIRMED" || brand.Id != request.BrandDnaId)
            throw new WeddingPlannerConflictException("Select the latest confirmed Brand DNA before preparing a new brief.");
        var reference = References().FirstOrDefault(x => x.NicheKey.Equals(family, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("No eligible ACTIVE reference exists for this family. No substitute was selected.");
        var file = references.OpenReference(reference.ReferenceId) ?? throw new InvalidOperationException("Reference image is unavailable.");
        var referenceHash = Hash(await File.ReadAllBytesAsync(file.FullPath, ct));
        var brief = CreativeAcademy.PrepareProductionBrief(family, brand.BrandName, brand.Colors, brand.Font, headline, cta,
            brand.Market, brand.Language, $"Goal: {brand.Goals}\nRestrictions: {brand.Restrictions}", false, false, false, [reference], []);
        var row = new AcademyProductionBrief
        {
            Id = Guid.NewGuid(), WorkspaceId = workspace.WorkspaceId, BrandDnaId = brand.Id, Family = family, Headline = headline, Cta = cta,
            ReferenceId = reference.ReferenceId, ReferenceSha256 = referenceHash, ReferenceSnapshotJson = Serialize(reference),
            BrandSnapshotJson = Serialize(brand), ProductionBriefJson = Serialize(brief), RecipeSha256 = Hash(brief.GenerationRecipe),
            RequestSha256 = fingerprint, IdempotencyKey = key, ActorLabel = actorLabel, CreatedAt = DateTime.UtcNow
        };
        return await Commit(row, fingerprint, Replay, x => x.RequestSha256, workspace, "PRODUCTION_BRIEF_SAVED", actorType, actorLabel, requestId, ct);
    }
    public async Task<(AcademyProductionCandidate Row, bool Replay)> SubmitAsync(WeddingPlannerWorkspaceResult workspace,
        Guid briefId, SubmitProductionCandidate request, string actorType, string actorLabel, string? requestId, CancellationToken ct)
    {
        await BriefAsync(workspace.WorkspaceId, briefId, ct);
        var key = Text(request.IdempotencyKey, 128); var label = Text(request.Label, 200); var notes = Text(request.Notes, 2000, false);
        if (request.ExpectedVersion < 0 || request.ImageBase64 is null || request.ImageBase64.Length > 1_398_104)
            throw new InvalidOperationException("A nonnegative version and PNG image of at most 1 MiB are required.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(request.ImageBase64); }
        catch (FormatException) { throw new InvalidOperationException("Image data must be base64-encoded PNG."); }
        CandidatePng.Validate(bytes);
        var imageHash = Hash(bytes);
        var fingerprint = Hash(Serialize(new { request.ExpectedVersion, label, notes, imageHash }));
        async Task<AcademyProductionCandidate?> Replay() => await db.AcademyProductionCandidates.AsNoTracking().SingleOrDefaultAsync(x => x.BriefId == briefId && x.IdempotencyKey == key, ct);
        var prior = await Replay();
        if (prior is not null) { Same(prior.RequestSha256, fingerprint); return (prior, true); }
        var version = await db.AcademyProductionCandidates.Where(x => x.BriefId == briefId).Select(x => (int?)x.Version).MaxAsync(ct) ?? 0;
        if (request.ExpectedVersion != version)
        {
            var winner = await Replay(); if (winner is not null) { Same(winner.RequestSha256, fingerprint); return (winner, true); }
            throw new WeddingPlannerConflictException("Candidate history changed. Reload before submitting a revision.");
        }
        var row = new AcademyProductionCandidate
        {
            Id = Guid.NewGuid(), BriefId = briefId, Version = version + 1, ExpectedVersion = version, Label = label, Notes = notes,
            ImageBytes = bytes, ImageSha256 = imageHash, RequestSha256 = fingerprint, IdempotencyKey = key, ActorLabel = actorLabel, CreatedAt = DateTime.UtcNow
        };
        return await Commit(row, fingerprint, Replay, x => x.RequestSha256, workspace, "PRODUCTION_CANDIDATE_SAVED", actorType, actorLabel, requestId, ct);
    }
    public async Task<(AcademyProductionReview Row, bool Replay)> ReviewAsync(WeddingPlannerWorkspaceResult workspace,
        Guid briefId, Guid candidateId, RecordProductionReview request, string actorType, string actorLabel, string? requestId, CancellationToken ct)
    {
        await BriefAsync(workspace.WorkspaceId, briefId, ct);
        var candidate = await CandidateAsync(briefId, candidateId, ct);
        var key = Text(request.IdempotencyKey, 128);
        var decision = Text(request.Decision, 32).ToUpperInvariant();
        var gates = new[] { request.Customization, request.QualityParity, request.Originality, request.GeographicAuthenticity }
            .Select(x => Text(x, 32).ToUpperInvariant()).ToArray();
        var rationale = Text(request.Rationale, 2000); var preserve = Text(request.Preserve, 2000, false); var repair = Text(request.Repair, 2000, false);
        if (request.ExpectedVersion < 0 || !new[] { "PASS", "REVISE", "FAIL" }.Contains(decision) || gates.Any(x => x is not ("PASS" or "FAIL")))
            throw new InvalidOperationException("A valid review decision, nonnegative version, and four independent PASS/FAIL gates are required.");
        if (decision == "PASS" && (request.CriticalDefect || gates.Contains("FAIL")))
            throw new InvalidOperationException("PASS requires all four gates to pass with no critical defect.");
        if (decision == "REVISE" && repair.Length == 0) throw new InvalidOperationException("REVISE requires a repair instruction.");
        var fingerprint = Hash(Serialize(new { request.ExpectedVersion, decision, gates, request.CriticalDefect, rationale, preserve, repair }));
        async Task<AcademyProductionReview?> Replay() => await db.AcademyProductionReviews.AsNoTracking().SingleOrDefaultAsync(x => x.CandidateId == candidateId && x.IdempotencyKey == key, ct);
        var prior = await Replay();
        if (prior is not null) { Same(prior.RequestSha256, fingerprint); return (prior, true); }
        var latestCandidate = await db.AcademyProductionCandidates.Where(x => x.BriefId == briefId).MaxAsync(x => x.Version, ct);
        if (latestCandidate != candidate.Version) throw new WeddingPlannerConflictException("Review the latest candidate revision.");
        var version = await db.AcademyProductionReviews.Where(x => x.CandidateId == candidateId).Select(x => (int?)x.Version).MaxAsync(ct) ?? 0;
        if (request.ExpectedVersion != version)
        {
            var winner = await Replay(); if (winner is not null) { Same(winner.RequestSha256, fingerprint); return (winner, true); }
            throw new WeddingPlannerConflictException("Review history changed. Reload before recording a judgment.");
        }
        var row = new AcademyProductionReview
        {
            Id = Guid.NewGuid(), CandidateId = candidateId, Version = version + 1, ExpectedVersion = version, Decision = decision,
            Customization = gates[0], QualityParity = gates[1], Originality = gates[2], GeographicAuthenticity = gates[3], CriticalDefect = request.CriticalDefect,
            Rationale = rationale, Preserve = preserve, Repair = repair, RequestSha256 = fingerprint, IdempotencyKey = key, ActorLabel = actorLabel, CreatedAt = DateTime.UtcNow
        };
        return await Commit(row, fingerprint, Replay, x => x.RequestSha256, workspace, "PRODUCTION_REVIEW_RECORDED", actorType, actorLabel, requestId, ct);
    }
    private static void Same(string old, string current)
    {
        if (old != current) throw new WeddingPlannerConflictException("This key belongs to different production input. Use a new key.");
    }
    private async Task<(T Row, bool Replay)> Commit<T>(T row, string fingerprint, Func<Task<T?>> replay, Func<T, string> signature,
        WeddingPlannerWorkspaceResult workspace, string action, string actorType, string actorLabel, string? requestId, CancellationToken ct) where T : class
    {
        var audit = new WeddingPlannerAuditEvent
        {
            Id = Guid.NewGuid(), AdvertiserId = workspace.AdvertiserId, WorkspaceId = workspace.WorkspaceId, Action = action,
            ActorType = actorType, ActorLabel = actorLabel, Outcome = "APPENDED", RequestId = requestId,
            Detail = $"{action}: {db.Entry(row).Property("Id").CurrentValue}", OccurredAt = DateTime.UtcNow
        };
        db.Add(row); db.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(row).State = EntityState.Detached; db.Entry(audit).State = EntityState.Detached;
            var winner = await replay();
            if (winner is not null) { Same(signature(winner), fingerprint); return (winner, true); }
            throw new WeddingPlannerConflictException("Production history changed. Reload and retry.");
        }
        return (row, false);
    }
}
