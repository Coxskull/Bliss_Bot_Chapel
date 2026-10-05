using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Api.Runtime;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed record CreativeImageRequest(
    string RequestKey,
    string Prompt,
    int Width,
    int Height);

public sealed record CreativeImageResult(
    byte[] Content,
    string MediaType,
    string ProviderRequestId);

public interface ICreativeImageGenerator
{
    bool Configured { get; }
    Task<CreativeImageResult> GenerateAsync(
        CreativeImageRequest request,
        CancellationToken cancellationToken);
}

public sealed class ConfiguredCreativeImageGenerator(
    HttpClient client,
    BlissRuntimeOptions runtime) : ICreativeImageGenerator
{
    private readonly CreativeGenerationOptions _options = runtime.CreativeGeneration;

    public bool Configured => _options.Configured;

    public async Task<CreativeImageResult> GenerateAsync(
        CreativeImageRequest request,
        CancellationToken cancellationToken)
    {
        if (!Configured)
        {
            throw new InvalidOperationException(
                "The ad creator is installed but its image provider is not configured. "
                + "Set Runtime:CreativeGeneration:Endpoint and supply the API token through the secret store.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 10, 300)));
        using var message = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);
        message.Content = JsonContent.Create(new
        {
            requestId = request.RequestKey,
            prompt = request.Prompt,
            width = request.Width,
            height = request.Height,
            responseFormat = "base64"
        });

        using var response = await client.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                "The configured image provider refused the generation request. No advertisement was stored.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        var body = await JsonSerializer.DeserializeAsync<ProviderImageResponse>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken: timeout.Token);
        if (body is null || string.IsNullOrWhiteSpace(body.ImageBase64))
        {
            throw new InvalidOperationException(
                "The configured image provider returned no image. No advertisement was stored.");
        }

        byte[] content;
        try
        {
            content = Convert.FromBase64String(body.ImageBase64);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "The configured image provider returned an invalid image. No advertisement was stored.");
        }

        var mediaType = NormalizeAndValidateImage(
            content,
            body.MediaType,
            Math.Clamp(_options.MaxImageBytes, 1024, 50 * 1024 * 1024));
        var providerRequestId = (body.ProviderRequestId ?? string.Empty).Trim();
        if (providerRequestId.Length > 200)
        {
            throw new InvalidOperationException(
                "The configured image provider returned an invalid request identifier. No advertisement was stored.");
        }

        return new CreativeImageResult(
            content,
            mediaType,
            providerRequestId);
    }

    internal static string NormalizeAndValidateImage(
        byte[] content,
        string? claimedMediaType,
        int maximumBytes)
    {
        if (content.Length == 0 || content.Length > maximumBytes)
        {
            throw new InvalidOperationException(
                "The configured image provider returned an image outside the allowed size. No advertisement was stored.");
        }

        var detected = content switch
        {
            [0x89, 0x50, 0x4e, 0x47, ..] => "image/png",
            [0xff, 0xd8, 0xff, ..] => "image/jpeg",
            [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
            _ => string.Empty
        };
        var claimed = (claimedMediaType ?? string.Empty).Trim().ToLowerInvariant();
        if (detected.Length == 0 || (claimed.Length > 0 && claimed != detected))
        {
            throw new InvalidOperationException(
                "The configured image provider returned an unsupported image type. No advertisement was stored.");
        }

        return detected;
    }

    private sealed record ProviderImageResponse(
        string? ImageBase64,
        string? MediaType,
        string? ProviderRequestId);
}

public sealed record CreativeGenerationDecision(
    Guid Id,
    string RequestKey,
    string Status,
    string BrandName,
    string Family,
    string TeacherKey,
    string ImagePath,
    string MediaType,
    int ModelCalls,
    bool CampaignReady,
    string Delivery,
    string Notice,
    bool Duplicate);

public sealed class CreativeGenerationService(
    BlissDbContext database,
    ICreativeImageGenerator generator,
    BlissRuntimeOptions runtime,
    IWebHostEnvironment environment)
{
    public bool Configured => generator.Configured;

    public async Task<IReadOnlyList<CreativeAcademyGenerationRow>> ReadAsync(
        CancellationToken cancellationToken) =>
        await database.CreativeAcademyGenerations.AsNoTracking()
            .OrderByDescending(item => item.RecordedAt)
            .ToListAsync(cancellationToken);

    public async Task<CreativeGenerationDecision> GenerateAsync(
        string? requestKey,
        string? family,
        string? brandName,
        string? palette,
        string? fontFamily,
        string? headline,
        string? cta,
        string? market,
        string? language,
        string? requirements,
        bool marketResearchComplete,
        bool approvedPeopleProvided,
        CancellationToken cancellationToken)
    {
        var key = RequireKey(requestKey);
        var prior = await database.CreativeAcademyGenerations.AsNoTracking()
            .FirstOrDefaultAsync(item => item.RequestKey == key, cancellationToken);
        if (prior is not null)
        {
            return Decision(prior, true);
        }

        var brief = CreativeAcademy.PrepareProductionBrief(
            family,
            brandName,
            palette,
            fontFamily,
            headline,
            cta,
            market,
            language,
            requirements,
            marketResearchComplete,
            approvedPeopleProvided,
            false);
        if (brief.Status != CreativeAcademy.ProductionSpecReady)
        {
            throw new InvalidOperationException(
                brief.Status == NicheCatalog.AlreadyCreated
                    ? "This niche was already created. The prototype was not repeated."
                    : "This niche is not created yet. An unrelated quality teacher was not used.");
        }

        CreativeImageResult result;
        try
        {
            result = await generator.GenerateAsync(
                new CreativeImageRequest(key, brief.GenerationRecipe, 1536, 864),
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "The configured image provider timed out. No advertisement was stored.");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException(
                "The configured image provider could not be reached. No advertisement was stored.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                "The configured image provider returned an invalid response. No advertisement was stored.");
        }
        var id = Guid.NewGuid();
        var extension = result.MediaType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => throw new InvalidOperationException("The generated image type is unsupported.")
        };
        var outputRoot = ResolveOutputRoot();
        Directory.CreateDirectory(outputRoot);
        var fileName = id.ToString("N") + extension;
        var fullPath = Path.Combine(outputRoot, fileName);
        await File.WriteAllBytesAsync(fullPath, result.Content, cancellationToken);

        var row = new CreativeAcademyGenerationRow
        {
            Id = id,
            RequestKey = key,
            Family = brief.Family,
            BrandName = brief.BrandName,
            TeacherKey = brief.TeacherKey,
            Status = "GENERATED_PENDING_REVIEW",
            ImagePath = "/operations/generated/" + fileName,
            MediaType = result.MediaType,
            ProviderRequestId = result.ProviderRequestId,
            RecipeSha256 = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(brief.GenerationRecipe))),
            ModelCalls = 1,
            CampaignReady = false,
            Delivery = "NOT_SENT",
            Notice =
                "The configured image provider created one draft. It is GENERATED_PENDING_REVIEW. "
                + "The visual benchmark and all four final gates are unrecorded. "
                + "Campaign ready is false. Delivery remains NOT_SENT.",
            RecordedAt = DateTime.UtcNow
        };
        database.CreativeAcademyGenerations.Add(row);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            File.Delete(fullPath);
            throw;
        }

        return Decision(row, false);
    }

    private string ResolveOutputRoot()
    {
        var configured = runtime.CreativeGeneration.OutputPath.Trim();
        var root = configured.Length > 0
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "frontend", "operations", "generated"));
        return root;
    }

    private static string RequireKey(string? value)
    {
        var key = (value ?? string.Empty).Trim();
        if (key.Length is < 8 or > 80
            || key.Any(character =>
                character is not (>= 'a' and <= 'z'
                    or >= 'A' and <= 'Z'
                    or >= '0' and <= '9'
                    or '-')))
        {
            throw new InvalidOperationException("A valid generation request key is required.");
        }

        return key;
    }

    private static CreativeGenerationDecision Decision(
        CreativeAcademyGenerationRow row,
        bool duplicate) =>
        new(
            row.Id,
            row.RequestKey,
            row.Status,
            row.BrandName,
            row.Family,
            row.TeacherKey,
            row.ImagePath,
            row.MediaType,
            row.ModelCalls,
            row.CampaignReady,
            row.Delivery,
            row.Notice,
            duplicate);
}
