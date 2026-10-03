namespace Bliss.Domain.Demonstrations;

public sealed record DeliveryAssessment(
    bool Eligible,
    string Policy,
    string Adapter,
    string Eligibility,
    string Notice);

/// <summary>
/// Decides whether the preview adapter may prepare a copy.
/// Suppression and an unapproved policy or adapter withhold the road.
/// This type does not transmit anything.
/// </summary>
public static class DeliveryPolicy
{
    public const string PreviewPolicy = "preview-only";
    public const string PreviewAdapter = "preview-adapter";
    public const string PreviewAuthorization = "Prepare the preview";
    public const string Transmission = "NOT_SENT";

    public static DeliveryAssessment Decide(
        bool suppressed,
        bool roadStored,
        string? policy,
        string? adapter,
        string? authorization)
    {
        var policyName = (policy ?? string.Empty).Trim().ToLowerInvariant();
        var adapterName = (adapter ?? string.Empty).Trim().ToLowerInvariant();
        if (!roadStored)
        {
            return Withheld(policyName, adapterName, "A delivery decision needs a stored contact road. Alpha will not invent one.");
        }

        if (suppressed)
        {
            return Withheld(policyName, adapterName, "Suppression comes before the adapter. Nothing is sent.");
        }

        if (policyName != PreviewPolicy)
        {
            return Withheld(policyName, adapterName, "The delivery policy is not approved. Nothing is sent.");
        }

        if (adapterName != PreviewAdapter)
        {
            return Withheld(policyName, adapterName, "The channel adapter is not approved. Nothing is sent.");
        }

        if (!string.Equals((authorization ?? string.Empty).Trim(), PreviewAuthorization, StringComparison.Ordinal))
        {
            return Withheld(policyName, adapterName, "That text does not authorize the preview adapter. Nothing is sent.");
        }

        return new DeliveryAssessment(
            true,
            PreviewPolicy,
            PreviewAdapter,
            "ELIGIBLE",
            "The preview adapter prepared a copy. The road is eligible. Transmission remains NOT_SENT.");
    }

    private static DeliveryAssessment Withheld(string policy, string adapter, string notice) =>
        new(false, policy, adapter, "WITHHELD", notice);
}
