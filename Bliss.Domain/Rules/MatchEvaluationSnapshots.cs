using System.Text.Json;
using System.Text.Json.Serialization;
using Bliss.Domain.Entities;

namespace Bliss.Domain.Rules;

public static class MatchEvaluationSnapshots
{
    public const string AlgorithmVersion = "deterministic-rules/2.0.0";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static string BuildInput(BlissMatch match, Creator creator, AdvertiserOpportunity opportunity)
    {
        var payload = new
        {
            blissMatchId = match.Id,
            creatorId = creator.Id,
            creatorName = creator.Name,
            creatorCountryCode = creator.CountryCode,
            creatorPrimaryLanguage = creator.PrimaryLanguage,
            audienceSize = creator.AudienceSize,
            femalePercentage = creator.FemalePercentage,
            malePercentage = creator.MalePercentage,
            advertiserOpportunityId = opportunity.Id,
            opportunityName = opportunity.Name,
            opportunityCategory = opportunity.Category,
            opportunityStatus = opportunity.Status,
            marketCountryCode = opportunity.MarketCountryCode,
            opportunityLanguage = opportunity.Language,
            ruleVersionId = match.RuleVersionId,
            ruleVersion = match.RuleVersion?.Version,
            documentJson = match.RuleVersion?.DocumentJson
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string BuildOutput(RuleEvaluationResult result)
    {
        var payload = new
        {
            matchStatus = result.MatchStatus,
            overallScore = result.OverallScore,
            confidenceScore = result.ConfidenceScore,
            checks = result.Checks,
            scores = result.Scores
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
