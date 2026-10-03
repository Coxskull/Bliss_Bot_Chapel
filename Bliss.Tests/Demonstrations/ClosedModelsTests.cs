using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class ClosedModelsTests
{
    [Fact]
    public void A_closed_reading_stores_zero_configured_models_and_the_measured_calls()
    {
        var laboratory = ConversationLaboratory.Run();
        var reading = ClosedModels.Store(
            "models-reading-1",
            0,
            1,
            laboratory.Passed,
            laboratory.PassedCount,
            laboratory.ScenarioCount,
            false,
            null,
            false,
            []);

        Assert.False(reading.GreenMeansSend);
        Assert.Equal("NOT_SENT", reading.Delivery);
        Assert.Equal(0, reading.ConfiguredModels);
        Assert.Equal(0, reading.ModelCalls);
        Assert.Equal(1, reading.NoteCount);
        Assert.Equal(laboratory.PassedCount, reading.PassedCount);
        Assert.Equal(laboratory.ScenarioCount, reading.ScenarioCount);
        Assert.False(reading.ModelsConfigured);
        Assert.False(reading.AuthorizedTraffic);
        Assert.False(reading.ProductionChanged);
        Assert.False(reading.Duplicate);
        Assert.Contains("not configured", reading.Notice);
        Assert.Contains("not a traffic count", reading.Notice);
    }

    [Fact]
    public void A_repeat_does_not_raise_the_stored_model_calls()
    {
        var again = ClosedModels.Store(
            "models-reading-1",
            4,
            2,
            true,
            10,
            10,
            false,
            null,
            false,
            [new ModelRecord("models-reading-1", 0, 1, 10, 10)]);
        Assert.True(again.Duplicate);
        Assert.Equal(0, again.ConfiguredModels);
        Assert.Equal(0, again.ModelCalls);
        Assert.Equal(1, again.NoteCount);
        Assert.Equal(ClosedModels.DuplicateNotice, again.Notice);
        Assert.False(again.ModelsConfigured);
        Assert.False(again.ProductionChanged);
    }

    [Fact]
    public void Configuring_a_model_traffic_or_an_engagement_count_is_refused()
    {
        var configured = Assert.Throws<InvalidOperationException>(() =>
            ClosedModels.Store("models-reading-1", 0, 0, true, 10, 10, true, null, false, []));
        Assert.Contains("not configured", configured.Message);
        var traffic = Assert.Throws<InvalidOperationException>(() =>
            ClosedModels.Store("models-reading-1", 0, 0, true, 10, 10, false, null, true, []));
        Assert.Contains("authorized traffic", traffic.Message);
        var engagement = Assert.Throws<InvalidOperationException>(() =>
            ClosedModels.Store("models-reading-1", 0, 0, true, 10, 10, false, 3, false, []));
        Assert.Contains("not on file", engagement.Message);
    }

    [Fact]
    public void A_laboratory_result_that_does_not_match_its_counts_is_refused()
    {
        var mismatch = Assert.Throws<InvalidOperationException>(() =>
            ClosedModels.Store("models-reading-1", 0, 0, true, 9, 10, false, null, false, []));
        Assert.Contains("None is invented", mismatch.Message);
    }

    [Fact]
    public void Closed_model_source_does_not_name_a_provider_or_a_call()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Demonstrations", "ClosedModels.cs"));
        Assert.Contains("not configured", source);
        Assert.Contains("not a traffic count", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("$", source);
    }
}
