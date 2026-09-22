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
