using System.Security.Claims;
using System.Text.RegularExpressions;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Security;

// Stage F: read-only application-DB plan for a future independently authorized
// visitor-owned NEW session. No DI registration, route, EF migration or writes.
// Existing Wedding Planner sessions belong to advertisers, NOT visitors.
public sealed record AskAlphaAuthoritativeEnrollmentReading(
    string Status,
    Guid WorkspaceId,
    bool AuthenticatedClaimPrerequisitesMatch = false,
    bool WorkspaceExistsAndActive = false,
    bool VisitorOwnershipSchemaAvailable = false,
    bool NewVisitorSessionCreated = false,
    bool RealVisitorIdentityVerified = false,
    bool HistoricalSessionOwnershipVerified = false,
    bool VisitorConsentRecorded = false,
    bool TranscriptLinked = false,
    bool TranscriptExportAuthorized = false,
    bool PaymentAuthorized = false,
    bool CreatorPayoutAuthorized = false,
    bool CampaignDeliveryAuthorized = false,
    string Delivery = "NOT_SENT");

public sealed class AskAlphaAuthoritativeEnrollmentPlanner(
    BlissDbContext db, BlissAuthenticationOptions authentication)
{
    // RequestKey is a future server-side idempotency input. It MUST NOT be
    // interpreted as proof of identity or a historic conversation/session ID.
    public async Task<AskAlphaAuthoritativeEnrollmentReading> InspectAsync(
        ClaimsPrincipal? principal, Guid workspaceId,
        string sourceSlug, string requestKey, CancellationToken ct = default)
    {
        var unavailable = new AskAlphaAuthoritativeEnrollmentReading(
            "REAL_VISITOR_ENROLLMENT_NOT_AUTHORIZED", workspaceId);
        var identity = AskAlphaVisitorEnrollmentGate.Inspect(principal, authentication);
        if (!identity.IssuerSubjectClaimsConsistent ||
            identity.Status != "NEW_VISITOR_OIDC_CLAIMS_DECLARED_SERVER_CREATION_PENDING")
            return unavailable;

        var authenticatedClaims = unavailable with { AuthenticatedClaimPrerequisitesMatch = true };
        if (workspaceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(sourceSlug) || sourceSlug.Length > 96 ||
            !Regex.IsMatch(sourceSlug, @"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant) ||
            string.IsNullOrWhiteSpace(requestKey) || requestKey.Length is < 8 or > 100 ||
            !Regex.IsMatch(requestKey, @"\A[a-zA-Z0-9_-]+\z", RegexOptions.CultureInvariant))
            return authenticatedClaims with { Status = "INVALID_NEW_SESSION_SCOPE" };

        // The workspace is in the actual authoritative EF application model.
        // Read and filter only on identifiers/status; do not retrieve messages.
        var workspace = await db.WeddingPlannerWorkspaces.AsNoTracking()
            .Where(x => x.Id == workspaceId)
            .Select(x => new { x.Id, x.AdvertiserId, x.Status })
            .SingleOrDefaultAsync(ct);
        if (workspace is null || workspace.AdvertiserId == Guid.Empty ||
            workspace.Status != "ACTIVE")
            return authenticatedClaims with { Status = "SCOPED_WORKSPACE_UNAVAILABLE" };

        var scoped = authenticatedClaims with { WorkspaceExistsAndActive = true };
        // No application visitor ownership table/unique constraint exists yet.
        // NEVER infer visitor ownership from a matching retry key or an
        // advertiser/staff-created session. Cross-workspace keys have the same
        // denial shape, without revealing existence or tenant identifiers.
        var reserved = await db.WeddingPlannerPlanningSessions.AsNoTracking()
            .AnyAsync(x => x.IdempotencyKey == requestKey, ct);
        if (reserved)
            return scoped with { Status = "UNOWNED_SESSION_REPLAY_DENIED" };

        return scoped with {
            Status = "REAL_VISITOR_OWNERSHIP_SCHEMA_AND_SIGNED_IDP_MISSING"
        };
    }
}
