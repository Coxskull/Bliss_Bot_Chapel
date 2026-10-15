using System.Security.Claims;

namespace Bliss.Api.Security;

public sealed record WeddingPlannerActor(
    bool IsChapelStaff,
    Guid? BoundAdvertiserId,
    string ActorType,
    string ActorLabel,
    bool HasAdvertiserWriteRole = false);

public sealed class WeddingPlannerAccess(BlissAuthenticationOptions options, OperatorIdentity operatorIdentity)
{
    public WeddingPlannerActor Resolve(ClaimsPrincipal user)
    {
        if (!options.Enabled)
        {
            return new WeddingPlannerActor(true, null, "OPERATOR", "DEV_OPERATOR");
        }

        var isStaff = user.IsInRole(options.OperatorRole) || user.IsInRole(options.AdminRole);
        Guid? advertiserId = null;
        var claim = user.FindFirst(options.AdvertiserIdClaimType)?.Value;
        if (Guid.TryParse(claim, out var parsed) && parsed != Guid.Empty)
        {
            advertiserId = parsed;
        }

        var label = operatorIdentity.ResolveLabel(user, "UNKNOWN");
        var actorType = isStaff ? "OPERATOR" : "ADVERTISER";
        // A tenant-binding claim scopes access but cannot grant write rights.
        // Viewer/reviewer identities may read their tenant without changing it.
        return new WeddingPlannerActor(isStaff, advertiserId, actorType, label,
            user.IsInRole(options.AdvertiserRole));
    }

    public bool CanWrite(WeddingPlannerActor actor) =>
        actor.IsChapelStaff || (actor.HasAdvertiserWriteRole && actor.BoundAdvertiserId.HasValue);
}
