using System.Globalization;

namespace Bliss.Domain.Demonstrations;

public sealed record MeasuredProspect(string? BusinessName, IReadOnlyList<string>? Messages);

public sealed record MeasurementReading(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    bool HostedAcceptanceClaimed,
    bool FactoryTargetClaimed,
    bool CensusClaimed,
    int StoredProspects,
    long ElapsedMilliseconds,
    string ResourceLine,
    string CostLine,
    int Retries,
    int PartialFailures,
    bool Recovered,
    bool Leakage,
    IReadOnlyList<string> Failures);

/// <summary>
/// Measures one local database read. Hosted acceptance is not claimed.
/// The 15-minute factory target is not claimed. No invoice amount is recorded.
/// </summary>
public static class BatchMeasurement
{
    public const string Notice =
        "This measurement reads the local database. It is not hosted acceptance. The 15-minute factory target is not claimed. A rung of 100, 1,000, or 10,000 is not a stored census. No invoice is on file. A partial failure keeps the other stored prospects. A message that names another prospect is leakage. Green does not send. Delivery remains NOT_SENT.";

    public const string CostUnrecorded =
        "No invoice is on file. Cost stays unrecorded. None was invented. Economics remains the only price authority.";

    public const string ResourceUnrecorded =
        "Working set is not recorded. None was invented. This is not a hosted capacity claim.";

    public const string BlankName =
        "A stored prospect has no usable name. It was not invented and the others remain.";

    public static MeasurementReading Read(
        IReadOnlyList<MeasuredProspect>? prospects,
        long elapsedMilliseconds,
        long? workingSetBytes,
        int attempt)
    {
        if (prospects is null)
        {
            throw new InvalidOperationException("A batch measurement needs the stored prospects. None is invented.");
        }

        if (elapsedMilliseconds < 0)
        {
            throw new InvalidOperationException("Elapsed time cannot be negative. None is invented.");
        }

        if (attempt is not 1 and not 2)
        {
            throw new InvalidOperationException("A third attempt is not configured. None was invented.");
        }

        if (workingSetBytes is < 0)
        {
            throw new InvalidOperationException("A working set cannot be negative. None is invented.");
        }

        var usable = new List<(string Name, IReadOnlyList<string> Messages)>();
        var failures = new List<string>();
        var partial = 0;
        foreach (var item in prospects)
        {
            var name = (item.BusinessName ?? string.Empty).Trim();
            if (name.Length < 3)
            {
                partial++;
                if (!failures.Contains(BlankName))
                {
                    failures.Add(BlankName);
                }

                continue;
            }

            usable.Add((name, item.Messages ?? []));
        }

        var leakage = false;
        foreach (var item in usable)
        {
            foreach (var message in item.Messages)
            {
                var text = message ?? string.Empty;
                foreach (var other in usable)
                {
                    if (string.Equals(other.Name, item.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (text.Contains(other.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        leakage = true;
                        var line = item.Name + " stores a message that names " + other.Name + ". The conversations are not isolated.";
                        if (!failures.Contains(line))
                        {
                            failures.Add(line);
                        }
                    }
                }
            }
        }

        var resource = workingSetBytes is null or 0
            ? ResourceUnrecorded
            : "Working set is " + workingSetBytes.Value.ToString(CultureInfo.InvariantCulture)
                + " bytes. This is one process reading. It is not a hosted capacity claim.";

        return new MeasurementReading(
            Notice,
            false,
            "NOT_SENT",
            false,
            false,
            false,
            usable.Count,
            elapsedMilliseconds,
            resource,
            CostUnrecorded,
            attempt - 1,
            partial,
            true,
            leakage,
            failures);
    }

    public static void RequireKey(string? idempotencyKey)
    {
        var key = (idempotencyKey ?? string.Empty).Trim();
        if (key.Length < 8 || key.Length > 80)
        {
            throw new InvalidOperationException("An idempotency key is required. None is invented.");
        }

        foreach (var character in key)
        {
            var letter = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-';
            if (!letter)
            {
                throw new InvalidOperationException("An idempotency key is required. None is invented.");
            }
        }
    }
}
