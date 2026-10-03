namespace Bliss.Domain.Demonstrations;

public sealed record PreviewCopy(string Text, string Transmission);

/// <summary>
/// The only approved channel adapter in this phase. It prepares a copy
/// on the prospect record. It has no transmitter.
/// </summary>
public static class PreviewDeliveryAdapter
{
    public static PreviewCopy Prepare(string businessName)
    {
        var name = string.IsNullOrWhiteSpace(businessName) ? "the prospect" : businessName.Trim();
        return new PreviewCopy(
            "Prepared copy for " + name + ". The preview adapter did not transmit it.",
            DeliveryPolicy.Transmission);
    }
}
