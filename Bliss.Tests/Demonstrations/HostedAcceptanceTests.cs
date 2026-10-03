using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class HostedAcceptanceTests
{
    [Fact]
    public void A_development_reading_does_not_claim_hosted_acceptance()
    {
        var reading = HostedAcceptance.Store(
            "Development", false, false, false, false, false, true, true, false, "hosted-reading-1", []);

        Assert.False(reading.GreenMeansSend);
        Assert.Equal("NOT_SENT", reading.Delivery);
        Assert.Equal("Development", reading.EnvironmentName);
        Assert.False(reading.ProductionGatesApplied);
        Assert.False(reading.HostedDatabaseConfigured);
        Assert.False(reading.IdentityProviderHttps);
        Assert.False(reading.BackupDeclared);
        Assert.True(reading.SecretMaterialExternal);
        Assert.True(reading.RoleClaimsDistinct);
        Assert.False(reading.HostedAcceptanceClaimed);
        Assert.False(reading.IdentityContacted);
        Assert.False(reading.BackupDrillRun);
        Assert.False(reading.Duplicate);
        Assert.Contains("not claimed", reading.Notice);
    }

    [Fact]
    public void Satisfied_gates_still_leave_hosted_acceptance_unclaimed()
    {
        var reading = HostedAcceptance.Store(
            "Production", true, true, true, true, true, true, true, false, "hosted-reading-1", []);

        Assert.True(reading.ProductionGatesApplied);
        Assert.True(reading.HostedDatabaseConfigured);
        Assert.True(reading.DatabaseServerCertificateVerified);
        Assert.True(reading.IdentityProviderHttps);
        Assert.True(reading.BackupDeclared);
        Assert.False(reading.HostedAcceptanceClaimed);
        Assert.False(reading.IdentityContacted);
        Assert.False(reading.BackupDrillRun);

        var again = HostedAcceptance.Store(
            "Production", true, true, true, true, true, true, true, false, "hosted-reading-1", ["hosted-reading-1"]);
        Assert.True(again.Duplicate);
        Assert.False(again.HostedAcceptanceClaimed);
        Assert.Equal(HostedAcceptance.DuplicateNotice, again.Notice);
    }

    [Fact]
    public void A_hosted_claim_and_a_missing_environment_are_refused()
    {
        var claim = Assert.Throws<InvalidOperationException>(() =>
            HostedAcceptance.Store("Development", false, false, false, false, false, true, true, true, "hosted-reading-1", []));
        Assert.Contains("not on file", claim.Message);
        var missing = Assert.Throws<InvalidOperationException>(() =>
            HostedAcceptance.Store(" ", false, false, false, false, false, true, true, false, "hosted-reading-1", []));
        Assert.Contains("None is invented", missing.Message);
    }

    [Fact]
    public void Hosted_source_does_not_claim_a_deployment()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Demonstrations", "HostedAcceptance.cs"));
        Assert.Contains("not claimed", source);
        Assert.Contains("not on file", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("$", source);
    }
}
