using Bliss.Domain.WeddingPlanner;

namespace Bliss.Tests.WeddingPlanner;

public sealed class WeddingPlannerMeasurementLearningValidationTests
{
    [Fact]
    public void Metrics_use_null_denominators_and_six_decimal_rounding()
    {
        var zero = WeddingPlannerMeasurementLearningValidation.ComputeMetrics(0, 0, 0, 0m, null);
        Assert.Null(zero.Ctr);
        Assert.Null(zero.ConversionRate);
        Assert.Null(zero.Cpm);
        Assert.Null(zero.Cpc);
        Assert.Null(zero.Cpa);
        Assert.Null(zero.Roas);

        var withRevenue = WeddingPlannerMeasurementLearningValidation.ComputeMetrics(1000, 25, 5, 50m, 125m);
        Assert.Equal(0.025000m, withRevenue.Ctr);
        Assert.Equal(0.200000m, withRevenue.ConversionRate);
        Assert.Equal(50.000000m, withRevenue.Cpm);
        Assert.Equal(2.000000m, withRevenue.Cpc);
        Assert.Equal(10.000000m, withRevenue.Cpa);
        Assert.Equal(2.500000m, withRevenue.Roas);

        var noRevenue = WeddingPlannerMeasurementLearningValidation.ComputeMetrics(10, 2, 1, 0m, null);
        Assert.Null(noRevenue.Roas);
        Assert.Equal(0m, noRevenue.Cpc);
    }

    [Fact]
    public void Observation_window_bounds_are_enforced()
    {
        var now = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(WeddingPlannerMeasurementLearningValidation.IsValidObservationWindow(
            now.AddDays(-10), now.AddDays(-1), now));
        Assert.False(WeddingPlannerMeasurementLearningValidation.IsValidObservationWindow(
            now.AddDays(-1), now.AddDays(-1), now));
        Assert.False(WeddingPlannerMeasurementLearningValidation.IsValidObservationWindow(
            now.AddDays(-10), now.AddDays(1), now));
        Assert.False(WeddingPlannerMeasurementLearningValidation.IsValidObservationWindow(
            now.AddDays(-400), now.AddDays(-1), now));
        Assert.False(WeddingPlannerMeasurementLearningValidation.IsValidObservationWindow(
            DateTime.SpecifyKind(now.AddDays(-2), DateTimeKind.Unspecified),
            DateTime.SpecifyKind(now.AddDays(-1), DateTimeKind.Unspecified),
            now));
    }

    [Fact]
    public void Currency_code_requires_three_letter_uppercase()
    {
        Assert.True(WeddingPlannerMeasurementLearningValidation.IsIso4217LikeCurrency("USD"));
        Assert.False(WeddingPlannerMeasurementLearningValidation.IsIso4217LikeCurrency("usd"));
        Assert.False(WeddingPlannerMeasurementLearningValidation.IsIso4217LikeCurrency("US"));
        Assert.False(WeddingPlannerMeasurementLearningValidation.IsIso4217LikeCurrency("USDD"));
    }

    [Fact]
    public void Performance_output_rejects_forbidden_causal_claims()
    {
        var json = """
            {
              "schemaVersion":"performance-analysis-worker-output.v1",
              "workerProfileVersion":"PERFORMANCE_ANALYSIS_V1",
              "marker":"SYNTHETIC DEVELOPMENT MEASUREMENT LEARNING",
              "contributions":[{
                "logicalRole":"PERFORMANCE_ANALYST",
                "summary":"Observed association only.",
                "descriptivePatterns":["This campaign caused higher clicks."],
                "limitations":["Human attestation only."],
                "dataGaps":["No events."]
              }]
            }
            """;
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerMeasurementLearningValidation.CanonicalizePerformanceAnalysisOutput(json, true));
        Assert.Contains("caused", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Learning_synthesis_requires_exactly_two_mapped_roles()
    {
        var json = """
            {
              "schemaVersion":"learning-synthesis-worker-output.v1",
              "workerProfileVersion":"LEARNING_SYNTHESIS_V1",
              "marker":"SYNTHETIC DEVELOPMENT MEASUREMENT LEARNING",
              "contributions":[{
                "logicalRole":"LEARNING_SYNTHESIZER",
                "summary":"Hypothesis only.",
                "learningHypotheses":["Hypothesis: association may exist."],
                "limitations":["Advisory only."]
              }]
            }
            """;
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerMeasurementLearningValidation.CanonicalizeLearningSynthesisOutput(json, true));
        Assert.Contains("exactly two", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
