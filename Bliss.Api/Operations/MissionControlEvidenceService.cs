using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Domain.Entities;
using Bliss.Domain.MissionControl;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class MissionControlEvidenceService(
    BlissDbContext database,
    IWebHostEnvironment environment,
    IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const uint MaxScreenshotDimension = 16_384;
    private const ulong MaxScreenshotPixels = 40_000_000;

    public async Task<IReadOnlyList<EvidenceManifest>> ReadAsync(CancellationToken cancellationToken)
    {
        var rows = await database.EvidencePackages.AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(ReadManifest).ToArray();
    }

    public async Task<EvidenceManifest> ReadOneAsync(string evidenceId, CancellationToken cancellationToken)
    {
        var row = await FindAsync(evidenceId, cancellationToken);
        return ReadManifest(row);
    }

    public async Task<EvidenceManifest> IssueAsync(EvidenceIssueRequest? request, CancellationToken cancellationToken)
    {
        var manifest = NewManifest(
            await NextIdAsync(cancellationToken),
            request,
            parentId: null,
            retestReason: string.Empty,
            DateTime.UtcNow);
        await StoreAsync(manifest, cancellationToken);
        return manifest;
    }

    public async Task<EvidenceManifest> AttachAsync(
        string evidenceId,
        string? evidenceType,
        byte[]? content,
        CancellationToken cancellationToken)
    {
        var row = await FindAsync(evidenceId, cancellationToken);
        var manifest = ReadManifest(row);
        var type = (evidenceType ?? string.Empty).Trim();
        var bytes = content ?? [];
        if (bytes.Length == 0 || bytes.Length > 20_000_000)
        {
            throw new InvalidOperationException("An evidence file is required. None was invented.");
        }

        if (type.Equals("SCREENSHOT", StringComparison.OrdinalIgnoreCase))
        {
            if (!HasPngHeader(bytes))
            {
                throw new InvalidOperationException("A screenshot must contain a valid PNG signature and IHDR dimensions. None was stored.");
            }

            if (!HasSafePngDimensions(bytes))
            {
                throw new InvalidOperationException("Screenshot dimensions exceed the allowed validation limit. None was stored.");
            }

            await ValidatePngDecodingAsync(bytes, cancellationToken);
        }

        var sequence = manifest.Files.Count(item => item.EvidenceType.Equals("SCREENSHOT", StringComparison.OrdinalIgnoreCase)) + 1;
        var fileName = EvidenceIdentity.FileName(manifest.EvidenceId, type, sequence);
        EvidenceIdentity.RequireSafe(fileName, "file name");
        if (type.Equals("LOG", StringComparison.OrdinalIgnoreCase)
            || type.Equals("REPORT", StringComparison.OrdinalIgnoreCase)
            || type.Equals("MANIFEST", StringComparison.OrdinalIgnoreCase))
        {
            EvidenceIdentity.RequireSafe(Encoding.Latin1.GetString(bytes), "file");
        }

        var path = Path.Combine(PackageDirectory(manifest.EvidenceId), fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        var files = manifest.Files.Append(new EvidenceFileRef(
            fileName,
            type.Trim().ToUpperInvariant(),
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant())).ToArray();
        var updated = manifest with { Files = files, SubmittedAt = DateTime.UtcNow };
        await StoreAsync(updated, cancellationToken);
        return updated;
    }

    public async Task VerifyVisualArtifactAsync(string evidenceId, CancellationToken cancellationToken)
    {
        var manifest = ReadManifest(await FindAsync(evidenceId, cancellationToken));
        var screenshots = manifest.Files
            .Where(item => item.EvidenceType.Equals("SCREENSHOT", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (screenshots.Length == 0)
        {
            throw new InvalidOperationException("A stored screenshot artifact is required. None was verified.");
        }

        foreach (var screenshot in screenshots)
        {
            if (!string.Equals(Path.GetFileName(screenshot.FileName), screenshot.FileName, StringComparison.Ordinal)
                || !screenshot.FileName.StartsWith(evidenceId + "-SCREENSHOT-", StringComparison.Ordinal)
                || !screenshot.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The screenshot manifest contains an unsafe file reference. None was verified.");
            }

            var path = Path.Combine(PackageDirectory(evidenceId), screenshot.FileName);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("A screenshot listed in the evidence manifest is missing. None was verified.");
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(actualHash, screenshot.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("A screenshot hash does not match its evidence manifest. None was verified.");
            }
        }
    }

    public async Task<EvidenceManifest> RetestAsync(
        string parentId,
        EvidenceIssueRequest? request,
        CancellationToken cancellationToken)
    {
        var parent = ReadManifest(await FindAsync(parentId, cancellationToken));
        var reason = (request?.RetestReason ?? EvidenceIdentity.RetestReasonText).Trim();
        if (!string.Equals(reason, EvidenceIdentity.RetestReasonText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A retest must say RETEST AFTER CORRECTION. The original package was not overwritten.");
        }

        if (parent.FinalReviewResult is not ("CORRECTION_REQUIRED" or "RETEST_REQUIRED"))
        {
            throw new InvalidOperationException("A retest follows a recorded correction. The original package was not overwritten.");
        }

        var manifest = NewManifest(
            await NextIdAsync(cancellationToken),
            new EvidenceIssueRequest(
                string.IsNullOrWhiteSpace(request?.TaskId) ? parent.TaskId : request!.TaskId,
                string.IsNullOrWhiteSpace(request?.TestName) ? parent.TestName : request!.TestName,
                string.IsNullOrWhiteSpace(request?.TestCategory) ? parent.TestCategory : request!.TestCategory,
                string.IsNullOrWhiteSpace(request?.SubmittedBy) ? parent.SubmittedBy : request!.SubmittedBy,
                string.IsNullOrWhiteSpace(request?.Environment) ? parent.Environment : request!.Environment,
                string.IsNullOrWhiteSpace(request?.BuildVersion) ? parent.BuildVersion : request!.BuildVersion,
                string.IsNullOrWhiteSpace(request?.CommitId) ? parent.CommitId : request!.CommitId,
                string.IsNullOrWhiteSpace(request?.ClaimedResult) ? EvidenceIdentity.NotReady : request!.ClaimedResult,
                request?.KnownBlockers ?? parent.KnownBlockers,
                request?.KnownFailures ?? parent.KnownFailures,
                string.IsNullOrWhiteSpace(request?.CostStatus) ? parent.CostStatus : request!.CostStatus,
                reason),
            parent.EvidenceId,
            reason,
            DateTime.UtcNow) with
        {
            TaskId = parent.TaskId,
            TestName = parent.TestName,
            TestCategory = parent.TestCategory
        };
        if (string.Equals(manifest.EvidenceId, parent.EvidenceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A retest reused an evidence id. None was overwritten.");
        }

        await StoreAsync(manifest, cancellationToken);
        return manifest;
    }

    public async Task<EvidenceManifest> ReviewAsync(
        string evidenceId,
        string? reviewer,
        string? reviewerRole,
        string? result,
        CancellationToken cancellationToken)
    {
        var row = await FindAsync(evidenceId, cancellationToken);
        var updated = EvidenceIdentity.Review(ReadManifest(row), reviewer, reviewerRole, result, DateTime.UtcNow);
        if (!string.Equals(updated.EvidenceId, row.EvidenceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A review changed the evidence id. None was substituted.");
        }

        await StoreAsync(updated, cancellationToken);
        return updated;
    }

    public ApprovedValidationTest Select(int? seed) =>
        EvidenceIdentity.SelectValidation(seed ?? Random.Shared.Next());

    public async Task<EvidenceManifest> RunValidationAsync(int? seed, CancellationToken cancellationToken)
    {
        var selected = Select(seed);
        var observation = ApprovedValidation.Observe(selected);
        if (observation.ClaimedResult is not (ApprovedValidation.Observed or ApprovedValidation.Failed))
        {
            throw new InvalidOperationException("A validation run invented a review result. None was stored.");
        }

        var manifest = await IssueAsync(new EvidenceIssueRequest(
            "MC-VALIDATION",
            selected.Name,
            ApprovedValidation.Category(selected),
            "Erwin",
            environment.EnvironmentName,
            EvidenceIdentity.Unrecorded,
            EvidenceIdentity.Unrecorded,
            observation.ClaimedResult,
            "Google Drive upload is NOT_CONNECTED. ChatGPT retrieval is NOT_RUN. This run is not a review.",
            observation.Held ? string.Empty : observation.Log,
            EvidenceIdentity.Unrecorded,
            null), cancellationToken);
        if (!string.Equals(manifest.FinalReviewResult, EvidenceIdentity.NotReviewed, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A validation run reviewed itself. None was accepted.");
        }

        return await AttachAsync(
            manifest.EvidenceId,
            "LOG",
            Encoding.UTF8.GetBytes(observation.Log),
            cancellationToken);
    }

    public async Task<UploadFolderResult> ExportUploadAsync(CancellationToken cancellationToken)
    {
        var manifests = await ReadAsync(cancellationToken);
        var root = Path.Combine(Root(), EvidenceIdentity.DriveRoot);
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        foreach (var folder in EvidenceIdentity.DriveFolders)
        {
            Directory.CreateDirectory(Path.Combine(root, folder));
        }

        var entries = new List<UploadPackageEntry>();
        foreach (var manifest in manifests)
        {
            var folder = EvidenceIdentity.FolderFor(manifest);
            var destination = Path.Combine(root, folder, manifest.EvidenceId);
            Directory.CreateDirectory(destination);
            var source = PackageDirectory(manifest.EvidenceId);
            var copied = new List<string>();
            if (Directory.Exists(source))
            {
                foreach (var file in Directory.GetFiles(source))
                {
                    var name = Path.GetFileName(file);
                    if (!name.StartsWith(manifest.EvidenceId + "-", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    File.Copy(file, Path.Combine(destination, name), true);
                    copied.Add(name);
                }
            }

            var manifestName = manifest.EvidenceId + "-MANIFEST.json";
            if (!copied.Contains(manifestName, StringComparer.Ordinal))
            {
                var json = EvidenceIdentity.Serialize(manifest);
                EvidenceIdentity.RequireSafe(json, "manifest");
                await File.WriteAllTextAsync(Path.Combine(destination, manifestName), json, cancellationToken);
                copied.Add(manifestName);
            }

            entries.Add(new UploadPackageEntry(
                manifest.EvidenceId,
                folder,
                manifest.TestName,
                manifest.ClaimedResult,
                manifest.FinalReviewResult,
                manifest.CreatedAt,
                copied.OrderBy(name => name, StringComparer.Ordinal).ToArray()));
        }

        var assignment = entries
            .Where(item => item.Folder == EvidenceIdentity.SubmittedFolder
                && item.Files.Any(name => name.EndsWith("-REPORT.pdf", StringComparison.Ordinal))
                && item.Files.Any(name => name.EndsWith("-VIDEO.mp4", StringComparison.Ordinal)))
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        var assignmentText = AssignmentText(assignment?.EvidenceId, entries);
        EvidenceIdentity.RequireSafe(assignmentText, "assignment");
        await File.WriteAllTextAsync(
            Path.Combine(root, EvidenceIdentity.CurrentAssignmentFolder, "CURRENT-ASSIGNMENT.txt"),
            assignmentText,
            cancellationToken);

        var index = JsonSerializer.Serialize(new
        {
            driveStatus = EvidenceIdentity.NotConnected,
            chatgptRetrieval = EvidenceIdentity.NotRun,
            uploadPerformed = false,
            assignmentEvidenceId = assignment?.EvidenceId,
            packages = entries.Select(item => new
            {
                item.EvidenceId,
                item.Folder,
                item.TestName,
                item.ClaimedResult,
                item.FinalReviewResult,
                item.Files
            })
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        EvidenceIdentity.RequireSafe(index, "index");
        await File.WriteAllTextAsync(Path.Combine(root, "EVIDENCE-INDEX.json"), index, cancellationToken);

        foreach (var folder in EvidenceIdentity.DriveFolders)
        {
            var path = Path.Combine(root, folder);
            if (!Directory.EnumerateFileSystemEntries(path).Any())
            {
                var note = "This folder is empty.\nNo evidence package has been placed here.\nGoogle Drive upload is NOT_CONNECTED.\nChatGPT retrieval is NOT_RUN.\n";
                await File.WriteAllTextAsync(Path.Combine(path, "EMPTY.txt"), note, cancellationToken);
            }
        }

        return new UploadFolderResult(
            EvidenceIdentity.DriveRoot,
            EvidenceIdentity.NotConnected,
            EvidenceIdentity.NotRun,
            assignment?.EvidenceId,
            entries.Count);
    }

    public async Task<EvidenceManifest> PrepareRetrievalProbeAsync(CancellationToken cancellationToken)
    {
        var manifest = await IssueAsync(new EvidenceIssueRequest(
            "MC-EVIDENCE-ID",
            "Mission Control retrieval probe",
            "evidence-retrieval",
            "Erwin",
            environment.EnvironmentName,
            "UNRECORDED",
            "UNRECORDED",
            EvidenceIdentity.NotReady,
            "Google Drive upload is NOT_CONNECTED. ChatGPT retrieval is NOT_RUN.",
            string.Empty,
            EvidenceIdentity.Unrecorded,
            null), cancellationToken);
        var lines = new[]
        {
            "Evidence ID: " + manifest.EvidenceId,
            "This PDF and its video share one evidence id.",
            "Claimed result: NOT_READY_FOR_REVIEW.",
            "Google Drive: NOT_CONNECTED.",
            "ChatGPT retrieval: NOT_RUN.",
            "This probe does not accept a product requirement."
        };
        manifest = await AttachAsync(manifest.EvidenceId, "REPORT", PlainPdf.Write(manifest.EvidenceId, lines), cancellationToken);
        manifest = await AttachAsync(manifest.EvidenceId, "VIDEO", await ProbeVideoAsync(manifest.EvidenceId, cancellationToken), cancellationToken);
        return manifest;
    }

    private async Task<string> NextIdAsync(CancellationToken cancellationToken)
    {
        var existing = await database.EvidencePackages.AsNoTracking()
            .Select(item => item.EvidenceId)
            .ToListAsync(cancellationToken);
        var used = existing.ToHashSet(StringComparer.Ordinal);
        return EvidenceIdentity.Issue(DateTime.UtcNow, used.Contains);
    }

    private async Task<EvidencePackageRow> FindAsync(string evidenceId, CancellationToken cancellationToken)
    {
        if (!EvidenceIdentity.IsWellFormed(evidenceId))
        {
            throw new InvalidOperationException("The evidence id is not a system id. None was substituted.");
        }

        return await database.EvidencePackages.SingleOrDefaultAsync(item => item.EvidenceId == evidenceId, cancellationToken)
            ?? throw new InvalidOperationException("That evidence id is not stored. None was substituted.");
    }

    private async Task StoreAsync(EvidenceManifest manifest, CancellationToken cancellationToken)
    {
        var json = EvidenceIdentity.Serialize(manifest);
        EvidenceIdentity.RequireSafe(json, "manifest");
        var directory = PackageDirectory(manifest.EvidenceId);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, manifest.EvidenceId + "-MANIFEST.json"), json, cancellationToken);
        var row = await database.EvidencePackages.SingleOrDefaultAsync(item => item.EvidenceId == manifest.EvidenceId, cancellationToken);
        if (row is null)
        {
            database.EvidencePackages.Add(new EvidencePackageRow
            {
                EvidenceId = manifest.EvidenceId,
                ManifestJson = json,
                CreatedAt = manifest.CreatedAt,
                ParentEvidenceId = manifest.ParentEvidenceId,
                ReviewStatus = manifest.ReviewStatus,
                DriveStatus = manifest.DriveStatus
            });
        }
        else
        {
            if (!string.Equals(row.EvidenceId, manifest.EvidenceId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("An evidence id changed after issuance. None was reused.");
            }

            row.ManifestJson = json;
            row.ReviewStatus = manifest.ReviewStatus;
            row.DriveStatus = manifest.DriveStatus;
            row.ParentEvidenceId = manifest.ParentEvidenceId;
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private static async Task ValidatePngDecodingAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), "bliss-screenshot-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            await File.WriteAllBytesAsync(path, bytes, cancellationToken);
            var start = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("-v");
            start.ArgumentList.Add("error");
            start.ArgumentList.Add("-xerror");
            start.ArgumentList.Add("-i");
            start.ArgumentList.Add(path);
            start.ArgumentList.Add("-frames:v");
            start.ArgumentList.Add("1");
            start.ArgumentList.Add("-f");
            start.ArgumentList.Add("null");
            start.ArgumentList.Add("-");

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("PNG decoder could not start. The screenshot was not stored.");
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                throw new InvalidOperationException("PNG decoding exceeded the validation time limit. The screenshot was not stored.");
            }

            var error = await errorTask;
            if (process.ExitCode != 0)
            {
                _ = error;
                throw new InvalidOperationException("Screenshot bytes could not be decoded as a PNG image. None was stored.");
            }
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static bool HasPngHeader(byte[] bytes)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(signature))
        {
            return false;
        }

        if (bytes[8] != 0 || bytes[9] != 0 || bytes[10] != 0 || bytes[11] != 13
            || bytes[12] != (byte)'I' || bytes[13] != (byte)'H'
            || bytes[14] != (byte)'D' || bytes[15] != (byte)'R')
        {
            return false;
        }

        var width = ((uint)bytes[16] << 24) | ((uint)bytes[17] << 16) | ((uint)bytes[18] << 8) | bytes[19];
        var height = ((uint)bytes[20] << 24) | ((uint)bytes[21] << 16) | ((uint)bytes[22] << 8) | bytes[23];
        return width > 0 && height > 0;
    }

    private static bool HasSafePngDimensions(byte[] bytes)
    {
        var width = ((uint)bytes[16] << 24) | ((uint)bytes[17] << 16) | ((uint)bytes[18] << 8) | bytes[19];
        var height = ((uint)bytes[20] << 24) | ((uint)bytes[21] << 16) | ((uint)bytes[22] << 8) | bytes[23];
        return width <= MaxScreenshotDimension
            && height <= MaxScreenshotDimension
            && (ulong)width * height <= MaxScreenshotPixels;
    }

    private string PackageDirectory(string evidenceId) =>
        Path.Combine(Root(), EvidenceIdentity.SubmittedFolder, evidenceId);

    private string Root() =>
        string.IsNullOrWhiteSpace(configuration["MissionControl:Root"])
            ? Path.Combine(environment.ContentRootPath, "data", "mission-control")
            : configuration["MissionControl:Root"]!;

    private static EvidenceManifest NewManifest(
        string evidenceId,
        EvidenceIssueRequest? request,
        string? parentId,
        string retestReason,
        DateTime createdAt)
    {
        var task = RequireToken(request?.TaskId, "task");
        var name = RequireToken(request?.TestName, "test name");
        var category = RequireToken(request?.TestCategory, "category");
        var submittedBy = RequireToken(request?.SubmittedBy, "submitter");
        var claimed = string.IsNullOrWhiteSpace(request?.ClaimedResult)
            ? EvidenceIdentity.NotReady
            : request!.ClaimedResult.Trim();
        if (string.Equals(claimed, "PASS", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Issuing an id does not mark the test passed. None was invented.");
        }

        var cost = string.IsNullOrWhiteSpace(request?.CostStatus) ? EvidenceIdentity.Unrecorded : request!.CostStatus.Trim();
        if (decimal.TryParse(cost, out _))
        {
            throw new InvalidOperationException("A cost was not reported. None was invented.");
        }

        EvidenceIdentity.RequireSafe(task + name + category + submittedBy + claimed + cost, "request");
        return new EvidenceManifest(
            evidenceId,
            task,
            name,
            category,
            createdAt,
            null,
            submittedBy,
            string.IsNullOrWhiteSpace(request?.Environment) ? "UNRECORDED" : request!.Environment.Trim(),
            string.IsNullOrWhiteSpace(request?.BuildVersion) ? EvidenceIdentity.Unrecorded : request!.BuildVersion.Trim(),
            string.IsNullOrWhiteSpace(request?.CommitId) ? EvidenceIdentity.Unrecorded : request!.CommitId.Trim(),
            [],
            claimed,
            request?.KnownBlockers?.Trim() ?? string.Empty,
            request?.KnownFailures?.Trim() ?? string.Empty,
            cost,
            EvidenceIdentity.RequiresOwnerDecision(category),
            EvidenceIdentity.NotReviewed,
            string.Empty,
            null,
            EvidenceIdentity.NotReviewed,
            parentId,
            retestReason,
            EvidenceIdentity.NotConnected,
            EvidenceIdentity.SubmittedFolder + "/" + evidenceId);
    }

    private static EvidenceManifest ReadManifest(EvidencePackageRow row) =>
        JsonSerializer.Deserialize<EvidenceManifest>(row.ManifestJson, JsonOptions)
        ?? throw new InvalidOperationException("The evidence manifest is not stored. None was invented.");

    private static string AssignmentText(string? evidenceId, IReadOnlyList<UploadPackageEntry> entries)
    {
        var others = entries
            .Select(item => item.EvidenceId)
            .Where(id => !string.Equals(id, evidenceId, StringComparison.Ordinal))
            .ToArray();
        var otherLines = others.Length == 0
            ? "No other evidence id is in this folder."
            : "Other evidence ids are different packages. Do not assess them unless they are named:\n" + string.Join("\n", others);
        if (string.IsNullOrWhiteSpace(evidenceId))
        {
            return "CURRENT ASSIGNMENT\n\nNo retrieval package with a report and a video is stored.\nGoogle Drive upload is NOT_CONNECTED.\nChatGPT retrieval is NOT_RUN.\nThis file is not a review.\n";
        }

        return "CURRENT ASSIGNMENT\n\nAssess this evidence id only:\n"
            + evidenceId
            + "\n\nMessage for ChatGPT:\nAssess "
            + evidenceId
            + ".\n\nPackage folder:\n"
            + EvidenceIdentity.SubmittedFolder
            + "/"
            + evidenceId
            + "\n\n"
            + otherLines
            + "\n\nGoogle Drive upload is NOT_CONNECTED.\nChatGPT retrieval is NOT_RUN.\nThis file is not a review.\n";
    }

    private static string RequireToken(string? value, string label)
    {
        var token = (value ?? string.Empty).Trim();
        if (token.Length is < 2 or > 160)
        {
            throw new InvalidOperationException("A " + label + " is required. None was invented.");
        }

        return token;
    }

    private async Task<byte[]> ProbeVideoAsync(string evidenceId, CancellationToken cancellationToken)
    {
        var caption = Path.Combine(Path.GetTempPath(), evidenceId + "-caption.txt");
        var output = Path.Combine(Path.GetTempPath(), evidenceId + "-VIDEO.mp4");
        await File.WriteAllTextAsync(caption, evidenceId + "\nRetrieval probe. Not a product acceptance.", cancellationToken);
        var font = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
        var start = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            ArgumentList =
            {
                "-y",
                "-f", "lavfi",
                "-i", "color=c=0x12303a:s=640x360:d=2",
                "-vf", "drawtext=fontfile=" + font + ":textfile=" + caption + ":fontsize=22:fontcolor=white:x=24:y=150",
                "-pix_fmt", "yuv420p",
                output
            },
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The retrieval video was not created. None was invented.");
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0 || !File.Exists(output))
        {
            throw new InvalidOperationException("The retrieval video was not created. None was invented.");
        }

        _ = error;
        return await File.ReadAllBytesAsync(output, cancellationToken);
    }
}

public sealed record UploadFolderResult(
    string DriveRoot,
    string DriveStatus,
    string ChatGptRetrieval,
    string? AssignmentEvidenceId,
    int PackageCount);

public sealed record UploadPackageEntry(
    string EvidenceId,
    string Folder,
    string TestName,
    string ClaimedResult,
    string FinalReviewResult,
    DateTime CreatedAt,
    IReadOnlyList<string> Files);

public sealed record EvidenceIssueRequest(
    string? TaskId,
    string? TestName,
    string? TestCategory,
    string? SubmittedBy,
    string? Environment,
    string? BuildVersion,
    string? CommitId,
    string? ClaimedResult,
    string? KnownBlockers,
    string? KnownFailures,
    string? CostStatus,
    string? RetestReason);

internal static class PlainPdf
{
    public static byte[] Write(string title, IReadOnlyList<string> lines)
    {
        var stream = new StringBuilder();
        stream.Append("BT\n/F1 16 Tf\n72 740 Td\n(");
        stream.Append(Escape(title));
        stream.Append(") Tj\n/F1 11 Tf\n");
        foreach (var line in lines)
        {
            stream.Append("0 -18 Td\n(");
            stream.Append(Escape(line));
            stream.Append(") Tj\n");
        }

        stream.Append("ET\n");
        var contents = stream.ToString();
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            "<< /Length " + contents.Length + " >>\nstream\n" + contents + "endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");
        var offsets = new int[objects.Length];
        for (var index = 0; index < objects.Length; index++)
        {
            offsets[index] = builder.Length;
            builder.Append(index + 1);
            builder.Append(" 0 obj\n");
            builder.Append(objects[index]);
            builder.Append("\nendobj\n");
        }

        var xref = builder.Length;
        builder.Append("xref\n0 ");
        builder.Append(objects.Length + 1);
        builder.Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            builder.Append(offset.ToString("D10"));
            builder.Append(" 00000 n \n");
        }

        builder.Append("trailer\n<< /Size ");
        builder.Append(objects.Length + 1);
        builder.Append(" /Root 1 0 R >>\nstartxref\n");
        builder.Append(xref);
        builder.Append("\n%%EOF");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
}
