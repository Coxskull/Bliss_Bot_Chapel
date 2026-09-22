using System.Security.Claims;
using Bliss.Domain.WeddingPlanner;

namespace Bliss.Api.Security;

public sealed record WeddingPlannerActor(
    bool IsChapelStaff,
    Guid? BoundAdvertiserId,
    string ActorType,
    string ActorLabel,
    bool IsReviewer = false,
    bool IsOperator = false,
    bool IsAdmin = false);

public sealed class WeddingPlannerAccess(BlissAuthenticationOptions options, OperatorIdentity operatorIdentity)
{
    public WeddingPlannerActor Resolve(ClaimsPrincipal user)
    {
        if (!options.Enabled)
        {
            return new WeddingPlannerActor(
                true,
                null,
                WeddingPlannerActorTypes.Operator,
                "DEV_OPERATOR",
                IsReviewer: true,
                IsOperator: true,
                IsAdmin: true);
        }

        var isOperator = user.IsInRole(options.OperatorRole);
        var isAdmin = user.IsInRole(options.AdminRole);
        var isReviewer = user.IsInRole(options.ReviewerRole);
        var isStaff = isOperator || isAdmin;
        Guid? advertiserId = null;
        var claim = user.FindFirst(options.AdvertiserIdClaimType)?.Value;
        if (Guid.TryParse(claim, out var parsed) && parsed != Guid.Empty)
        {
            advertiserId = parsed;
        }

        var label = operatorIdentity.ResolveLabel(user, "UNKNOWN");
        var actorType = isStaff
            ? WeddingPlannerActorTypes.Operator
            : isReviewer
                ? WeddingPlannerActorTypes.Reviewer
                : WeddingPlannerActorTypes.Advertiser;
        return new WeddingPlannerActor(isStaff, advertiserId, actorType, label, isReviewer, isOperator, isAdmin);
    }

    /// <summary>Generic Wedding Planner write (Phases 1–6). Do not widen for QA decisions.</summary>
    public bool CanWrite(WeddingPlannerActor actor) =>
        actor.IsChapelStaff || actor.BoundAdvertiserId.HasValue;

    /// <summary>Phase 7 job create: advertiser bound, or reviewer/operator/admin. Auth disabled → allowed.</summary>
    public bool CanCreateQaReview(WeddingPlannerActor actor) =>
        !options.Enabled
        || actor.IsOperator
        || actor.IsAdmin
        || actor.IsReviewer
        || actor.BoundAdvertiserId.HasValue;

    /// <summary>Phase 7 read/create across workspaces for reviewer|operator|admin (not advertisers).</summary>
    public bool CanAccessQaAcrossWorkspaces(WeddingPlannerActor actor) =>
        !options.Enabled || actor.IsReviewer || actor.IsOperator || actor.IsAdmin;

    /// <summary>
    /// Phase 7 decision authority: reviewer|operator|admin union.
    /// Intentionally broader than <see cref="BlissAuthorization.CanReview"/> (which excludes operator).
    /// </summary>
    public bool CanDecideQaReview(WeddingPlannerActor actor) =>
        !options.Enabled || actor.IsReviewer || actor.IsOperator || actor.IsAdmin;

    /// <summary>Phase 7 WAIVE_AND_ACCEPT: operator/admin only.</summary>
    public bool CanWaiveQaEscalation(WeddingPlannerActor actor) =>
        !options.Enabled || actor.IsOperator || actor.IsAdmin;
}
