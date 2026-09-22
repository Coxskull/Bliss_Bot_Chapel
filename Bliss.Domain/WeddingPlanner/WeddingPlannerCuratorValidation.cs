using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Strict validators/canonicalizers for Curator research briefs, citation URLs,
/// source catalogs, stage outputs, and merged research reports.
/// Never performs DNS resolution or network I/O.
/// </summary>
public static class WeddingPlannerCuratorValidation
{
    public const int MaxTopicLength = 200;
    public const int MaxObjectiveLength = 4000;
    public const int MinQuestions = 1;
    public const int MaxQuestions = 8;
    public const int MaxQuestionLength = 500;
    public const int MaxGeographyLength = 200;
    public const int MaxLanguageLength = 32;
    public const int MaxAllowedDomains = 10;
    public const int MaxHostnameLength = 253;
    public const int MaxUrlLength = 2048;
    public const int MaxSources = 25;
    public const int MaxSourceIdLength = 64;
    public const int MaxSourceTitleLength = 300;
    public const int MaxPublisherLength = 200;
    public const int MaxFindingsPerRole = 20;
    public const int MaxFindingStatementLength = 2000;
    public const int MaxContributionSummaryLength = 2000;
    public const int MaxExecutiveSummaryLength = 4000;
    public const int MaxOpenQuestions = 12;
    public const int MaxRisks = 12;
    public const int MaxCitationsPerFinding = 10;
    public const int MaxRemoteResponseBytes = 262_144;

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private static readonly JsonSerializerOptions NodeWriteOptions = new()
    {
        WriteIndented = false,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private static readonly HashSet<string> FindingTypeSet = new(StringComparer.Ordinal)
    {
        WeddingPlannerFindingTypes.Fact,
        WeddingPlannerFindingTypes.Inference,
        WeddingPlannerFindingTypes.Gap,
        WeddingPlannerFindingTypes.Risk
    };

    public static CanonicalResearchBrief CanonicalizeBrief(
        string topic,
        string objective,
        IReadOnlyList<string> questions,
        string geography,
        string language,
        IReadOnlyList<string>? allowedDomains,
        Guid approvedBrandDnaVersionId,
        Guid? approvedColorProfileVersionId)
    {
        var canonicalTopic = Required(topic, "Topic", MaxTopicLength);
        var canonicalObjective = Required(objective, "Objective", MaxObjectiveLength);
        var canonicalGeography = Required(geography, "Geography", MaxGeographyLength);
        var canonicalLanguage = Required(language, "Language", MaxLanguageLength);

        if (questions is null || questions.Count < MinQuestions || questions.Count > MaxQuestions)
        {
            throw new InvalidOperationException(
                $"Questions must contain between {MinQuestions} and {MaxQuestions} non-empty items.");
        }

        var canonicalQuestions = new List<string>(questions.Count);
        foreach (var question in questions)
        {
            canonicalQuestions.Add(Required(question, "Questions item", MaxQuestionLength));
        }

        var domains = NormalizeAllowedDomains(allowedDomains);

        var payload = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.ResearchBriefV1,
            ["topic"] = canonicalTopic,
            ["objective"] = canonicalObjective,
            ["questions"] = ToJsonArray(canonicalQuestions),
            ["geography"] = canonicalGeography,
            ["language"] = canonicalLanguage,
            ["allowedDomains"] = ToJsonArray(domains),
            ["approvedBrandDnaVersionId"] = approvedBrandDnaVersionId.ToString("D"),
            ["researchProviderContractVersion"] = WeddingPlannerResearchContractVersions.ResearchProviderV1,
            ["curatorOrchestrationVersion"] = WeddingPlannerResearchContractVersions.CuratorOrchestrationV1
        };

        if (approvedColorProfileVersionId is Guid colorId)
        {
            payload["approvedColorProfileVersionId"] = colorId.ToString("D");
        }

        var ordered = OrderObjectKeys(payload);
        var json = ordered.ToJsonString(NodeWriteOptions);
        var sha = Sha256Hex(json);

        return new CanonicalResearchBrief(
            Topic: canonicalTopic,
            Objective: canonicalObjective,
            Questions: canonicalQuestions,
            Geography: canonicalGeography,
            Language: canonicalLanguage,
            AllowedDomains: domains,
            ApprovedBrandDnaVersionId: approvedBrandDnaVersionId,
            ApprovedColorProfileVersionId: approvedColorProfileVersionId,
            InputJson: json,
            InputSha256: sha);
    }

