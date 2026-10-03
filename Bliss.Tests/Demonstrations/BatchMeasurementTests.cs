using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class BatchMeasurementTests
{
    [Fact]
    public void A_local_read_records_the_clock_and_claims_nothing()
    {
        var reading = BatchMeasurement.Read(
        [
            new MeasuredProspect("Mesa Norte", ["Hello. This is a private demonstration for Mesa Norte."]),
            new MeasuredProspect("Puerto Azul", ["Puerto Azul is preserved. Nothing is sent."])
        ],
        12,
        4096,
        1);

        Assert.False(reading.GreenMeansSend);
        Assert.Equal("NOT_SENT", reading.Delivery);
        Assert.False(reading.HostedAcceptanceClaimed);
        Assert.False(reading.FactoryTargetClaimed);
        Assert.False(reading.CensusClaimed);
        Assert.Contains("not hosted acceptance", reading.Notice);
        Assert.Contains("15-minute factory target is not claimed", reading.Notice);
        Assert.Contains("not a stored census", reading.Notice);
        Assert.Equal(2, reading.StoredProspects);
        Assert.Equal(12, reading.ElapsedMilliseconds);
        Assert.Contains("4096", reading.ResourceLine);
        Assert.Contains("not a hosted capacity claim", reading.ResourceLine);
        Assert.Equal(BatchMeasurement.CostUnrecorded, reading.CostLine);
        Assert.Contains("No invoice is on file", reading.CostLine);
        Assert.Equal(0, reading.Retries);
        Assert.Equal(0, reading.PartialFailures);
        Assert.True(reading.Recovered);
        Assert.False(reading.Leakage);
        Assert.Empty(reading.Failures);
    }

    [Fact]
    public void A_blank_name_is_kept_as_a_partial_failure_and_the_other_prospect_remains()
    {
        var reading = BatchMeasurement.Read(
        [
            new MeasuredProspect("  ", ["nameless"]),
            new MeasuredProspect("Casa Verde", ["Hello Casa Verde"])
        ],
        0,
        null,
        2);

        Assert.Equal(1, reading.StoredProspects);
        Assert.Equal(1, reading.PartialFailures);
        Assert.True(reading.Recovered);
        Assert.Equal(1, reading.Retries);
        Assert.Equal(BatchMeasurement.ResourceUnrecorded, reading.ResourceLine);
        Assert.Contains(BatchMeasurement.BlankName, reading.Failures);
        Assert.False(reading.Leakage);
        Assert.False(reading.HostedAcceptanceClaimed);
    }

    [Fact]
    public void A_message_that_names_another_prospect_is_leakage()
    {
        var reading = BatchMeasurement.Read(
        [
            new MeasuredProspect("Mesa Norte", ["See Puerto Azul tomorrow"]),
            new MeasuredProspect("Puerto Azul", ["Puerto Azul is preserved."])
        ],
        3,
        100,
        1);

        Assert.True(reading.Leakage);
        Assert.Equal(2, reading.StoredProspects);
        Assert.Contains(reading.Failures, line => line.Contains("Mesa Norte") && line.Contains("Puerto Azul") && line.Contains("not isolated"));
        Assert.Equal("NOT_SENT", reading.Delivery);
    }

    [Fact]
    public void A_missing_list_a_negative_clock_and_a_third_attempt_are_refused()
    {
        var missing = Assert.Throws<InvalidOperationException>(() => BatchMeasurement.Read(null, 1, 1, 1));
        Assert.Contains("None is invented", missing.Message);
        var negative = Assert.Throws<InvalidOperationException>(() => BatchMeasurement.Read([], -1, 1, 1));
        Assert.Contains("cannot be negative", negative.Message);
        var third = Assert.Throws<InvalidOperationException>(() => BatchMeasurement.Read([], 1, 1, 3));
        Assert.Contains("third attempt is not configured", third.Message);
        var key = Assert.Throws<InvalidOperationException>(() => BatchMeasurement.RequireKey("no"));
        Assert.Contains("idempotency key", key.Message);
    }

    [Fact]
    public void Measurement_source_does_not_claim_hosted_scale()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Demonstrations", "BatchMeasurement.cs"));
        Assert.Contains("not hosted acceptance", source);
        Assert.Contains("15-minute factory target is not claimed", source);
        Assert.Contains("not a stored census", source);
        Assert.Contains("No invoice is on file", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("$", source);
    }
}
