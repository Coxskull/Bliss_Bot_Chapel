namespace Bliss.Domain.Demonstrations;

public sealed record FactoryConcept(
    string Id,
    string RecipeVersion,
    string QaStatus,
    string Headline,
    string Subhead,
    string Detail,
    string CallToAction,
    string QrDestination,
    Guid SourceClipId,
    bool SourceClipQualified,
    bool FlattenedFileExists,
    bool QrFileExists);

public sealed record FactoryProspect(
    string Slug,
    string BusinessName,
    string Market,
    string Language,
    string Disclosure,
    string DecisionMakerName,
    string ProspectState,
    bool Suppressed,
    IReadOnlyList<FactoryConcept> Concepts);

public sealed record FactoryLibrary(
    IReadOnlyList<FactoryProspect> Prospects,
    int QualifiedClipCount);

public sealed record FactoryManifest(
    string Status,
    int ProspectCount,
    int PreservedCount,
    int SuppressedCount,
    int DemonstrationCount,
    int ConceptCount,
    int QualifiedClipCount,
    int ChecksPassed,
    int ExceptionCount,
    int AiCalls,
    string Cost,
    IReadOnlyList<string> Exceptions);

public static class FactoryBatch
{
    public const string CostSentence =
        "No dollar amount is recorded. Economics remains the only price authority. AI calls are 0.";

    public static FactoryManifest Inspect(FactoryLibrary library)
    {
        var exceptions = new List<string>();
        var passed = 0;
        var preserved = 0;
        var suppressed = 0;
        var demonstrations = 0;
        var concepts = 0;
        foreach (var prospect in library.Prospects)
        {
            if (prospect.Suppressed)
            {
                suppressed++;
            }

            if (prospect.ProspectState == "PRESERVED")
            {
                preserved++;
                if (prospect.Concepts.Count == 0)
                {
                    passed++;
                }
                else
                {
                    exceptions.Add(prospect.BusinessName + " is preserved and has a demonstration.");
                }
            }

            if (prospect.Concepts.Count > 0)
            {
                demonstrations++;
            }

            foreach (var concept in prospect.Concepts)
            {
                concepts++;
                var recipe = new DemonstrationRecipe(
                    concept.RecipeVersion,
                    prospect.Slug,
                    prospect.BusinessName,
                    prospect.Market,
                    prospect.Language,
                    concept.Headline,
                    concept.Subhead,
                    concept.Detail,
                    concept.CallToAction,
                    concept.QrDestination,
                    prospect.Disclosure,
                    concept.SourceClipId,
                    prospect.DecisionMakerName);
                var reasons = RecipeRuntime.Check(recipe);
                if (reasons.Count == 0)
                {
                    passed++;
                }
                else
                {
                    exceptions.AddRange(reasons.Select(reason => prospect.BusinessName + " " + concept.Id + ": " + reason));
                }

                if (concept.SourceClipQualified)
                {
                    passed++;
                }
                else
                {
                    exceptions.Add(prospect.BusinessName + " " + concept.Id + ": The source slice is not qualified.");
                }

                if (concept.FlattenedFileExists)
                {
                    passed++;
                }
                else
                {
                    exceptions.Add(prospect.BusinessName + " " + concept.Id + ": The flattened composite is missing.");
                }

                if (concept.QrFileExists)
                {
                    passed++;
                }
                else
                {
                    exceptions.Add(prospect.BusinessName + " " + concept.Id + ": The QR file is missing.");
                }
            }
        }

        return new FactoryManifest(
            exceptions.Count == 0 ? "PASSED" : "EXCEPTIONS",
            library.Prospects.Count,
            preserved,
            suppressed,
            demonstrations,
            concepts,
            library.QualifiedClipCount,
            passed,
            exceptions.Count,
            0,
            CostSentence,
            exceptions);
    }
}