    public static IReadOnlyList<string> NormalizeAllowedDomains(IReadOnlyList<string>? allowedDomains)
    {
        if (allowedDomains is null || allowedDomains.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (allowedDomains.Count > MaxAllowedDomains)
        {
            throw new InvalidOperationException($"AllowedDomains cannot exceed {MaxAllowedDomains} entries.");
        }

        var normalized = new List<string>(allowedDomains.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in allowedDomains)
        {
            var host = NormalizeAllowedDomainEntry(raw);
            if (!seen.Add(host))
            {
                throw new InvalidOperationException($"AllowedDomains contains duplicate host '{host}'.");
            }

            normalized.Add(host);
        }

        normalized.Sort(StringComparer.Ordinal);
        return normalized;
    }

    public static string NormalizeAllowedDomainEntry(string? raw)
    {
        var value = raw?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("AllowedDomains entries must be non-empty hostnames.");
        }

        if (value.Contains("://", StringComparison.Ordinal)
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('?', StringComparison.Ordinal)
            || value.Contains('#', StringComparison.Ordinal)
            || value.Contains('@', StringComparison.Ordinal)
            || value.Contains(':', StringComparison.Ordinal)
            || value.Contains('*', StringComparison.Ordinal)
            || value.Contains(' ', StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "AllowedDomains entries must be exact hostnames without scheme, path, port, wildcard, or userinfo.");
        }

        if (IPAddress.TryParse(value, out _))
        {
            throw new InvalidOperationException("AllowedDomains entries must not be IP literals.");
        }

        ValidateHostnameLabels(value, allowLocalhost: false);
        return value;
    }

    /// <summary>
    /// Validates a citation/catalog URL without DNS or network I/O. Uses literal IP range checks only.
    /// </summary>
    public static string ValidateCitationUrl(string? rawUrl, IReadOnlyList<string> allowedDomains)
    {
        var url = rawUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Citation URL is required.");
        }

        if (url.Length > MaxUrlLength)
        {
            throw new InvalidOperationException($"Citation URL cannot exceed {MaxUrlLength} characters.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Citation URL must be an absolute http or https URI.");
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Citation URL scheme must be http or https.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException("Citation URL must not include userinfo.");
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException("Citation URL must not include a fragment.");
        }

