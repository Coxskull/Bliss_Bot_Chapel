using System.Text.Json;

namespace Bliss.Api.Demonstrations;

public sealed class SourceClipRecord
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string FrameHash { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Culture { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }
    public string Status { get; set; } = string.Empty;
    public int QuotaCredit { get; set; }
    public string Provenance { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
}

public sealed class ConceptRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Subhead { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string CallToAction { get; set; } = string.Empty;
    public string VideoFileName { get; set; } = string.Empty;
    public string QrFileName { get; set; } = string.Empty;
    public string QrDestination { get; set; } = string.Empty;
    public Guid SourceClipId { get; set; }
    public string RecipeVersion { get; set; } = string.Empty;
    public string QaStatus { get; set; } = string.Empty;
    public string AccentHex { get; set; } = string.Empty;
}

public sealed class ChatRecord
{
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Signal { get; set; } = string.Empty;
    public string Gear { get; set; } = string.Empty;
    public DateTime At { get; set; }
}

public sealed class ContactRoadRecord
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool OutreachEligible { get; set; }
    public DateTime RecordedAt { get; set; }
}

public sealed class DemonstrationRecord
{
    public string Slug { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string Niche { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string BuyingRoles { get; set; } = string.Empty;
    public string DecisionMakerStatus { get; set; } = string.Empty;
    public string DecisionMakerName { get; set; } = string.Empty;
    public string DecisionMakerRole { get; set; } = string.Empty;
    public string EvidenceKind { get; set; } = string.Empty;
    public string EvidenceSourceUrl { get; set; } = string.Empty;
    public string CorroboratingKind { get; set; } = string.Empty;
    public string CorroboratingSourceUrl { get; set; } = string.Empty;
    public string ContactTier { get; set; } = string.Empty;
    public string ContactRoute { get; set; } = string.Empty;
    public string ContactType { get; set; } = string.Empty;
    public string ContactValue { get; set; } = string.Empty;
    public string ContactSourceUrl { get; set; } = string.Empty;
    public string ContactVerification { get; set; } = string.Empty;
    public string FreshnessStatus { get; set; } = "UNRECORDED";
    public DateTime? LastVerifiedAt { get; set; }
    public bool PersonalizationAllowed { get; set; }
    public string Disclosure { get; set; } = string.Empty;
    public string PublicSourceUrl { get; set; } = string.Empty;
    public int OpportunityScore { get; set; }
    public string ProspectState { get; set; } = string.Empty;
    public string BusinessIdentity { get; set; } = string.Empty;
    public bool Illustrative { get; set; }
    public bool HumanEscalation { get; set; }
    public string LastSignal { get; set; } = string.Empty;
    public bool Suppressed { get; set; }
    public string SuppressionReason { get; set; } = string.Empty;
    public List<ContactRoadRecord> ContactRoads { get; set; } = [];
    public List<ConceptRecord> Concepts { get; set; } = [];
    public List<ChatRecord> Messages { get; set; } = [];
    public List<AcquisitionEventRecord> Events { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public sealed class AcquisitionEventRecord
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Observation { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public int AiCalls { get; set; }
}

public sealed class FactoryBatchRecord
{
    public string Id { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ProspectCount { get; set; }
    public int PreservedCount { get; set; }
    public int SuppressedCount { get; set; }
    public int DemonstrationCount { get; set; }
    public int ConceptCount { get; set; }
    public int QualifiedClipCount { get; set; }
    public int ChecksPassed { get; set; }
    public int ExceptionCount { get; set; }
    public int AiCalls { get; set; }
    public string Cost { get; set; } = string.Empty;
    public List<string> Exceptions { get; set; } = [];
}

public sealed class LibraryDocument
{
    public List<SourceClipRecord> Clips { get; set; } = [];
    public List<DemonstrationRecord> Demonstrations { get; set; } = [];
    public List<FactoryBatchRecord> Batches { get; set; } = [];
}

public sealed class ProspectDemonstrationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _root;
    private readonly object _gate = new();

    public ProspectDemonstrationStore(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _root = configuration["Demonstrations:Root"]
            ?? Path.Combine(environment.ContentRootPath, "App_Data", "demonstrations");
        Directory.CreateDirectory(ClipsDirectory);
        Directory.CreateDirectory(RendersDirectory);
    }

    public string Root => _root;
    public string ClipsDirectory => Path.Combine(_root, "clips");
    public string RendersDirectory => Path.Combine(_root, "renders");

    public LibraryDocument Read()
    {
        lock (_gate)
        {
            return ReadUnlocked();
        }
    }

    public void Update(Action<LibraryDocument> change)
    {
        lock (_gate)
        {
            var document = ReadUnlocked();
            change(document);
            var temporary = Path.Combine(_root, "library.json.tmp");
            File.WriteAllText(temporary, JsonSerializer.Serialize(document, JsonOptions));
            File.Move(temporary, Path.Combine(_root, "library.json"), overwrite: true);
        }
    }

    public string ClipPath(SourceClipRecord clip) => Path.Combine(ClipsDirectory, clip.StoredFileName);

    public string RenderPath(string fileName) => Path.Combine(RendersDirectory, fileName);

    private LibraryDocument ReadUnlocked()
    {
        var path = Path.Combine(_root, "library.json");
        if (!File.Exists(path))
        {
            return new LibraryDocument();
        }

        return JsonSerializer.Deserialize<LibraryDocument>(File.ReadAllText(path), JsonOptions)
            ?? new LibraryDocument();
    }
}
