using System.Text.Json.Serialization;

namespace Bliss.Domain.Entities;

public sealed class ControlledAiRun
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? BriefId { get; set; }
    public Guid? AttachedCandidateId { get; set; }
    public string Kind { get; set; } = "";
    public string Status { get; set; } = "QUEUED";
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string PromptVersion { get; set; } = "bliss-workers-v1";
    public string InputSnapshotJson { get; set; } = "";
    public string InputSha256 { get; set; } = "";
    public string RequestSha256 { get; set; } = "";
    public string ExecutionConfigSha256 { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string ActorLabel { get; set; } = "";
    public decimal ReservedUsd { get; set; }
    public decimal? ActualUsd { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public string CostStatus { get; set; } = "UNRECORDED";
    public string ProviderRequestId { get; set; } = "";
    public string OutputText { get; set; } = "";
    [JsonIgnore] public byte[]? OutputImage { get; set; }
    public string OutputSha256 { get; set; } = "";
    public string FailureCode { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? DeadlineAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public sealed class ControlledAiDailyUsage
{
    public DateTime Day { get; set; }
    public int Calls { get; set; }
    public decimal ReservedUsd { get; set; }
    public int Revision { get; set; }
}

public sealed class ControlledAiEvent
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Action { get; set; } = "";
    public string ActorLabel { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class ControlledAiBudgetLedger
{
    public int Id { get; set; } = 1;
    public int Calls { get; set; }
    public decimal ReservedUsd { get; set; }
    public int Revision { get; set; }
}