        var host = uri.IdnHost;
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("Citation URL host is required.");
        }

        if (host.Length > MaxHostnameLength)
        {
            throw new InvalidOperationException($"Citation URL host cannot exceed {MaxHostnameLength} characters.");
        }

        host = host.TrimEnd('.').ToLowerInvariant();
        RejectUnsafeHost(host);

        if (allowedDomains.Count > 0 && !IsHostAllowed(host, allowedDomains))
        {
            throw new InvalidOperationException(
                $"Citation URL host '{host}' is not covered by AllowedDomains.");
        }

        // Rebuild without fragment (already rejected) — keep original absolute form normalized lowercase host via Uri.
        return uri.GetComponents(
            UriComponents.SchemeAndServer | UriComponents.PathAndQuery,
            UriFormat.UriEscaped);
    }

    public static bool IsHostAllowed(string host, IReadOnlyList<string> allowedDomains)
    {
        foreach (var allowed in allowedDomains)
        {
            if (string.Equals(host, allowed, StringComparison.Ordinal))
            {
                return true;
            }

            // Exact label-boundary subdomain match only.
            if (host.Length > allowed.Length + 1
                && host.EndsWith("." + allowed, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static CanonicalSourceCatalog CanonicalizeSourceCatalog(
        string? catalogJson,
        IReadOnlyList<string> allowedDomains)
    {
        if (string.IsNullOrWhiteSpace(catalogJson))
        {
            throw new InvalidOperationException("Source catalog JSON is required.");
        }

        if (Encoding.UTF8.GetByteCount(catalogJson) > MaxRemoteResponseBytes)
        {
            throw new InvalidOperationException("Source catalog payload exceeds the maximum allowed size.");
        }

        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(catalogJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Source catalog JSON is invalid.", ex);
        }

        if (rootNode is not JsonObject root)
        {
            throw new InvalidOperationException("Source catalog must be a JSON object.");
        }

        AssertOnlyKnownProperties(root, "schemaVersion", "sources");

        var schema = root["schemaVersion"]?.GetValue<string>();
        if (!string.Equals(schema, WeddingPlannerSchemaVersions.ResearchSourceCatalogV1, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Source catalog schemaVersion must be {WeddingPlannerSchemaVersions.ResearchSourceCatalogV1}.");
        }

        if (root["sources"] is not JsonArray sourcesArr)
        {
            throw new InvalidOperationException("Source catalog sources must be an array.");
        }

        if (sourcesArr.Count == 0 || sourcesArr.Count > MaxSources)
        {
            throw new InvalidOperationException(
                $"Source catalog must contain between 1 and {MaxSources} sources.");
        }

        var sources = new List<CanonicalSource>(sourcesArr.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in sourcesArr)
        {
            if (item is not JsonObject sourceObj)
            {
                throw new InvalidOperationException("Each source catalog entry must be an object.");
            }

            AssertOnlyKnownProperties(sourceObj, "id", "title", "url", "publisher", "retrievedAt", "synthetic");

            var id = Required(sourceObj["id"]?.GetValue<string>(), "source.id", MaxSourceIdLength);
            if (!ids.Add(id))
            {
                throw new InvalidOperationException($"Duplicate source id '{id}'.");
            }

            var title = Required(sourceObj["title"]?.GetValue<string>(), "source.title", MaxSourceTitleLength);
            var url = ValidateCitationUrl(sourceObj["url"]?.GetValue<string>(), allowedDomains);
            var publisher = Required(sourceObj["publisher"]?.GetValue<string>(), "source.publisher", MaxPublisherLength);
            var retrievedAtRaw = Required(sourceObj["retrievedAt"]?.GetValue<string>(), "source.retrievedAt", 64);
            if (!DateTimeOffset.TryParse(
                    retrievedAtRaw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var retrievedAt))
            {
                throw new InvalidOperationException($"source.retrievedAt '{retrievedAtRaw}' is not a valid timestamp.");
            }

            if (sourceObj["synthetic"] is null || sourceObj["synthetic"]!.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new InvalidOperationException("source.synthetic must be a boolean.");
            }

            var synthetic = sourceObj["synthetic"]!.GetValue<bool>();
            sources.Add(new CanonicalSource(
                id,
                title,
                url,
                publisher,
                retrievedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                synthetic));
        }

        var ordered = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.ResearchSourceCatalogV1,
            ["sources"] = new JsonArray(sources.Select(s => (JsonNode)new JsonObject
            {
                ["id"] = s.Id,
                ["title"] = s.Title,
                ["url"] = s.Url,
                ["publisher"] = s.Publisher,
                ["retrievedAt"] = s.RetrievedAt,
                ["synthetic"] = s.Synthetic
            }).ToArray())
        };

        var json = ordered.ToJsonString(NodeWriteOptions);
        return new CanonicalSourceCatalog(json, sources);
    }

    public static CanonicalStageOutput CanonicalizeStageOutput(
        string? stageJson,
        string expectedWorkerProfileVersion,
        IReadOnlySet<string> knownSourceIds)
    {
        if (string.IsNullOrWhiteSpace(stageJson))
        {
            throw new InvalidOperationException("Curator stage output JSON is required.");
        }

        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(stageJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Curator stage output JSON is invalid.", ex);
        }

        if (rootNode is not JsonObject root)
        {
            throw new InvalidOperationException("Curator stage output must be a JSON object.");
        }

        AssertOnlyKnownProperties(root, "schemaVersion", "workerProfileVersion", "contributions");

        var schema = root["schemaVersion"]?.GetValue<string>();
        if (!string.Equals(schema, WeddingPlannerSchemaVersions.CuratorWorkerOutputV1, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Stage schemaVersion must be {WeddingPlannerSchemaVersions.CuratorWorkerOutputV1}.");
        }

        var profile = root["workerProfileVersion"]?.GetValue<string>();
        if (!string.Equals(profile, expectedWorkerProfileVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Stage workerProfileVersion must be {expectedWorkerProfileVersion}.");
        }

        var expectedRoles = WeddingPlannerCuratorWorkerProfiles.AssignedRoles(expectedWorkerProfileVersion);
        if (root["contributions"] is not JsonArray contributionsArr)
        {
            throw new InvalidOperationException("Stage contributions must be an array.");
        }

        if (contributionsArr.Count != expectedRoles.Count)
        {
            throw new InvalidOperationException(
                $"Stage must contain exactly {expectedRoles.Count} contributions for {expectedWorkerProfileVersion}.");
        }

        var contributions = new List<CanonicalContribution>(contributionsArr.Count);
        var seenRoles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in contributionsArr)
        {
            contributions.Add(ParseContribution(item, knownSourceIds, seenRoles));
        }

        foreach (var expected in expectedRoles)
        {
            if (!seenRoles.Contains(expected))
            {
                throw new InvalidOperationException(
                    $"Stage output is missing required logicalRole '{expected}'.");
            }
        }

        foreach (var role in seenRoles)
        {
            if (!expectedRoles.Contains(role))
            {
                throw new InvalidOperationException(
                    $"Stage output contains unexpected logicalRole '{role}'.");
            }
        }

        // Order contributions to match assigned role order.
        var orderedContributions = expectedRoles
            .Select(role => contributions.Single(c => c.LogicalRole == role))
            .ToList();

        var ordered = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.CuratorWorkerOutputV1,
            ["workerProfileVersion"] = expectedWorkerProfileVersion,
            ["contributions"] = new JsonArray(orderedContributions.Select(ToContributionNode).ToArray())
        };

        var json = ordered.ToJsonString(NodeWriteOptions);
        return new CanonicalStageOutput(json, expectedWorkerProfileVersion, orderedContributions);
    }

    public static CanonicalResearchReport MergeAndCanonicalizeReport(
        CanonicalResearchBrief brief,
        CanonicalSourceCatalog catalog,
        IReadOnlyList<CanonicalContribution> allContributions,
        Guid researchJobId,
        Guid approvedBrandDnaVersionId,
        int approvedBrandDnaVersionNumber,
        Guid? approvedColorProfileVersionId,
        string executiveSummary,
        IReadOnlyList<string> openQuestions,
        IReadOnlyList<string> risks)
    {
        if (allContributions.Count != WeddingPlannerCuratorLogicalRoles.AllInOrder.Count)
        {
            throw new InvalidOperationException("Merged research report requires exactly 8 role contributions.");
        }

        var byRole = allContributions.ToDictionary(x => x.LogicalRole, StringComparer.Ordinal);
        var ordered = new List<CanonicalContribution>(8);
        foreach (var role in WeddingPlannerCuratorLogicalRoles.AllInOrder)
        {
            if (!byRole.TryGetValue(role, out var contribution))
            {
                throw new InvalidOperationException($"Merged report is missing contribution for '{role}'.");
            }

            ordered.Add(contribution);
        }

        if (byRole.Count != 8)
        {
            throw new InvalidOperationException("Merged research report contains unexpected role contributions.");
        }

        var knownIds = catalog.Sources.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var contribution in ordered)
        {
            ValidateContributionCitations(contribution, knownIds);
        }

        var summaryText = Required(executiveSummary, "synthesis.executiveSummary", MaxExecutiveSummaryLength);
        var openQs = NormalizeStringList(openQuestions, "synthesis.openQuestions", MaxOpenQuestions, MaxQuestionLength);
        var riskList = NormalizeStringList(risks, "synthesis.risks", MaxRisks, MaxFindingStatementLength);

        var document = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.ResearchReportV1,
            ["disclaimer"] = WeddingPlannerResearchDisclaimer.Text,
            ["provenance"] = new JsonObject
            {
                ["approvedBrandDnaVersionId"] = approvedBrandDnaVersionId.ToString("D"),
                ["approvedBrandDnaVersionNumber"] = approvedBrandDnaVersionNumber,
                ["approvedColorProfileVersionId"] = approvedColorProfileVersionId is Guid cid
                    ? cid.ToString("D")
                    : null,
                ["researchJobId"] = researchJobId.ToString("D")
            },
            ["brief"] = new JsonObject
            {
                ["topic"] = brief.Topic,
                ["objective"] = brief.Objective,
                ["questions"] = ToJsonArray(brief.Questions),
                ["geography"] = brief.Geography,
                ["language"] = brief.Language,
                ["allowedDomains"] = ToJsonArray(brief.AllowedDomains)
            },
            ["sources"] = new JsonArray(catalog.Sources.Select(s => (JsonNode)new JsonObject
            {
                ["id"] = s.Id,
                ["title"] = s.Title,
                ["url"] = s.Url,
                ["publisher"] = s.Publisher,
                ["retrievedAt"] = s.RetrievedAt,
                ["synthetic"] = s.Synthetic
            }).ToArray()),
            ["contributions"] = new JsonArray(ordered.Select(ToContributionNode).ToArray()),
            ["synthesis"] = new JsonObject
            {
                ["executiveSummary"] = summaryText,
                ["openQuestions"] = ToJsonArray(openQs),
                ["risks"] = ToJsonArray(riskList)
            }
        };

        // Ensure null color id is preserved as JSON null (not omitted) per contract example.
        var provenance = (JsonObject)document["provenance"]!;
        if (approvedColorProfileVersionId is null)
        {
            provenance["approvedColorProfileVersionId"] = null;
        }

        var json = document.ToJsonString(NodeWriteOptions);
        if (!json.Contains(WeddingPlannerResearchDisclaimer.Text, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Merged research report must include the exact research disclaimer.");
        }

        var summary = Truncate(
            $"{WeddingPlannerSchemaVersions.ResearchReportV1}: {brief.Topic} — {ordered.Count} contributions, {catalog.Sources.Count} sources.",
            2000);

        return new CanonicalResearchReport(json, summary, ordered);
    }

    public static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string SerializeAssignedRolesJson(IReadOnlyList<string> roles) =>
        JsonSerializer.Serialize(roles, CanonicalJsonOptions);

    private static CanonicalContribution ParseContribution(
        JsonNode? item,
        IReadOnlySet<string> knownSourceIds,
        HashSet<string> seenRoles)
    {
        if (item is not JsonObject obj)
        {
            throw new InvalidOperationException("Each contribution must be an object.");
        }

        AssertOnlyKnownProperties(obj, "logicalRole", "summary", "findings");
        var role = Required(obj["logicalRole"]?.GetValue<string>(), "logicalRole", 64);
        if (!WeddingPlannerCuratorLogicalRoles.AllInOrder.Contains(role))
        {
            throw new InvalidOperationException($"Unknown logicalRole '{role}'.");
        }

        if (!seenRoles.Add(role))
        {
            throw new InvalidOperationException($"Duplicate logicalRole '{role}'.");
        }

        var summary = Required(obj["summary"]?.GetValue<string>(), "contribution.summary", MaxContributionSummaryLength);
        if (obj["findings"] is not JsonArray findingsArr || findingsArr.Count == 0)
        {
            throw new InvalidOperationException($"Contribution '{role}' must include at least one finding.");
        }

        if (findingsArr.Count > MaxFindingsPerRole)
        {
            throw new InvalidOperationException(
                $"Contribution '{role}' cannot exceed {MaxFindingsPerRole} findings.");
        }

        var findings = new List<CanonicalFinding>(findingsArr.Count);
        foreach (var findingNode in findingsArr)
        {
            findings.Add(ParseFinding(findingNode, knownSourceIds));
        }

        return new CanonicalContribution(role, summary, findings);
    }

    private static CanonicalFinding ParseFinding(JsonNode? node, IReadOnlySet<string> knownSourceIds)
    {
        if (node is not JsonObject obj)
        {
            throw new InvalidOperationException("Each finding must be an object.");
        }

        AssertOnlyKnownProperties(obj, "type", "statement", "confidence", "citationSourceIds");
        var type = Required(obj["type"]?.GetValue<string>(), "finding.type", 32);
        if (!FindingTypeSet.Contains(type))
        {
            throw new InvalidOperationException($"Unknown finding type '{type}'.");
        }

        var statement = Required(obj["statement"]?.GetValue<string>(), "finding.statement", MaxFindingStatementLength);
        if (obj["confidence"] is null || obj["confidence"]!.GetValueKind() != JsonValueKind.Number)
        {
            throw new InvalidOperationException("finding.confidence must be a number.");
        }

        var confidence = obj["confidence"]!.GetValue<double>();
        if (double.IsNaN(confidence) || double.IsInfinity(confidence) || confidence < 0 || confidence > 1)
        {
            throw new InvalidOperationException("finding.confidence must be in [0, 1].");
        }

        var citationIds = new List<string>();
        if (obj["citationSourceIds"] is JsonArray citations)
        {
            if (citations.Count > MaxCitationsPerFinding)
            {
                throw new InvalidOperationException(
                    $"finding.citationSourceIds cannot exceed {MaxCitationsPerFinding} entries.");
            }

            foreach (var citation in citations)
            {
                var id = Required(citation?.GetValue<string>(), "citationSourceId", MaxSourceIdLength);
                if (!knownSourceIds.Contains(id))
                {
                    throw new InvalidOperationException($"Unknown or dangling citationSourceId '{id}'.");
                }

                citationIds.Add(id);
            }
        }
        else if (obj["citationSourceIds"] is not null)
        {
            throw new InvalidOperationException("finding.citationSourceIds must be an array when present.");
        }

        if (type is WeddingPlannerFindingTypes.Fact
                or WeddingPlannerFindingTypes.Inference
                or WeddingPlannerFindingTypes.Risk
            && citationIds.Count == 0)
        {
            throw new InvalidOperationException(
                $"Finding type {type} requires one or more known citationSourceIds.");
        }

        return new CanonicalFinding(type, statement, confidence, citationIds);
    }

    private static void ValidateContributionCitations(
        CanonicalContribution contribution,
        IReadOnlySet<string> knownSourceIds)
    {
        foreach (var finding in contribution.Findings)
        {
            foreach (var id in finding.CitationSourceIds)
            {
                if (!knownSourceIds.Contains(id))
                {
                    throw new InvalidOperationException($"Unknown or dangling citationSourceId '{id}'.");
                }
            }
        }
    }

    private static JsonObject ToContributionNode(CanonicalContribution contribution) =>
        new()
        {
            ["logicalRole"] = contribution.LogicalRole,
            ["summary"] = contribution.Summary,
            ["findings"] = new JsonArray(contribution.Findings.Select(f => (JsonNode)new JsonObject
            {
                ["type"] = f.Type,
                ["statement"] = f.Statement,
                ["confidence"] = f.Confidence,
                ["citationSourceIds"] = ToJsonArray(f.CitationSourceIds)
            }).ToArray())
        };

    private static void RejectUnsafeHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.Ordinal)
            || host.EndsWith(".localhost", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Citation URL host must not be localhost.");
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            if (IsBlockedIp(ip))
            {
                throw new InvalidOperationException("Citation URL host must not be a private, loopback, link-local, or reserved address.");
            }

            return;
        }

        ValidateHostnameLabels(host, allowLocalhost: false);

        // Block obvious loopback DNS labels without resolving.
        if (host.Equals("localhost", StringComparison.Ordinal)
            || host.EndsWith(".localhost", StringComparison.Ordinal)
            || host.Equals("ip6-localhost", StringComparison.Ordinal)
            || host.Equals("ip6-loopback", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Citation URL host must not be a loopback label.");
        }
    }

    private static void ValidateHostnameLabels(string host, bool allowLocalhost)
    {
        if (host.Length is 0 or > MaxHostnameLength)
        {
            throw new InvalidOperationException("Hostname length is invalid.");
        }

        if (host.StartsWith(".", StringComparison.Ordinal) || host.EndsWith(".", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Hostname must not start or end with a dot.");
        }

        var labels = host.Split('.');
        if (labels.Length == 0)
        {
            throw new InvalidOperationException("Hostname is invalid.");
        }

        foreach (var label in labels)
        {
            if (label.Length is 0 or > 63)
            {
                throw new InvalidOperationException("Hostname label length is invalid.");
            }

            if (label.StartsWith('-') || label.EndsWith('-'))
            {
                throw new InvalidOperationException("Hostname labels must not start or end with a hyphen.");
            }

            foreach (var ch in label)
            {
                if (!char.IsAsciiLetterOrDigit(ch) && ch != '-')
                {
                    throw new InvalidOperationException("Hostname contains invalid characters.");
                }
            }
        }

        if (!allowLocalhost
            && (string.Equals(host, "localhost", StringComparison.Ordinal)
                || host.EndsWith(".localhost", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Hostname must not be localhost.");
        }
    }

    private static bool IsBlockedIp(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            // 0.0.0.0/8
            if (bytes[0] == 0) return true;
            // 10.0.0.0/8
            if (bytes[0] == 10) return true;
            // 127.0.0.0/8
            if (bytes[0] == 127) return true;
            // 169.254.0.0/16 (link-local + cloud metadata)
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            // 100.64.0.0/10 (CGNAT)
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) return true;
            // 224.0.0.0/4 multicast and 240.0.0.0/4 reserved
            if (bytes[0] >= 224) return true;
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                return IsBlockedIp(address.MapToIPv4());
            }

            var bytes = address.GetAddressBytes();
            // ::1 already covered by IsLoopback; fc00::/7 unique local
            if ((bytes[0] & 0xfe) == 0xfc) return true;
            // fe80::/10 link-local
            if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80) return true;
            // multicast ff00::/8
            if (bytes[0] == 0xff) return true;
            // unspecified ::
            if (address.Equals(IPAddress.IPv6Any)) return true;
            return false;
        }

        return true;
    }

    private static void AssertOnlyKnownProperties(JsonObject obj, params string[] allowed)
    {
        var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var property in obj)
        {
            if (!allowedSet.Contains(property.Key))
            {
                throw new InvalidOperationException($"Unknown JSON property '{property.Key}' is not allowed.");
            }
        }
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static JsonObject OrderObjectKeys(JsonObject source)
    {
        var ordered = new JsonObject();
        foreach (var property in source.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            ordered[property.Key] = property.Value is JsonObject child
                ? OrderObjectKeys(child)
                : property.Value?.DeepClone();
        }

        return ordered;
    }

    private static IReadOnlyList<string> NormalizeStringList(
        IReadOnlyList<string>? values,
        string name,
        int maxCount,
        int maxItemLength)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        if (values.Count > maxCount)
        {
            throw new InvalidOperationException($"{name} cannot exceed {maxCount} items.");
        }

        return values.Select(v => Required(v, name, maxItemLength)).ToList();
    }

    private static string Required(string? value, string name, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException($"{name} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"{name} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

public sealed record CanonicalResearchBrief(
    string Topic,
    string Objective,
    IReadOnlyList<string> Questions,
    string Geography,
    string Language,
    IReadOnlyList<string> AllowedDomains,
    Guid ApprovedBrandDnaVersionId,
    Guid? ApprovedColorProfileVersionId,
    string InputJson,
    string InputSha256);

public sealed record CanonicalSource(
    string Id,
    string Title,
    string Url,
    string Publisher,
    string RetrievedAt,
    bool Synthetic);

public sealed record CanonicalSourceCatalog(
    string CatalogJson,
    IReadOnlyList<CanonicalSource> Sources);

public sealed record CanonicalFinding(
    string Type,
    string Statement,
    double Confidence,
    IReadOnlyList<string> CitationSourceIds);

public sealed record CanonicalContribution(
    string LogicalRole,
    string Summary,
    IReadOnlyList<CanonicalFinding> Findings);

public sealed record CanonicalStageOutput(
    string OutputJson,
    string WorkerProfileVersion,
    IReadOnlyList<CanonicalContribution> Contributions);

public sealed record CanonicalResearchReport(
    string DocumentJson,
    string Summary,
    IReadOnlyList<CanonicalContribution> Contributions);
