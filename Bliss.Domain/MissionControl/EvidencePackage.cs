using System.Security.Cryptography;
using System.Text.Json;

namespace Bliss.Domain.MissionControl;

public sealed record EvidenceFileRef(string FileName, string EvidenceType, string Sha256);

public sealed record EvidenceManifest(
    string EvidenceId,
    string TaskId,
    string TestName,
    string TestCategory,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    string SubmittedBy,
    string Environment,
    string BuildVersion,
    string CommitId,
    IReadOnlyList<EvidenceFileRef> Files,
    string ClaimedResult,
    string KnownBlockers,
    string KnownFailures,
    string CostStatus,
    bool OwnerDecisionRequired,
    string ReviewStatus,
    string Reviewer,
    DateTime? ReviewedAt,
    string FinalReviewResult,
    string? ParentEvidenceId,
    string RetestReason,
    string DriveStatus,
    string DriveLocation);

public sealed record ApprovedValidationTest(string Key, string Name, string Area);

public static class EvidenceIdentity
{
    public const string Prefix = "ALPHA-EV";
    public const string Unreported = "UNREPORTED_BY_PROVIDER";
    public const string Unrecorded = "UNRECORDED";
    public const string NotReviewed = "NOT_REVIEWED";
    public const string NotReady = "NOT_READY_FOR_REVIEW";
    public const string NotConnected = "NOT_CONNECTED";
    public const string NotRun = "NOT_RUN";
    public const string RetestReasonText = "RETEST AFTER CORRECTION";
    public const string DriveRoot = "ALPHA — ERWIN ↔ CHATGPT MISSION CONTROL";
    public const string CurrentAssignmentFolder = "01 — CURRENT ASSIGNMENT";
    public const string SubmittedFolder = "02 — EVIDENCE SUBMITTED";
    public const string ReviewFolder = "03 — CHATGPT REVIEW";
    public const string CorrectionsFolder = "04 — CORRECTIONS REQUIRED";
    public const string AcceptedFolder = "05 — ACCEPTED EVIDENCE";
    public const string OwnerDecisionFolder = "06 — OWNER DECISION REQUIRED";
    public const string ArchiveFolder = "07 — ARCHIVE";
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static readonly string[] ReviewResults =
    [
        "PASS",
        "FAIL",
        "BLOCKED",
        "MISSING_EVIDENCE",
        "CORRECTION_REQUIRED",
        "RETEST_REQUIRED",
        "OWNER_DECISION_REQUIRED",
        NotReady,
        NotReviewed
    ];

    public static readonly string[] OwnerCategories =
    [
        "subscription",
        "spending",
        "economics",
        "pricing",
        "discount",
        "contract",
        "legal",
        "payment",
        "architecture",
        "doctrine",
        "requirement-removal",
        "creator-commitment",
        "human-approval",
        "amendment-closure"
    ];

    public static IReadOnlyList<ApprovedValidationTest> Catalog { get; } =
    [
        new("academy-retrieval", "Retrieval selects only active files and records why", "Creative Academy"),
        new("academy-notes", "Retrieval returns stored LEARN and DO NOT COPY notes", "Creative Academy"),
        new("academy-originality", "A do-not-copy identity fails originality", "Creative Academy"),
        new("catalog-price", "A draft catalog states no price", "Advertising Real Estate"),
        new("catalog-occupancy", "Occupancy stays unrecorded until areas are supplied", "Advertising Real Estate"),
        new("ask-alpha-price", "A price question advances without a number", "Ask Alpha"),
        new("economics-speech", "A missing Economics result states no number", "Economics"),
        new("tenant-isolation", "A tenant reading hides the other advertiser", "Marketplace"),
        new("conversation", "Persona scenarios pass without a behavior change", "Conversation"),
        new("evidence-retrieval", "Evidence retrieval itself", "Mission Control")
    ];

    public static string Issue(DateTime utc, Func<string, bool> alreadyUsed)
    {
        if (utc.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("An evidence id requires a UTC timestamp. None was invented.");
        }

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var id = Format(utc, RandomNumberGenerator.GetBytes(6));
            if (!alreadyUsed(id))
            {
                return id;
            }
        }

