using System.Text.Json;

namespace Bliss.Tests.Architecture;

public sealed class EconomicsPhase7WorkflowTests
{
    [Fact]
    public void N8n_template_is_inactive_provider_configured_and_staging_only()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "n8n",
            "workflows",
            "economics-public-research-staging.json");
        var text = File.ReadAllText(path);
        using var document = JsonDocument.Parse(text);

        Assert.False(document.RootElement.GetProperty("active").GetBoolean());
        Assert.Contains("$env.BLISS_API_BASE", text);
        Assert.Contains("$env.APPROVED_RESEARCH_ENDPOINT", text);
        Assert.Contains("$env.APPROVED_AI_EXTRACTION_ENDPOINT", text);
        Assert.Contains("/candidates", text);
        Assert.Contains("AI cannot submit VERIFIED", text);
        Assert.Contains("AI cannot submit HIGH confidence", text);
        Assert.Contains("never writes the database", text);
        Assert.DoesNotContain("localhost", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("postgres", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"active\": true", text, StringComparison.Ordinal);
        Assert.DoesNotContain("apiKey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git"))
                || File.Exists(Path.Combine(current.FullName, "Bliss.sln")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
