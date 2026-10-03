using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class LearningLedgerTests
{
    [Fact]
    public void A_graduated_laboratory_appends_a_research_note_and_leaves_production()
    {
        var note = LearningLedger.Append("casa-verde", true, false, false, null, "learning-casa-1", []);

        Assert.False(note.GreenMeansSend);
        Assert.Equal("NOT_SENT", note.Delivery);
        Assert.Equal("casa-verde", note.ProspectSlug);
        Assert.Equal(LearningLedger.NoteBody, note.Body);
        Assert.True(note.LaboratoryGraduated);
        Assert.False(note.AuthorizedTraffic);
        Assert.False(note.ProductionChanged);
        Assert.False(note.BehaviorChanged);
        Assert.False(note.Duplicate);
        Assert.Equal(0, note.ModelCalls);
        Assert.Contains("does not control production", note.Notice);
        Assert.Contains("not the record", note.Notice);
        Assert.Contains("No authorized traffic", note.Body);
    }

    [Fact]
    public void The_same_note_is_not_appended_again()
    {
        var again = LearningLedger.Append(
            "casa-verde",
            true,
            false,
            false,
            null,
            "learning-casa-1",
            [new LearningRecord("casa-verde", "learning-casa-1", LearningLedger.NoteBody)]);

        Assert.True(again.Duplicate);
        Assert.Equal(LearningLedger.DuplicateNotice, again.Notice);
        Assert.Equal(LearningLedger.NoteBody, again.Body);
        Assert.False(again.ProductionChanged);
        Assert.Equal(0, again.ModelCalls);
    }

    [Fact]
    public void Traffic_a_production_change_and_a_failed_laboratory_are_refused()
    {
        var production = Assert.Throws<InvalidOperationException>(() =>
            LearningLedger.Append("casa-verde", true, true, false, null, "learning-casa-1", []));
        Assert.Contains("does not control production", production.Message);
        var traffic = Assert.Throws<InvalidOperationException>(() =>
            LearningLedger.Append("casa-verde", true, false, true, null, "learning-casa-1", []));
        Assert.Contains("No authorized traffic", traffic.Message);
        var engagement = Assert.Throws<InvalidOperationException>(() =>
            LearningLedger.Append("casa-verde", true, false, false, 4, "learning-casa-1", []));
        Assert.Contains("None was invented", engagement.Message);
        var laboratory = Assert.Throws<InvalidOperationException>(() =>
            LearningLedger.Append("casa-verde", false, false, false, null, "learning-casa-1", []));
        Assert.Contains("has not graduated", laboratory.Message);
        Assert.Contains("not changed", laboratory.Message);
    }

    [Fact]
    public void Another_prospect_note_is_not_shown()
    {
        var rows = new[]
        {
            new LearningRecord("casa-verde", "learning-casa-1", LearningLedger.NoteBody),
            new LearningRecord("mesa-norte", "learning-mesa-1", LearningLedger.NoteBody)
        };
        var visible = LearningLedger.Visible("mesa-norte", rows);
        Assert.Single(visible);
        Assert.Equal("mesa-norte", visible[0].ProspectSlug);
        var missing = Assert.Throws<InvalidOperationException>(() => LearningLedger.Visible("no", rows));
        Assert.Contains("not shown", missing.Message);
    }

    [Fact]
    public void Learning_source_does_not_configure_grooming_models()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Demonstrations", "LearningLedger.cs"));
        Assert.Contains("Seven grooming models are not configured", source);
        Assert.Contains("A model is not the record", source);
        Assert.Contains("not a traffic count", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("$", source);
    }
}