        throw new InvalidOperationException("An evidence id collided. None was reused.");
    }

    public static string Format(DateTime utc, ReadOnlySpan<byte> entropy)
    {
        if (entropy.Length < 6)
        {
            throw new InvalidOperationException("An evidence id requires a random suffix. The date alone is not an id.");
        }

        var suffix = new char[6];
        for (var index = 0; index < suffix.Length; index++)
        {
            suffix[index] = Alphabet[entropy[index] % Alphabet.Length];
        }

        return Prefix + "-" + utc.ToString("yyyyMMdd") + "-" + new string(suffix);
    }

    public static bool IsWellFormed(string? evidenceId) =>
        evidenceId is { Length: 24 }
        && evidenceId.StartsWith(Prefix + "-", StringComparison.Ordinal)
        && evidenceId[17] == '-'
        && evidenceId[9..17].All(char.IsDigit)
        && evidenceId[18..].All(character => Alphabet.Contains(character));

    public static bool RequiresOwnerDecision(string? category)
    {
        var token = (category ?? string.Empty).Trim().ToLowerInvariant();
        return OwnerCategories.Contains(token, StringComparer.Ordinal);
    }

    public static ApprovedValidationTest SelectValidation(int seed)
    {
        var index = (int)((uint)seed % (uint)Catalog.Count);
        return Catalog[index];
    }

    public static readonly string[] DriveFolders =
    [
        CurrentAssignmentFolder,
        SubmittedFolder,
        ReviewFolder,
        CorrectionsFolder,
        AcceptedFolder,
        OwnerDecisionFolder,
        ArchiveFolder
    ];

    public static string FolderFor(EvidenceManifest manifest) => manifest.FinalReviewResult switch
    {
        "CORRECTION_REQUIRED" or "RETEST_REQUIRED" => CorrectionsFolder,
        "PASS" => AcceptedFolder,
        "OWNER_DECISION_REQUIRED" => OwnerDecisionFolder,
        _ => SubmittedFolder
    };

    public static string FileName(string evidenceId, string evidenceType, int sequence = 1)
    {
        RequireId(evidenceId);
        var role = evidenceType.Trim().ToUpperInvariant() switch
        {
            "REPORT" => "REPORT.pdf",
            "VIDEO" => "VIDEO.mp4",
            "LOG" => "LOG.txt",
            "MANIFEST" => "MANIFEST.json",
            "SCREENSHOT" => "SCREENSHOT-" + sequence.ToString("00") + ".png",
            _ => throw new InvalidOperationException("That evidence type is not part of a package. None was added.")
        };
        return evidenceId + "-" + role;
    }

    public static void RequireSafe(string? value, string label)
    {
        var text = value ?? string.Empty;
        var lowered = text.ToLowerInvariant();
        string[] markers =
        [
            "password=",
            "api_key",
            "apikey",
            "secret_key",
            "begin private",
            "defaultconnection",
            "bearer ",
            "connectionstring"
        ];
        if (markers.Any(marker => lowered.Contains(marker, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The " + label + " contains a secret. None was stored.");
        }
    }

    public static EvidenceManifest Review(
        EvidenceManifest manifest,
        string? reviewer,
        string? reviewerRole,
        string? result,
        DateTime reviewedAt)
    {
        var name = (reviewer ?? string.Empty).Trim();
        var outcome = (result ?? string.Empty).Trim();
        if (name.Length is < 2 or > 80
            || string.Equals(name, manifest.SubmittedBy, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "ALPHA", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The test author cannot review its own package. None was graded.");
        }

        if (!ReviewResults.Contains(outcome, StringComparer.Ordinal) || outcome == NotReviewed)
        {
            throw new InvalidOperationException("That review result is not one of the recorded outcomes. None was invented.");
        }

        if (reviewedAt.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("A review timestamp must be UTC. None was invented.");
        }

        if (string.Equals(outcome, "PASS", StringComparison.Ordinal)
            && manifest.OwnerDecisionRequired
            && !string.Equals(reviewerRole, "OWNER", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("This package needs an owner decision. A pass was not recorded.");
        }

        if (manifest.Files.Count == 0 && string.Equals(outcome, "PASS", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A package with no files was not marked passed. None was invented.");
        }

        return manifest with
        {
            ReviewStatus = outcome,
            Reviewer = name,
            ReviewedAt = reviewedAt,
            FinalReviewResult = outcome
        };
    }

    public static string Serialize(EvidenceManifest manifest) =>
        JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });

    private static void RequireId(string evidenceId)
    {
        if (!IsWellFormed(evidenceId))
        {
            throw new InvalidOperationException("The evidence id is not a system id. None was substituted.");
        }
    }
}
