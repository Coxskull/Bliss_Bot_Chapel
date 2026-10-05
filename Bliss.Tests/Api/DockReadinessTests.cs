using Bliss.Api.Runtime;
using Bliss.Infrastructure.Persistence;

namespace Bliss.Tests.Api;

public sealed class DockReadinessTests
{
    [Fact]
    public void Secret_file_supplies_the_connection_without_writing_appsettings()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bliss-dock-root-" + Guid.NewGuid().ToString("N")));
        var secrets = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bliss-dock-secrets-" + Guid.NewGuid().ToString("N")));
        var file = Path.Combine(secrets.FullName, "connection");
        const string secret = "Host=db.dock.example.test;Password=dock-file-secret-do-not-leak;SSL Mode=VerifyFull";
        try
        {
            File.WriteAllText(file, secret + "\n");
            var loaded = SecretFileLoader.Read(
                "ConnectionStrings:DefaultConnection",
                name => name.EndsWith("_FILE", StringComparison.Ordinal) ? file : "",
                root.FullName);

            Assert.Equal(secret, loaded);
        }
        finally
        {
            root.Delete(true);
            secrets.Delete(true);
        }
    }

    [Fact]
    public void Secret_file_refuses_a_path_inside_the_application_directory()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bliss-dock-inside-" + Guid.NewGuid().ToString("N")));
        var file = Path.Combine(root.FullName, "connection");
        const string secret = "dock-file-secret-do-not-leak";
        try
        {
            File.WriteAllText(file, secret);
            var exception = Assert.Throws<InvalidOperationException>(() =>
                SecretFileLoader.Read(
                    "ConnectionStrings:DefaultConnection",
                    name => name.EndsWith("_FILE", StringComparison.Ordinal) ? file : "",
                    root.FullName));

            Assert.Contains("outside the application directory", exception.Message);
            Assert.DoesNotContain(secret, exception.Message);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void Secret_file_refuses_an_inline_value_and_a_file_together()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SecretFileLoader.Read(
                "Authentication:ClientSecret",
                name => name.EndsWith("_FILE", StringComparison.Ordinal)
                    ? "/var/run/secrets/client"
                    : "dock-inline-secret-do-not-leak",
                Path.GetTempPath()));

        Assert.Contains("not both", exception.Message);
        Assert.DoesNotContain("dock-inline-secret-do-not-leak", exception.Message);
    }

    [Fact]
    public void Secret_file_refuses_a_missing_or_empty_file_without_echoing_a_value()
    {
        var missing = Assert.Throws<InvalidOperationException>(() =>
            SecretFileLoader.Read(
                "Runtime:DataProtectionCertificatePassword",
                name => name.EndsWith("_FILE", StringComparison.Ordinal) ? "/tmp/bliss-dock-missing-secret-file" : "",
                Path.GetTempPath()));
        Assert.Contains("not readable", missing.Message);

        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bliss-dock-empty-root-" + Guid.NewGuid().ToString("N")));
        var secrets = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bliss-dock-empty-" + Guid.NewGuid().ToString("N")));
        var file = Path.Combine(secrets.FullName, "empty");
        try
        {
            File.WriteAllText(file, "\n");
            var empty = Assert.Throws<InvalidOperationException>(() =>
                SecretFileLoader.Read(
                    "Runtime:DataProtectionCertificatePassword",
                    name => name.EndsWith("_FILE", StringComparison.Ordinal) ? file : "",
                    root.FullName));
            Assert.Contains("empty", empty.Message);
        }
        finally
        {
            root.Delete(true);
            secrets.Delete(true);
        }
    }

    [Fact]
    public void Migration_command_is_explicit_and_does_not_claim_acceptance()
    {
        Assert.False(MigrationCommand.Requested([]));
        Assert.False(MigrationCommand.Requested(["--Migrate"]));
        Assert.True(MigrationCommand.Requested(["--migrate"]));
        Assert.Contains("NOT_SENT", MigrationCommand.CompletionLine);
        Assert.Contains("not claimed", MigrationCommand.CompletionLine);
    }

    [Fact]
    public void Design_time_connection_uses_the_external_value_when_it_is_set()
    {
        Assert.Equal(
            BlissDbContextFactory.LocalDesignTimePlaceholder,
            BlissDbContextFactory.ResolveConnection("  "));
        Assert.Equal(
            "Host=db.dock.example.test;SSL Mode=VerifyFull",
            BlissDbContextFactory.ResolveConnection("  Host=db.dock.example.test;SSL Mode=VerifyFull  "));
    }

    [Fact]
    public void Publish_scanner_rejects_a_secret_left_in_the_output()
    {
        var script = Path.Combine(Phase20ProductionPostureEvaluatorTests.RepoRoot(), "scripts", "scan-publish-for-secrets.sh");
        var clean = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bliss-publish-clean-" + Guid.NewGuid().ToString("N")));
        var dirty = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bliss-publish-dirty-" + Guid.NewGuid().ToString("N")));
        try
        {
            File.WriteAllText(
                Path.Combine(clean.FullName, "appsettings.json"),
                """{"ConnectionStrings":{"DefaultConnection":""},"Authentication":{"ClientSecret":""},"Runtime":{"DataProtectionCertificatePassword":""}}""");
            Assert.Equal(0, Run(script, clean.FullName).ExitCode);

            File.WriteAllText(
                Path.Combine(dirty.FullName, "appsettings.json"),
                """{"ConnectionStrings":{"DefaultConnection":"Host=db;Password=dock-published-secret-do-not-leak"}}""");
            var leaked = Run(script, dirty.FullName);
            Assert.Equal(1, leaked.ExitCode);
            Assert.Contains("DefaultConnection", leaked.Text);
            Assert.DoesNotContain("dock-published-secret-do-not-leak", leaked.Text);
        }
        finally
        {
            clean.Delete(true);
            dirty.Delete(true);
        }
    }

    [Fact]
    public void VerifyFull_rehearsal_refuses_without_the_local_opt_in()
    {
        var script = Path.Combine(Phase20ProductionPostureEvaluatorTests.RepoRoot(), "scripts", "postgres-verifyfull-rehearsal.sh");
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "/bin/bash",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(script);
        start.Environment["BLISS_VERIFYFULL_REHEARSAL"] = "0";
        start.Environment["BLISS_VERIFYFULL_REHEARSAL_TARGET"] = "local";
        using var process = System.Diagnostics.Process.Start(start);
        Assert.NotNull(process);
        var text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(10000);
        Assert.Equal(2, process.ExitCode);
        Assert.Contains("refused", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Container_contract_does_not_embed_a_secret()
    {
        var root = Phase20ProductionPostureEvaluatorTests.RepoRoot();
        var dockerfile = File.ReadAllText(Path.Combine(root, "Dockerfile"));
        var ignore = File.ReadAllText(Path.Combine(root, ".dockerignore"));
        Assert.Contains("ASPNETCORE_ENVIRONMENT=Production", dockerfile);
        Assert.Contains("Bliss.Api.dll", dockerfile);
        Assert.DoesNotContain("Password=", dockerfile);
        Assert.DoesNotContain("ClientSecret=", dockerfile);
        Assert.Contains(".env", ignore);
        Assert.Contains("*.pfx", ignore);
    }

    private static (int ExitCode, string Text) Run(string script, string directory)
    {
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "/bin/bash",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(script);
        start.ArgumentList.Add(directory);
        using var process = System.Diagnostics.Process.Start(start);
        Assert.NotNull(process);
        var text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(10000);
        return (process.ExitCode, text);
    }
}
