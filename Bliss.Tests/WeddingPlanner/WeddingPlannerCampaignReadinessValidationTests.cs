using System.Text.Json.Nodes;
using Bliss.Domain.WeddingPlanner;

namespace Bliss.Tests.WeddingPlanner;

public sealed class WeddingPlannerCampaignReadinessValidationTests
{
    [Fact]
    public void Rejects_forbidden_and_unknown_commit_fields()
    {
        var forbidden = JsonNode.Parse("""{"blissMatchId":"11111111-1111-1111-1111-111111111111","campaignId":"22222222-2222-2222-2222-222222222222","contentItemId":"33333333-3333-3333-3333-333333333333","adInventorySlotId":"44444444-4444-4444-4444-444444444444","rationale":"ok","disclaimerAcknowledged":true,"sourceSystem":"TEST","idempotencyKey":"k1","qaReviewReportVersionId":"55555555-5555-5555-5555-555555555555"}""")!;
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCampaignReadinessValidation.RejectForbiddenCommitFields(forbidden));
        Assert.Contains("Forbidden", ex.Message, StringComparison.OrdinalIgnoreCase);

        var unknown = JsonNode.Parse("""{"blissMatchId":"11111111-1111-1111-1111-111111111111","campaignId":"22222222-2222-2222-2222-222222222222","contentItemId":"33333333-3333-3333-3333-333333333333","adInventorySlotId":"44444444-4444-4444-4444-444444444444","rationale":"ok","disclaimerAcknowledged":true,"sourceSystem":"TEST","idempotencyKey":"k1","extraField":true}""")!;
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCampaignReadinessValidation.RejectForbiddenCommitFields(unknown));
    }

    [Fact]
    public void Derived_keys_must_fit_128()
    {
        var longKey = new string('k', 120);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCampaignReadinessValidation.NormalizeCommitKeys("TEST", longKey));
        Assert.Contains("derived keys", ex.Message, StringComparison.OrdinalIgnoreCase);

        var ok = WeddingPlannerCampaignReadinessValidation.NormalizeCommitKeys("ops", "cr-1");
        Assert.Equal("OPS", ok.SourceSystem);
        Assert.Equal("cr-1:PLACEMENT", ok.PlacementKey);
        Assert.Equal("cr-1:MARK", ok.MarkKey);
    }

    [Fact]
    public void Canonical_document_pins_full_selected_graph_and_validates()
    {
        var qaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var qaSha = new string('a', 64);
        var creatorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var opportunityId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var ruleVersionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var placementId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var runId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        var rules = CreateMinimalPassRules();
        var json = WeddingPlannerCampaignReadinessValidation.BuildCanonicalHandshakeDocument(
            rules,
            qaId,
            qaSha,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('b', 64),
            Guid.NewGuid(),
            "variant_1",
            Guid.NewGuid(),
            new string('c', 64),
            10,
            8,
            8,
            Guid.NewGuid(),
            "concept_1",
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            creatorId,
            opportunityId,
            ruleVersionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            placementId,
            runId,
            "ACCEPTED",
            "APPROVED",
            "APPROVED",
            0.9m,
            "ACTIVE",
            "DRAFT",
            "Title",
            "VIDEO",
            "PRE_ROLL",
            0,
            15,
            true,
            "Creator Label",
            "Opportunity Name",
            "rationale",
            true,
            true,
            true);

        Assert.Contains(WeddingPlannerCampaignReadinessHandshakeDisclaimer.Text, json, StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerCampaignReadinessMarkers.SyntheticDevelopmentCampaignReadiness, json, StringComparison.Ordinal);
        Assert.Contains("campaign-readiness-handshake.v1", json, StringComparison.Ordinal);

        var node = JsonNode.Parse(json)!.AsObject();
        var pins = node["pins"]!.AsObject();
        Assert.Equal(qaId.ToString(), pins["qaReviewReportVersionId"]!.GetValue<string>());
        Assert.Equal(qaSha, pins["qaReviewReportDocumentSha256"]!.GetValue<string>());
        Assert.Equal(creatorId.ToString(), pins["creatorId"]!.GetValue<string>());
        Assert.Equal(opportunityId.ToString(), pins["advertiserOpportunityId"]!.GetValue<string>());
        Assert.Equal(ruleVersionId.ToString(), pins["ruleVersionId"]!.GetValue<string>());
        Assert.Equal(placementId.ToString(), node["campaignPlacementId"]!.GetValue<string>());
        Assert.Equal(runId.ToString(), node["campaignPlacementRunId"]!.GetValue<string>());

        var snapshots = node["snapshots"]!.AsObject();
        Assert.Equal("VIDEO", snapshots["contentType"]!.GetValue<string>());
        Assert.Equal(0, snapshots["slotStartSecond"]!.GetValue<int>());
        Assert.Equal(15, snapshots["slotDurationSeconds"]!.GetValue<int>());
        Assert.Equal("Creator Label", snapshots["creatorName"]!.GetValue<string>());
        Assert.Equal("Opportunity Name", snapshots["opportunityName"]!.GetValue<string>());
        Assert.DoesNotContain("url", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", json, StringComparison.OrdinalIgnoreCase);

        node["disclaimer"] = "altered";
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCampaignReadinessValidation.ValidateHandshakeDocument(node, requireSyntheticMarker: true));
    }

    [Fact]
    public void Validate_rejects_block_overall_severity_duplicate_codes_and_non_pass_findings()
    {
        var good = JsonNode.Parse(BuildCanonicalJson())!.AsObject();

        var blockedOverall = JsonNode.Parse(BuildCanonicalJson())!.AsObject();
        blockedOverall["rules"]!["overallSeverity"] = "BLOCK";
        var overallEx = Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCampaignReadinessValidation.ValidateHandshakeDocument(blockedOverall, true));
        Assert.Contains("overallSeverity must be PASS", overallEx.Message, StringComparison.Ordinal);

        var blockedFinding = JsonNode.Parse(BuildCanonicalJson())!.AsObject();
        blockedFinding["rules"]!["findings"]![0]!["severity"] = "BLOCK";
        var findingEx = Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCampaignReadinessValidation.ValidateHandshakeDocument(blockedFinding, true));
        Assert.Contains("must have severity PASS", findingEx.Message, StringComparison.Ordinal);

        var duplicate = JsonNode.Parse(BuildCanonicalJson())!.AsObject();
        var findings = duplicate["rules"]!["findings"]!.AsArray();
        findings.RemoveAt(findings.Count - 1);
        findings.Add(new JsonObject
        {
            ["code"] = WeddingPlannerCampaignReadinessRuleCodes.All[0],
            ["severity"] = WeddingPlannerCampaignReadinessFindingSeverities.Pass,
            ["message"] = "dup"
        });
        var dupEx = Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCampaignReadinessValidation.ValidateHandshakeDocument(duplicate, true));
        Assert.Contains("duplicate code", dupEx.Message, StringComparison.Ordinal);

        var missingPins = JsonNode.Parse(BuildCanonicalJson())!.AsObject();
        missingPins.Remove("pins");
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCampaignReadinessValidation.ValidateHandshakeDocument(missingPins, true));

        var missingDisclosures = JsonNode.Parse(BuildCanonicalJson())!.AsObject();
        missingDisclosures.Remove("disclosures");
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCampaignReadinessValidation.ValidateHandshakeDocument(missingDisclosures, true));

        // Sanity: unmodified good document still validates.
        WeddingPlannerCampaignReadinessValidation.ValidateHandshakeDocument(good, true);
    }

    [Fact]
    public void Revoke_rejects_non_revoke_decision()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCampaignReadinessValidation.ValidateRevokeDecision(
                WeddingPlannerCampaignReadinessDecisions.MarkCampaignReady,
                "nope"));
        Assert.Contains("REVOKE_CAMPAIGN_READY", ex.Message, StringComparison.Ordinal);
    }

    private static string BuildCanonicalJson() =>
        WeddingPlannerCampaignReadinessValidation.BuildCanonicalHandshakeDocument(
            CreateMinimalPassRules(),
            Guid.NewGuid(),
            new string('a', 64),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('b', 64),
            Guid.NewGuid(),
            "variant_1",
            Guid.NewGuid(),
            new string('c', 64),
            10,
            8,
            8,
            Guid.NewGuid(),
            "concept_1",
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "ACCEPTED",
            "APPROVED",
            "APPROVED",
            0.9m,
            "ACTIVE",
            "DRAFT",
            "Title",
            "VIDEO",
            "PRE_ROLL",
            0,
            15,
            true,
            "Creator",
            "Opp",
            "rationale",
            true,
            true,
            true);

    private static CanonicalCampaignReadinessRulesFindings CreateMinimalPassRules()
    {
        var findings = WeddingPlannerCampaignReadinessRuleCodes.All
            .Select(c => new CanonicalCampaignReadinessFinding(
                c,
                WeddingPlannerCampaignReadinessFindingSeverities.Pass,
                "ok"))
            .ToList();
        var document = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.CampaignReadinessRulesV1,
            ["overallSeverity"] = WeddingPlannerCampaignReadinessFindingSeverities.Pass,
            ["findings"] = new JsonArray(findings.Select(f => (JsonNode)new JsonObject
            {
                ["code"] = f.Code,
                ["severity"] = f.Severity,
                ["message"] = f.Message
            }).ToArray())
        };
        return new CanonicalCampaignReadinessRulesFindings(
            WeddingPlannerCampaignReadinessFindingSeverities.Pass,
            findings,
            document.ToJsonString());
    }
}
