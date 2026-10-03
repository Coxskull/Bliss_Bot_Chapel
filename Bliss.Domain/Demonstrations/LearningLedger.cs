namespace Bliss.Domain.Demonstrations;

public sealed record LearningRecord(string ProspectSlug, string IdempotencyKey, string Body);

public sealed record LearningNote(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    string ProspectSlug,
    string Body,
    bool LaboratoryGraduated,
    bool AuthorizedTraffic,
    bool ProductionChanged,
    bool BehaviorChanged,
    bool Duplicate,
    int ModelCalls);

/// <summary>
/// Appends one research note after the laboratory graduates.
/// The note does not change production and does not invent traffic.
/// </summary>
public static class LearningLedger
{
    public const string Notice =
        "A research note is appended only after the laboratory graduates. No authorized traffic is on file. The note does not change production. Research does not control production. A model is not the record. Seven grooming models are not configured. This is not a traffic count. Green does not send. Delivery remains NOT_SENT.";

    public const string NoteBody =
        "The laboratory graduated. No authorized traffic is on file. This research note does not change production. Delivery remains NOT_SENT.";

    public const string DuplicateNotice =
        "That research note is already recorded. Production was not read again. Delivery remains NOT_SENT.";

    public static LearningNote Append(
        string? prospectSlug,
        bool laboratoryPassed,
        bool applyToProduction,
        bool authorizedTraffic,
        int? engagementCount,
        string? idempotencyKey,
        IReadOnlyList<LearningRecord>? existing)
    {
        if (existing is null)
        {
            throw new InvalidOperationException("A research note needs the stored ledger. None is invented.");
        }

        var slug = (prospectSlug ?? string.Empty).Trim();
        if (slug.Length < 3)
        {
            throw new InvalidOperationException("A stored prospect is required. None was invented.");
        }

        if (applyToProduction)
        {
            throw new InvalidOperationException("Research does not control production. None was changed.");
        }

        if (authorizedTraffic)
        {
            throw new InvalidOperationException("No authorized traffic is on file. None was invented.");
        }

        if (engagementCount is not null)
        {
            throw new InvalidOperationException("An engagement count is not on file. None was invented.");
        }

        RequireKey(idempotencyKey);
        var key = idempotencyKey!.Trim();
        if (!laboratoryPassed)
        {
            throw new InvalidOperationException("The laboratory has not graduated. Production is not changed.");
        }

        var prior = existing.FirstOrDefault(item =>
            string.Equals(item.ProspectSlug, slug, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.IdempotencyKey, key, StringComparison.Ordinal));
        if (prior is not null)
        {
            return new LearningNote(
                DuplicateNotice,
                false,
                "NOT_SENT",
                slug,
                prior.Body,
                true,
                false,
                false,
                false,
                true,
                0);
        }

        return new LearningNote(
            "The laboratory graduated. No authorized traffic is on file. Research does not control production. A model is not the record. Delivery remains NOT_SENT.",
            false,
            "NOT_SENT",
            slug,
            NoteBody,
            true,
            false,
            false,
            false,
            false,
            0);
    }

    public static IReadOnlyList<LearningRecord> Visible(string? prospectSlug, IReadOnlyList<LearningRecord>? rows)
    {
        if (rows is null)
        {
            throw new InvalidOperationException("A research reading needs the stored ledger. None is invented.");
        }

        var slug = (prospectSlug ?? string.Empty).Trim();
        if (slug.Length < 3)
        {
            throw new InvalidOperationException("A stored prospect is required. Another prospect's note is not shown.");
        }

        return rows.Where(item => string.Equals(item.ProspectSlug, slug, StringComparison.OrdinalIgnoreCase)).ToList();
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
