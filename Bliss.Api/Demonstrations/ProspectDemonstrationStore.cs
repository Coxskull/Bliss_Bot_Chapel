using System.Text.Json;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
    public string EconomicsQuoteId { get; set; } = string.Empty;
    public string BlissRematchNotice { get; set; } = string.Empty;
    public string BlissRematchStatus { get; set; } = string.Empty;
    public string BlissRematchCreatorName { get; set; } = string.Empty;
    public decimal? BlissRematchScore { get; set; }
    public List<ContactRoadRecord> ContactRoads { get; set; } = [];
    public List<DeliveryDecisionRecord> DeliveryDecisions { get; set; } = [];
    public List<ConceptRecord> Concepts { get; set; } = [];
    public List<ChatRecord> Messages { get; set; } = [];
    public List<AcquisitionEventRecord> Events { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public sealed class DeliveryDecisionRecord
{
    public string Id { get; set; } = string.Empty;
    public string RoadId { get; set; } = string.Empty;
    public string Policy { get; set; } = string.Empty;
    public string Adapter { get; set; } = string.Empty;
    public string Eligibility { get; set; } = string.Empty;
    public string Transmission { get; set; } = "NOT_SENT";
    public string Notice { get; set; } = string.Empty;
    public string PreparedCopy { get; set; } = string.Empty;
    public DateTime DecidedAt { get; set; }
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
    private readonly IServiceScopeFactory _scopes;
    private readonly object _gate = new();

    public ProspectDemonstrationStore(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IServiceScopeFactory scopes)
    {
        _scopes = scopes;
        _root = configuration["Demonstrations:Root"]
            ?? Path.Combine(environment.ContentRootPath, "App_Data", "demonstrations");
        Directory.CreateDirectory(ClipsDirectory);
        Directory.CreateDirectory(RendersDirectory);
    }

    public string Root => _root;
    public string ClipsDirectory => Path.Combine(_root, "clips");
    public string RendersDirectory => Path.Combine(_root, "renders");

    public string MemoryProvider
    {
        get
        {
            using var scope = _scopes.CreateScope();
            var name = scope.ServiceProvider.GetRequiredService<BlissDbContext>().Database.ProviderName ?? string.Empty;
            if (name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                return "POSTGRESQL";
            }

            if (name.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                return "IN_MEMORY";
            }

            return "UNKNOWN";
        }
    }

    public LibraryDocument Read()
    {
        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            return Load(database);
        }
    }

    public void Update(Action<LibraryDocument> change)
    {
        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            var document = Load(database);
            change(document);
            Save(database, document);
        }
    }

    public string ClipPath(SourceClipRecord clip) => Path.Combine(ClipsDirectory, clip.StoredFileName);

    public string RenderPath(string fileName) => Path.Combine(RendersDirectory, fileName);

    private LibraryDocument Load(BlissDbContext database)
    {
        var prospects = database.ProspectMemories.AsNoTracking().ToList();
        var clips = database.SourceClipMemories.AsNoTracking().ToList();
        var batches = database.FactoryBatchMemories.AsNoTracking().ToList();
        if (prospects.Count == 0 && clips.Count == 0 && batches.Count == 0)
        {
            var imported = ReadFile();
            if (imported.Demonstrations.Count > 0 || imported.Clips.Count > 0 || imported.Batches.Count > 0)
            {
                Save(database, imported);
                return imported;
            }
        }

        return new LibraryDocument
        {
            Demonstrations = prospects.Select(row => JsonSerializer.Deserialize<DemonstrationRecord>(row.PayloadJson, JsonOptions)!).ToList(),
            Clips = clips.Select(row => JsonSerializer.Deserialize<SourceClipRecord>(row.PayloadJson, JsonOptions)!).ToList(),
            Batches = batches.Select(row => JsonSerializer.Deserialize<FactoryBatchRecord>(row.PayloadJson, JsonOptions)!).ToList()
        };
    }

    private static void Save(BlissDbContext database, LibraryDocument document)
    {
        document.Demonstrations ??= [];
        document.Clips ??= [];
        document.Batches ??= [];
        var now = DateTime.UtcNow;
        var prospects = database.ProspectMemories.ToDictionary(item => item.Slug, StringComparer.Ordinal);
        var seenProspects = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in document.Demonstrations)
        {
            if (string.IsNullOrWhiteSpace(record.Slug) || !seenProspects.Add(record.Slug))
            {
                continue;
            }

            if (!prospects.TryGetValue(record.Slug, out var row))
            {
                row = new ProspectMemory { Id = Guid.NewGuid(), Slug = record.Slug };
                database.ProspectMemories.Add(row);
            }

            row.BusinessName = Bound(record.BusinessName, 300);
            row.Market = Bound(record.Market, 128);
            row.ProspectState = Bound(record.ProspectState, 64);
            row.OpportunityScore = record.OpportunityScore;
            row.Suppressed = record.Suppressed;
            row.PayloadJson = JsonSerializer.Serialize(record, JsonOptions);
            row.UpdatedAt = now;
        }

        foreach (var stale in prospects.Values.Where(item => !seenProspects.Contains(item.Slug)))
        {
            database.ProspectMemories.Remove(stale);
        }

        var clips = database.SourceClipMemories.ToDictionary(item => item.Id);
        var seenClips = new HashSet<Guid>();
        foreach (var record in document.Clips)
        {
            if (record.Id == Guid.Empty || !seenClips.Add(record.Id))
            {
                continue;
            }

            if (!clips.TryGetValue(record.Id, out var row))
            {
                row = new SourceClipMemory { Id = record.Id };
                database.SourceClipMemories.Add(row);
            }

            row.Market = Bound(record.Market, 128);
            row.Status = Bound(record.Status, 64);
            row.QuotaCredit = record.QuotaCredit;
            row.Sha256 = Bound(record.Sha256, 128);
            row.PayloadJson = JsonSerializer.Serialize(record, JsonOptions);
            row.UpdatedAt = now;
        }

        foreach (var stale in clips.Values.Where(item => !seenClips.Contains(item.Id)))
        {
            database.SourceClipMemories.Remove(stale);
        }

        var batches = database.FactoryBatchMemories.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var seenBatches = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in document.Batches)
        {
            if (string.IsNullOrWhiteSpace(record.Id) || !seenBatches.Add(record.Id))
            {
                continue;
            }

            if (!batches.TryGetValue(record.Id, out var row))
            {
                row = new FactoryBatchMemory { Id = record.Id };
                database.FactoryBatchMemories.Add(row);
            }

            row.Status = Bound(record.Status, 64);
            row.AiCalls = record.AiCalls;
            row.PayloadJson = JsonSerializer.Serialize(record, JsonOptions);
            row.UpdatedAt = now;
        }

        foreach (var stale in batches.Values.Where(item => !seenBatches.Contains(item.Id)))
        {
            database.FactoryBatchMemories.Remove(stale);
        }

        database.SaveChanges();
    }

    private LibraryDocument ReadFile()
    {
        var path = Path.Combine(_root, "library.json");
        if (!File.Exists(path))
        {
            return new LibraryDocument();
        }

        return JsonSerializer.Deserialize<LibraryDocument>(File.ReadAllText(path), JsonOptions)
            ?? new LibraryDocument();
    }

    private static string Bound(string? value, int max)
    {
        var text = value ?? string.Empty;
        return text.Length <= max ? text : text[..max];
    }
}
