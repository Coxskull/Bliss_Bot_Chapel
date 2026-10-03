namespace Bliss.Domain.Entities;

public class SubscriptionRegisterRow
{
    public Guid Id { get; set; }
    public string ServiceKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Capability { get; set; } = string.Empty;
    public string? Classification { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AccountOwner { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public decimal? MonthlyAmount { get; set; }
    public string? Currency { get; set; }
    public string Notice { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class SubscriptionLedgerAudit
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ServiceKey { get; set; } = string.Empty;
    public string? Classification { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? MonthlyAmount { get; set; }
    public string? Currency { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public string? EstimatedCurrency { get; set; }
    public string? EngineeringContract { get; set; }
    public string? Provider { get; set; }
    public string? Capability { get; set; }
    public string? UsageCharges { get; set; }
    public string? Alternatives { get; set; }
    public string? BuildAlternative { get; set; }
    public string? WhyAlphaIsInsufficient { get; set; }
    public string? RequiredDate { get; set; }
    public string? RequiredOrOptional { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Notice { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}

public class FactoryBudgetState
{
    public Guid Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public decimal? CeilingAmount { get; set; }
    public string? CeilingCurrency { get; set; }
    public decimal? RecordedSpend { get; set; }
    public string Notice { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class FactoryBudgetAudit
{
    public Guid Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public decimal CeilingAmount { get; set; }
    public string CeilingCurrency { get; set; } = string.Empty;
    public decimal? RecordedSpend { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Notice { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}
