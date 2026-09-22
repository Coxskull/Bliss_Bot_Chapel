namespace Bliss.Domain.WeddingPlanner;

public static class WeddingPlannerStatuses
{
    public const string Active = "ACTIVE";
    public const string Open = "OPEN";
}

public static class WeddingPlannerActorTypes
{
    public const string Advertiser = "ADVERTISER";
    public const string Operator = "OPERATOR";
    public const string System = "SYSTEM";
    public const string Planner = "PLANNER";
}

public static class WeddingPlannerAuditActions
{
    public const string WorkspaceOpened = "WORKSPACE_OPENED";
    public const string SessionCreated = "SESSION_CREATED";
    public const string MessageAppended = "MESSAGE_APPENDED";
    public const string AccessDenied = "ACCESS_DENIED";
    public const string AgentRunStarted = "AGENT_RUN_STARTED";
    public const string AgentRunSucceeded = "AGENT_RUN_SUCCEEDED";
    public const string AgentRunFailed = "AGENT_RUN_FAILED";
    public const string AgentRunReplayed = "AGENT_RUN_REPLAYED";
    public const string BrandDnaProposed = "BRAND_DNA_PROPOSED";
    public const string BrandDnaApproved = "BRAND_DNA_APPROVED";
    public const string BrandDnaRejected = "BRAND_DNA_REJECTED";
    public const string BrandDnaReplayed = "BRAND_DNA_REPLAYED";
    public const string ColorProfileProposed = "COLOR_PROFILE_PROPOSED";
    public const string ColorProfileApproved = "COLOR_PROFILE_APPROVED";
    public const string ColorProfileRejected = "COLOR_PROFILE_REJECTED";
    public const string ColorProfileSuperseded = "COLOR_PROFILE_SUPERSEDED";
    public const string ColorProfileReplayed = "COLOR_PROFILE_REPLAYED";
}

public static class WeddingPlannerOutcomes
{
    public const string Created = "CREATED";
    public const string Replayed = "REPLAYED";
    public const string Appended = "APPENDED";
    public const string Denied = "DENIED";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
    public const string Proposed = "PROPOSED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Superseded = "SUPERSEDED";
}

public static class WeddingPlannerAgentRoles
{
    public const string Concierge = "CONCIERGE";
    public const string BrandDnaInterpreter = "BRAND_DNA_INTERPRETER";
}

public static class WeddingPlannerAgentRunStatuses
{
    public const string Running = "RUNNING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
}

public static class WeddingPlannerBrandDnaStatuses
{
    public const string Proposed = "PROPOSED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Superseded = "SUPERSEDED";
}

public static class WeddingPlannerBrandDnaDecisions
{
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
}

public static class WeddingPlannerPromptPacks
{
    public const string ConciergeV1 = "wp-phase2.concierge.v1";
    public const string BrandDnaV1 = "wp-phase2.brand-dna.v1";
}

public static class WeddingPlannerSchemaVersions
{
    public const string BrandDnaV1 = "brand-dna.v1";
    public const string ColorProfileV1 = "color-profile.v1";
}

public static class WeddingPlannerAlgorithmVersions
{
    public const string AciHslV1 = "aci.hsl.v1";
}

public static class WeddingPlannerColorProfileStatuses
{
    public const string Proposed = "PROPOSED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Superseded = "SUPERSEDED";
}

public static class WeddingPlannerColorProfileDecisions
{
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
}

public static class WeddingPlannerResponseFormats
{
    public const string Text = "text";
    public const string Json = "json";
}

public static class WeddingPlannerWorkers
{
    public const string LocalDeterministicV1 = "wp-local-deterministic.v1";
    public const string OpenAiCompatibleV1 = "wp-openai-compatible.v1";
}

public static class WeddingPlannerAiProviderKinds
{
    public const string Local = "Local";
    public const string OpenAiCompatible = "OpenAiCompatible";
}
