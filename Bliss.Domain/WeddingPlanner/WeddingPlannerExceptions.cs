namespace Bliss.Domain.WeddingPlanner;

public sealed class WeddingPlannerNotFoundException : InvalidOperationException
{
    public WeddingPlannerNotFoundException(string message) : base(message)
    {
    }
}

public sealed class WeddingPlannerForbiddenException : InvalidOperationException
{
    public WeddingPlannerForbiddenException(string message) : base(message)
    {
    }
}

/// <summary>
/// Raised after a durable FAILED agent run is persisted when the AI provider fails.
/// Controllers map this to HTTP 502.
/// </summary>
public sealed class WeddingPlannerProviderException : InvalidOperationException
{
    public Guid AgentRunId { get; }

    public WeddingPlannerProviderException(Guid agentRunId, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        AgentRunId = agentRunId;
    }
}

/// <summary>
/// Raised after a durable FAILED research job is persisted when source acquisition fails
/// (0 agent runs). Controllers map this to HTTP 502.
/// </summary>
public sealed class WeddingPlannerResearchJobProviderException : InvalidOperationException
{
    public Guid ResearchJobId { get; }
    public string ErrorCode { get; }

    public WeddingPlannerResearchJobProviderException(
        Guid researchJobId,
        string message,
        string errorCode,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ResearchJobId = researchJobId;
        ErrorCode = errorCode;
    }
}
