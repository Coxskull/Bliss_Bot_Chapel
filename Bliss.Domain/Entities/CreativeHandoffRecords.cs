namespace Bliss.Domain.Entities;

public sealed class CandidateApprovalDecision
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public Guid? ReviewId { get; set; }
    public Guid? AdInventorySlotId { get; set; }
    public string Stage { get; set; } = "";
    public string ScopeKey { get; set; } = "";
    public int Version { get; set; }
    public string Decision { get; set; } = "";
    public string Status { get; set; } = "";
    public string Rationale { get; set; } = "";
    public bool DimensionsVerified { get; set; }
    public bool FormatVerified { get; set; }
    public bool CreatorSafeAreaVerified { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public string ActorSubject { get; set; } = "";
    public string ActorLabel { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestSha256 { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class CreativeCampaignHandoff
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid CandidateId { get; set; }
    public Guid ReviewId { get; set; }
    public Guid AdvertiserApprovalId { get; set; }
    public Guid AlphaQaApprovalId { get; set; }
    public Guid InventoryFitId { get; set; }
    public Guid QuoteLineItemId { get; set; }
    public Guid CampaignPlacementId { get; set; }
    public Guid CampaignPlacementRunId { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string ActorLabel { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestSha256 { get; set; } = "";
    public string Status { get; set; } = "PLANNED";
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime CreatedAt { get; set; }
}
