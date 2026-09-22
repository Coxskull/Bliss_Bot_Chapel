using Bliss.Domain.WeddingPlanner;

namespace Bliss.Tests.WeddingPlanner;

public sealed class WeddingPlannerCuratorValidationTests
{
    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("http://cdn.example.com/a")]
    [InlineData("https://203.0.113.10/public")]
    public void Citation_url_accepts_safe_http_https(string url)
    {
        var normalized = WeddingPlannerCuratorValidation.ValidateCitationUrl(url, Array.Empty<string>());
        Assert.StartsWith(url.Split(':')[0], normalized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://user:pass@example.com/x")]
    [InlineData("https://example.com/x#frag")]
    [InlineData("https://localhost/x")]
    [InlineData("https://foo.localhost/x")]
    [InlineData("https://127.0.0.1/x")]
    [InlineData("https://10.0.0.5/x")]
    [InlineData("https://192.168.1.1/x")]
    [InlineData("https://172.16.4.1/x")]
    [InlineData("https://169.254.169.254/latest")]
    [InlineData("ftp://example.com/x")]
    [InlineData("not-a-url")]
    public void Citation_url_rejects_unsafe_forms(string url)
    {
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCuratorValidation.ValidateCitationUrl(url, Array.Empty<string>()));
    }

    [Fact]
    public void Allowlist_exact_and_subdomain_pass_lookalikes_fail()
    {
        var allowed = new[] { "example.com" };
        Assert.Equal(
            "https://example.com/a",
            WeddingPlannerCuratorValidation.ValidateCitationUrl("https://example.com/a", allowed));
        Assert.Equal(
            "https://cdn.example.com/a",
            WeddingPlannerCuratorValidation.ValidateCitationUrl("https://cdn.example.com/a", allowed));
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCuratorValidation.ValidateCitationUrl("https://example.com.attacker.tld/a", allowed));
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCuratorValidation.ValidateCitationUrl("https://notexample.com/a", allowed));
    }

    [Fact]
    public void Allowed_domain_entries_reject_scheme_wildcard_and_ip_literals()
    {
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCuratorValidation.NormalizeAllowedDomainEntry("https://example.com"));
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCuratorValidation.NormalizeAllowedDomainEntry("*.example.com"));
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCuratorValidation.NormalizeAllowedDomainEntry("203.0.113.10"));
        Assert.Equal("example.com", WeddingPlannerCuratorValidation.NormalizeAllowedDomainEntry(" Example.COM "));
    }

    [Fact]
    public void Empty_allowlist_still_enforces_safety_rules()
    {
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCuratorValidation.ValidateCitationUrl("https://127.0.0.1/x", Array.Empty<string>()));
        var ok = WeddingPlannerCuratorValidation.ValidateCitationUrl(
            "https://docs.example.invalid/synthetic",
            Array.Empty<string>());
        Assert.Contains("example.invalid", ok, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_catalog_requires_strict_schema_and_valid_urls()
    {
        var good = """
            {"schemaVersion":"research-source-catalog.v1","sources":[{"id":"src_1","title":"SYNTHETIC A","url":"https://a.example.invalid/x","publisher":"SYNTHETIC","retrievedAt":"2026-01-01T00:00:00Z","synthetic":true}]}
            """;
        var catalog = WeddingPlannerCuratorValidation.CanonicalizeSourceCatalog(good, Array.Empty<string>());
        Assert.Single(catalog.Sources);
        Assert.Contains("SYNTHETIC", catalog.Sources[0].Title, StringComparison.Ordinal);

        var unknownField = """
            {"schemaVersion":"research-source-catalog.v1","sources":[],"extra":true}
            """;
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCuratorValidation.CanonicalizeSourceCatalog(unknownField, Array.Empty<string>()));
    }

    [Fact]
    public void Stage_output_accepts_only_assigned_roles_and_rejects_dangling_citations()
    {
        var known = new HashSet<string>(StringComparer.Ordinal) { "src_1" };
        var good = """
            {"schemaVersion":"curator-worker-output.v1","workerProfileVersion":"CURATOR_EVIDENCE_V1","contributions":[
              {"logicalRole":"EVIDENCE_ANALYST","summary":"ok","findings":[{"type":"FACT","statement":"s","confidence":0.5,"citationSourceIds":["src_1"]}]},
              {"logicalRole":"SOURCE_VERIFIER","summary":"ok","findings":[{"type":"GAP","statement":"gap","confidence":0.9,"citationSourceIds":[]}]}
            ]}
            """;
        var stage = WeddingPlannerCuratorValidation.CanonicalizeStageOutput(
            good, WeddingPlannerCuratorWorkerProfiles.EvidenceV1, known);
        Assert.Equal(2, stage.Contributions.Count);

        var dangling = """
            {"schemaVersion":"curator-worker-output.v1","workerProfileVersion":"CURATOR_EVIDENCE_V1","contributions":[
              {"logicalRole":"EVIDENCE_ANALYST","summary":"ok","findings":[{"type":"FACT","statement":"s","confidence":0.5,"citationSourceIds":["missing"]}]},
              {"logicalRole":"SOURCE_VERIFIER","summary":"ok","findings":[{"type":"GAP","statement":"gap","confidence":0.9,"citationSourceIds":[]}]}
            ]}
            """;
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCuratorValidation.CanonicalizeStageOutput(
                dangling, WeddingPlannerCuratorWorkerProfiles.EvidenceV1, known));

        var extraRole = """
            {"schemaVersion":"curator-worker-output.v1","workerProfileVersion":"CURATOR_EVIDENCE_V1","contributions":[
              {"logicalRole":"EVIDENCE_ANALYST","summary":"ok","findings":[{"type":"FACT","statement":"s","confidence":0.5,"citationSourceIds":["src_1"]}]},
              {"logicalRole":"SOURCE_VERIFIER","summary":"ok","findings":[{"type":"GAP","statement":"gap","confidence":0.9,"citationSourceIds":[]}]},
              {"logicalRole":"RESEARCH_SYNTHESIZER","summary":"extra","findings":[{"type":"GAP","statement":"x","confidence":0.1,"citationSourceIds":[]}]}
            ]}
            """;
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCuratorValidation.CanonicalizeStageOutput(
                extraRole, WeddingPlannerCuratorWorkerProfiles.EvidenceV1, known));
    }

    [Fact]
    public void Fact_without_citation_and_confidence_out_of_range_fail()
    {
        var known = new HashSet<string>(StringComparer.Ordinal) { "src_1" };
        var noCite = """
            {"schemaVersion":"curator-worker-output.v1","workerProfileVersion":"CURATOR_EVIDENCE_V1","contributions":[
              {"logicalRole":"EVIDENCE_ANALYST","summary":"ok","findings":[{"type":"FACT","statement":"s","confidence":0.5,"citationSourceIds":[]}]},
              {"logicalRole":"SOURCE_VERIFIER","summary":"ok","findings":[{"type":"GAP","statement":"gap","confidence":0.9,"citationSourceIds":[]}]}
            ]}
            """;
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCuratorValidation.CanonicalizeStageOutput(
                noCite, WeddingPlannerCuratorWorkerProfiles.EvidenceV1, known));

        var badConfidence = """
            {"schemaVersion":"curator-worker-output.v1","workerProfileVersion":"CURATOR_EVIDENCE_V1","contributions":[
              {"logicalRole":"EVIDENCE_ANALYST","summary":"ok","findings":[{"type":"FACT","statement":"s","confidence":1.5,"citationSourceIds":["src_1"]}]},
              {"logicalRole":"SOURCE_VERIFIER","summary":"ok","findings":[{"type":"GAP","statement":"gap","confidence":0.9,"citationSourceIds":[]}]}
            ]}
            """;
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCuratorValidation.CanonicalizeStageOutput(
                badConfidence, WeddingPlannerCuratorWorkerProfiles.EvidenceV1, known));
    }

    [Fact]
    public void Merge_requires_exactly_eight_contributions_and_exact_disclaimer()
    {
        var brief = WeddingPlannerCuratorValidation.CanonicalizeBrief(
            "Topic",
            "Objective",
            ["Q1"],
            "Manila",
            "en",
            Array.Empty<string>(),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            null);
        var catalog = WeddingPlannerCuratorValidation.CanonicalizeSourceCatalog(
            """
            {"schemaVersion":"research-source-catalog.v1","sources":[{"id":"src_1","title":"SYNTHETIC","url":"https://a.example.invalid/x","publisher":"SYNTHETIC","retrievedAt":"2026-01-01T00:00:00Z","synthetic":true}]}
            """,
            Array.Empty<string>());

        var contributions = WeddingPlannerCuratorLogicalRoles.AllInOrder
            .Select(role => new CanonicalContribution(
                role,
                "summary",
                [new CanonicalFinding(WeddingPlannerFindingTypes.Gap, "gap", 0.5, Array.Empty<string>())]))
            .ToList();

        var report = WeddingPlannerCuratorValidation.MergeAndCanonicalizeReport(
            brief,
            catalog,
            contributions,
            Guid.NewGuid(),
            brief.ApprovedBrandDnaVersionId,
            1,
            null,
            "Executive",
            ["Open?"],
            ["Risk"]);
        Assert.Contains(WeddingPlannerResearchDisclaimer.Text, report.DocumentJson, StringComparison.Ordinal);
        Assert.Equal(8, report.Contributions.Count);

        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCuratorValidation.MergeAndCanonicalizeReport(
                brief,
                catalog,
                contributions.Take(7).ToList(),
                Guid.NewGuid(),
                brief.ApprovedBrandDnaVersionId,
                1,
                null,
                "Executive",
                ["Open?"],
                ["Risk"]));
    }

    [Fact]
    public void Workforce_mapping_is_exactly_eight_roles_and_three_profiles()
    {
        Assert.Equal(8, WeddingPlannerCuratorLogicalRoles.AllInOrder.Count);
        Assert.Equal(3, WeddingPlannerCuratorWorkerProfiles.All.Count);
        Assert.Equal(4, WeddingPlannerCuratorWorkerProfiles.AssignedRoles(WeddingPlannerCuratorWorkerProfiles.ResearchV1).Count);
        Assert.Equal(2, WeddingPlannerCuratorWorkerProfiles.AssignedRoles(WeddingPlannerCuratorWorkerProfiles.EvidenceV1).Count);
        Assert.Equal(2, WeddingPlannerCuratorWorkerProfiles.AssignedRoles(WeddingPlannerCuratorWorkerProfiles.SynthesisRiskV1).Count);
        Assert.Equal(105, WeddingPlannerCuratorIdempotency.MaxJobIdempotencyKeyLength);
        Assert.Equal(23, WeddingPlannerCuratorIdempotency.LongestSuffixLength);
    }
}
