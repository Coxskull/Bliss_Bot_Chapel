namespace Bliss.Domain.WeddingPlanner;

public static class WeddingPlannerStatuses
{
    public const string Active = "ACTIVE";
    public const string Open = "OPEN";
    public const string RecommendationReady = "RECOMMENDATION_READY";
}

public static class WeddingPlannerActorTypes
{
    public const string Advertiser = "ADVERTISER";
    public const string Operator = "OPERATOR";
    public const string System = "SYSTEM";
}

public static class WeddingPlannerAuditActions
{
    public const string WorkspaceOpened = "WORKSPACE_OPENED";
    public const string SessionCreated = "SESSION_CREATED";
    public const string MessageAppended = "MESSAGE_APPENDED";
    public const string EconomicsRecommendationRequested = "ECONOMICS_RECOMMENDATION_REQUESTED";
    public const string AccessDenied = "ACCESS_DENIED";
}

public static class WeddingPlannerOutcomes
{
    public const string Created = "CREATED";
    public const string Replayed = "REPLAYED";
    public const string Appended = "APPENDED";
    public const string RecommendationReady = "RECOMMENDATION_READY";
    public const string Denied = "DENIED";
}
