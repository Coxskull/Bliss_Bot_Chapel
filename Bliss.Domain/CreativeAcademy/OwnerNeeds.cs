namespace Bliss.Domain.CreativeAcademy;

public sealed record OwnerNeed(
    string NeedId,
    string Status,
    string OwnerEntry,
    string Need);

/// <summary>
/// Owner inputs that later phases must not invent. A blank entry stays blank.
/// </summary>
public static class OwnerNeeds
{
    public const string Open = "OPEN";
    public const string Supplied = "SUPPLIED";

    public static IReadOnlyList<OwnerNeed> Parse(string? tsv)
    {
        var rows = new List<OwnerNeed>();
        if (string.IsNullOrWhiteSpace(tsv))
        {
            return rows;
        }

        foreach (var line in tsv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("needId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cells = line.Split('\t');
            if (cells.Length < 4 || string.IsNullOrWhiteSpace(cells[0]))
            {
                continue;
            }

            var status = cells[1].Trim();
            if (!status.Equals(Open, StringComparison.Ordinal) && !status.Equals(Supplied, StringComparison.Ordinal))
            {
                status = Open;
            }

            rows.Add(new OwnerNeed(cells[0].Trim(), status, cells[2].Trim(), cells[3].Trim()));
        }

        return rows;
    }
}
