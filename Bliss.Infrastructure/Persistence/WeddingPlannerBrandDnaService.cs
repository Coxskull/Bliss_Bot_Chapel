using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Infrastructure.Persistence;

public sealed record BrandDnaFields(string? BrandName, string? Niche, string? Market,
    string? Language, string? Colors, string? Font, string? Goals, string? Restrictions);

public sealed class WeddingPlannerBrandDnaService(BlissDbContext db)
{
    public Task<List<WeddingPlannerBrandDna>> ReadAsync(Guid workspaceId, CancellationToken ct) =>
        db.WeddingPlannerBrandDna.AsNoTracking().Where(x => x.WorkspaceId == workspaceId)
            .OrderBy(x => x.Version).ToListAsync(ct);

    // The caller must authorize the workspace before invoking these operations.
    public async Task<(WeddingPlannerBrandDna Revision, bool IsReplay)> AppendAsync(
        WeddingPlannerWorkspaceResult workspace, BrandDnaFields? fields, int expectedVersion,
        string? idempotencyKey, bool confirm, string actorType, string actorLabel, string? requestId,
        CancellationToken ct)
    {
        var key = Text(idempotencyKey, 128, "Idempotency key");
        if (key.Length == 0 || expectedVersion < 0)
            throw new InvalidOperationException("An idempotency key and nonnegative expected version are required.");
        var normalized = confirm ? null : Normalize(fields ?? throw new InvalidOperationException("Brand fields are required."));
        var replay = await db.WeddingPlannerBrandDna.AsNoTracking()
            .SingleOrDefaultAsync(x => x.WorkspaceId == workspace.WorkspaceId && x.IdempotencyKey == key, ct);
        if (replay is not null)
        {
            if (replay.ExpectedVersion != expectedVersion || replay.Status != (confirm ? "CONFIRMED" : "DRAFT")
                || (!confirm && Fields(replay) != normalized))
                throw new WeddingPlannerConflictException("This key belongs to different brand intake. Use a new key.");
            return (replay, true);
        }
        var latest = await db.WeddingPlannerBrandDna.AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.WorkspaceId).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if ((latest?.Version ?? 0) != expectedVersion)
        {
            // An identical writer can commit between the key lookup and latest-version lookup.
            var winner = await db.WeddingPlannerBrandDna.AsNoTracking().SingleOrDefaultAsync(
                x => x.WorkspaceId == workspace.WorkspaceId && x.IdempotencyKey == key, ct);
            if (winner is not null && winner.ExpectedVersion == expectedVersion && winner.Status == (confirm ? "CONFIRMED" : "DRAFT")
                && (confirm || Fields(winner) == normalized)) return (winner, true);
            throw new WeddingPlannerConflictException("Brand intake changed. Reload and review the latest version before saving.");
        }
        if (confirm)
        {
            if (latest is null || latest.Status != "DRAFT")
                throw new InvalidOperationException("Save a draft before confirming brand direction.");
            normalized = Fields(latest);
            if (new[] { normalized.BrandName, normalized.Niche, normalized.Market, normalized.Language, normalized.Goals }
                .Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("Brand name, niche, market, language, and campaign goal are required to confirm.");
        }
        var data = normalized!;
        var row = new WeddingPlannerBrandDna
        {
            Id = Guid.NewGuid(), WorkspaceId = workspace.WorkspaceId, Version = expectedVersion + 1,
            ExpectedVersion = expectedVersion, Status = confirm ? "CONFIRMED" : "DRAFT",
            BrandName = data.BrandName!, Niche = data.Niche!, Market = data.Market!, Language = data.Language!,
            Colors = data.Colors!, Font = data.Font!, Goals = data.Goals!, Restrictions = data.Restrictions!,
            IdempotencyKey = key, ActorLabel = actorLabel, CreatedAt = DateTime.UtcNow
        };
        var audit = new WeddingPlannerAuditEvent
        {
            Id = Guid.NewGuid(), AdvertiserId = workspace.AdvertiserId, WorkspaceId = workspace.WorkspaceId,
            Action = confirm ? "BRAND_DNA_CONFIRMED" : "BRAND_DNA_DRAFT_SAVED",
            ActorType = actorType, ActorLabel = actorLabel, Outcome = "APPENDED", RequestId = requestId,
            Detail = $"Brand DNA revision {row.Version} ({row.Id}).", OccurredAt = row.CreatedAt
        };
        db.WeddingPlannerBrandDna.Add(row);
        db.WeddingPlannerAuditEvents.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // SaveChanges rolls the revision and audit back together. Reconcile identical races.
            db.Entry(row).State = EntityState.Detached;
            db.Entry(audit).State = EntityState.Detached;
            var winner = await db.WeddingPlannerBrandDna.AsNoTracking().SingleOrDefaultAsync(
                x => x.WorkspaceId == workspace.WorkspaceId && x.IdempotencyKey == key, ct);
            if (winner is not null && winner.ExpectedVersion == expectedVersion && winner.Status == row.Status
                && Fields(winner) == data)
                return (winner, true);
            throw new WeddingPlannerConflictException("Brand intake changed. Reload and review the latest version before saving.");
        }
        return (row, false);
    }

    private static BrandDnaFields Fields(WeddingPlannerBrandDna row) => new(
        row.BrandName, row.Niche, row.Market, row.Language, row.Colors, row.Font, row.Goals, row.Restrictions);

    private static BrandDnaFields Normalize(BrandDnaFields fields) => new(
        Text(fields.BrandName, 200, "Brand name"), Text(fields.Niche, 200, "Niche"),
        Text(fields.Market, 200, "Market"), Text(fields.Language, 100, "Language"),
        Text(fields.Colors, 500, "Colors"), Text(fields.Font, 200, "Font"),
        Text(fields.Goals, 2000, "Goals"), Text(fields.Restrictions, 2000, "Restrictions"));

    private static string Text(string? value, int maxLength, string name)
    {
        var result = (value ?? "").Trim();
        if (result.Length > maxLength) throw new InvalidOperationException($"{name} must be at most {maxLength} characters.");
        return result;
    }
}
