using System.Security.Claims;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Security;

// AGM-LAUNCH-015 Stage C - read-only synthetic new-session source verification.
// Requires a row actually fetched from a scoped database, not a request JSON
// pretending to be a newly created session. No production route/DI registration.
// This does NOT verify real historical visitor authorship or consent.
public sealed class AskAlphaNewSessionFixturePreflight(
    BlissDbContext db, AskAlphaOwnershipFixtureStore receipts,
    BlissAuthenticationOptions authentication)
{
    public async Task<AskAlphaVisitorIdentityReadiness> InspectAsync(
        ClaimsPrincipal? visitor, Guid workspaceId, Guid sessionId,
        string sourceSlug, DateTimeOffset now, ReadOnlyMemory<byte> fixtureKey,
        CancellationToken ct = default)
    {
        var denied = new AskAlphaVisitorIdentityReadiness(
            "STORED_NEW_SESSION_PREREQUISITES_MISSING", sessionId);
        if (workspaceId == Guid.Empty || sessionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(sourceSlug))
            return denied;

        // The authenticated visitor gate precedes all database and source
        // lookup. Invalid claimant cannot probe whether a session exists.
        var claims = AskAlphaVisitorClaimGate.Inspect(visitor, authentication, sessionId);
        if (!claims.ClaimsMatchIssuerAndSession) return denied;

        // Both scope predicates are required; do not query session globally
        // and then infer ownership from a mutable Ask Alpha demonstration slug.
        var workspace = await db.WeddingPlannerWorkspaces.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == workspaceId, ct);
        var session = await db.WeddingPlannerPlanningSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == sessionId && x.WorkspaceId == workspaceId, ct);
        if (workspace is null || session is null ||
            workspace.AdvertiserId == Guid.Empty || workspace.Status != "ACTIVE" ||
            session.AdvertiserId != workspace.AdvertiserId ||
            session.SourceSystem != "FICTIONAL_ASK_ALPHA_WAKE" ||
            session.Status != "OPEN" ||
            session.CreatedAt.Kind != DateTimeKind.Utc ||
            session.CreatedAt > now.UtcDateTime ||
            session.CreatedAt < now.UtcDateTime.AddHours(-1))
            return denied;

        // Read only the receipt and existence/earliest message timestamp.
        // Never load message bodies, contacts, identity data, or full transcripts.
        var record = await receipts.ReadAsync(sessionId, ct);
        if (record is null || record.Scope.WorkspaceId != workspaceId ||
            record.Scope.SessionId != sessionId)
            return denied;
        var firstMessageAt = await db.WeddingPlannerConversationMessages.AsNoTracking()
            .Where(x => x.WorkspaceId == workspaceId && x.SessionId == sessionId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (record.IssuedAt < new DateTimeOffset(session.CreatedAt).AddSeconds(-5) ||
            record.IssuedAt > new DateTimeOffset(session.CreatedAt).AddSeconds(5) ||
            (firstMessageAt.HasValue && record.IssuedAt.UtcDateTime > firstMessageAt.Value))
            return denied;

        var expected = new AskAlphaOwnershipFixtureScope(
            workspaceId,sessionId,sourceSlug,authentication.AskAlphaVisitorIssuer);
        var signed = AskAlphaVisitorOwnershipPreflight.Inspect(
            visitor,authentication,expected,record,now,fixtureKey.Span);
        if (signed.Status != "SYNTHETIC_AT_CREATION_RECEIPT_MATCHES_HISTORY_UNVERIFIED")
            return signed with { Status = "NEW_SESSION_FIXTURE_PROOF_REJECTED" };

        // Exact scope and creation-time correlation is necessary, NOT sufficient.
        // The existing planning-session endpoint is advertiser/staff-owned;
        // real independent visitor registration and consent are still missing.
        return signed with { Status = "SYNTHETIC_STORED_NEW_SESSION_MATCH_HISTORY_UNVERIFIED" };
    }
}
