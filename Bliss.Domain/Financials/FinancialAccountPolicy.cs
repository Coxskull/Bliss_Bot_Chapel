namespace Bliss.Domain.Financials;

// Owner clarification: no verified Stripe account exists for ALPHA GLOBAL MEDIA or Bliss Chapel.
public static class FinancialAccountPolicy
{
    public const string StripeAccountStatus = "UNVERIFIED / OWNER DECISION REQUIRED";
    public const string TaskStatus = "IN PROGRESS — DEVELOPMENT CONTINUES";
}
