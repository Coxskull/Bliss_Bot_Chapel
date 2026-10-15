namespace Bliss.Domain.Entities;

public sealed class CampaignObservation
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid HandoffId { get; set; }
    public string Kind { get; set; } = "";
    public string EvidenceStatus { get; set; } = "";
    public string SourceLabel { get; set; } = "";
    public string Evidence { get; set; } = "";
    public DateTime ObservedAt { get; set; }
    public DateTime? WindowStart { get; set; }
    public DateTime? WindowEnd { get; set; }
    public long? Impressions { get; set; }
    public long? Clicks { get; set; }
    public long? Conversions { get; set; }
    public string LineageSnapshotJson { get; set; } = "";
    public string LineageSha256 { get; set; } = "";
    public string ActorSubject { get; set; } = "";
    public string ActorLabel { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestSha256 { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class CampaignLearningProposal
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid ObservationId { get; set; }
    public string Hypothesis { get; set; } = "";
    public string Experiment { get; set; } = "";
    public string SuccessCriterion { get; set; } = "";
    public string Status { get; set; } = "PROPOSED";
    public string ActorSubject { get; set; } = "";
    public string ActorLabel { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestSha256 { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class CampaignLearningReview
{
    public Guid Id { get; set; }
    public Guid ProposalId { get; set; }
    public int Version { get; set; }
    public string Decision { get; set; } = "";
    public string Rationale { get; set; } = "";
    public string ActorSubject { get; set; } = "";
    public string ActorLabel { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestSha256 { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
