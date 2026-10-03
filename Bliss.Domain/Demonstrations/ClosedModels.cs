namespace Bliss.Domain.Demonstrations;

public sealed record ModelRecord(
    string ReadingKey,
    int ModelCalls,
    int NoteCount,
    int PassedCount,
    int ScenarioCount);

public sealed record ModelSnapshot(
    int ConfiguredModels,
    int ModelCalls,
    int NoteCount,
    bool LaboratoryPassed,
    int PassedCount,
    int ScenarioCount);

public sealed record ModelReading(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    string ReadingKey,
    int ConfiguredModels,
    int ModelCalls,
    int NoteCount,
    bool LaboratoryPassed,
    int PassedCount,
    int ScenarioCount,
    bool ModelsConfigured,
    bool AuthorizedTraffic,
    bool ProductionChanged,
    bool Duplicate);

/// <summary>
/// Stores one reading that the grooming models are not configured.
/// A model is not named and a model is not called.
/// </summary>
public static class ClosedModels
{
    public const string Notice =
        "A closed-model reading stores that seven grooming models are not configured. Configured models stay at zero. Model calls stay at the stored count. No authorized traffic is on file. Production is not changed. This is not a traffic count. The learning page is unchanged. Green does not send. Delivery remains NOT_SENT.";

    public const string DuplicateNotice =
        "That closed-model reading is already stored. A grooming model was not configured. Delivery remains NOT_SENT.";

    public static ModelSnapshot Read(int modelCalls, int noteCount, bool laboratoryPassed, int passedCount, int scenarioCount)
    {
        RequireCounts(modelCalls, noteCount, laboratoryPassed, passedCount, scenarioCount);
        return new ModelSnapshot(0, modelCalls, noteCount, laboratoryPassed, passedCount, scenarioCount);
    }

    public static ModelReading Store(
        string? readingKey,
        int modelCalls,
        int noteCount,
        bool laboratoryPassed,
        int passedCount,
        int scenarioCount,
        bool configureModel,
        int? engagementCount,
        bool authorizedTraffic,
        IReadOnlyList<ModelRecord>? existing)
    {
        if (existing is null)
        {
            throw new InvalidOperationException("The closed-model history is required. None is invented.");
        }

        if (configureModel)
        {
            throw new InvalidOperationException("A grooming model is not configured. None was invented.");
        }

        if (engagementCount is not null)
        {
            throw new InvalidOperationException("An engagement count is not on file. None was invented.");
        }

        if (authorizedTraffic)
        {
            throw new InvalidOperationException("No authorized traffic is on file. None was invented.");
        }

        var snapshot = Read(modelCalls, noteCount, laboratoryPassed, passedCount, scenarioCount);
        RequireKey(readingKey);
        var key = readingKey!.Trim();
        var prior = existing.FirstOrDefault(item => string.Equals(item.ReadingKey, key, StringComparison.Ordinal));
        if (prior is not null)
        {
            var priorPassed = prior.PassedCount == prior.ScenarioCount;
            return new ModelReading(
                DuplicateNotice,
                false,
                "NOT_SENT",
                key,
                0,
                prior.ModelCalls,
                prior.NoteCount,
                priorPassed,
                prior.PassedCount,
                prior.ScenarioCount,
                false,
                false,
                false,
                true);
        }

        return new ModelReading(
            Notice,
            false,
            "NOT_SENT",
            key,
            0,
            snapshot.ModelCalls,
            snapshot.NoteCount,
            snapshot.LaboratoryPassed,
            snapshot.PassedCount,
            snapshot.ScenarioCount,
            false,
            false,
            false,
            false);
    }

    public static void RequireKey(string? readingKey)
    {
        var key = (readingKey ?? string.Empty).Trim();
        if (key.Length < 8 || key.Length > 80)
        {
            throw new InvalidOperationException("A reading key is required. None is invented.");
        }

        foreach (var character in key)
        {
            var letter = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-';
            if (!letter)
            {
                throw new InvalidOperationException("A reading key is required. None is invented.");
            }
        }
    }

    private static void RequireCounts(int modelCalls, int noteCount, bool laboratoryPassed, int passedCount, int scenarioCount)
    {
        if (modelCalls < 0)
        {
            throw new InvalidOperationException("A model call count must be zero or greater. None is invented.");
        }

        if (noteCount < 0)
        {
            throw new InvalidOperationException("A research note count must be zero or greater. None is invented.");
        }

        if (scenarioCount < 0 || passedCount < 0 || passedCount > scenarioCount)
        {
            throw new InvalidOperationException("The laboratory result is required. None is invented.");
        }

        if (laboratoryPassed != (passedCount == scenarioCount))
        {
            throw new InvalidOperationException("The laboratory result is required. None is invented.");
        }
    }
}
