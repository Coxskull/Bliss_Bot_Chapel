namespace Bliss.Domain.Common;

/// <summary>
/// String status values used by Phase 1 persistence. These are not an intelligence engine.
/// </summary>
public static class EntityStatuses
{
    public const string Active = "ACTIVE";
    public const string Created = "CREATED";
    public const string Unknown = "UNKNOWN";
    public const string Approved = "APPROVED";
    public const string Draft = "DRAFT";
    public const string Completed = "COMPLETED";
}

public static class ConfidenceLevels
{
    public const string Unknown = "UNKNOWN";
}

public static class InventorySlotTypes
{
    public const string PreRoll = "PRE_ROLL";
    public const string MidRoll = "MID_ROLL";
    public const string LowerThird = "LOWER_THIRD";
    public const string PostRoll = "POST_ROLL";
}
