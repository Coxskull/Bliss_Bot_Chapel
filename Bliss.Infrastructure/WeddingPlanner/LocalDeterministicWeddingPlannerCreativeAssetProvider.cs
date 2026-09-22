using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Domain.WeddingPlanner;
using Microsoft.Extensions.Options;

namespace Bliss.Infrastructure.WeddingPlanner;

/// <summary>
/// Deterministic local creative asset provider. Generates valid PNG bytes for the exact canvas
/// using System.IO.Compression/ZLibStream only — no new dependencies. Output is stable for
/// identical inputs. Wedding Planner orchestrates this provider; it is not an AI worker.
/// </summary>
public sealed class LocalDeterministicWeddingPlannerCreativeAssetProvider : IWeddingPlannerCreativeAssetProvider
{
    public const string ProviderKey = "local-deterministic-creative-asset";
    public const string AdapterVersion = "wp-creative-asset-local-adapter.v1";

    private readonly WeddingPlannerCreativeAssetOptions _options;

    public LocalDeterministicWeddingPlannerCreativeAssetProvider(
        IOptions<WeddingPlannerCreativeAssetOptions>? options = null)
    {
        _options = options?.Value ?? new WeddingPlannerCreativeAssetOptions();
    }

    public string WorkerKey =>
        string.IsNullOrWhiteSpace(_options.WorkerKey)
            ? WeddingPlannerCreativeAssetWorkers.LocalDeterministicV1
            : _options.WorkerKey.Trim();

    public int InvokeCount { get; private set; }

    public Task<WeddingPlannerCreativeAssetGenerationResult> GeneratePngAsync(
        WeddingPlannerCreativeAssetGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        InvokeCount++;

        if (request.Width <= 0 || request.Height <= 0)
        {
            throw new InvalidOperationException("Creative asset canvas dimensions must be positive.");
        }

        var seedMaterial = string.Join('\n',
            request.VariantId,
            request.Format,
            request.Width.ToString(),
            request.Height.ToString(),
            request.RenderSpecJson ?? string.Empty,
            request.CreativeProductionJobId.ToString("D"),
            request.ApprovedConceptPackageVersionId.ToString("D"),
            request.SelectedConceptId);
        var seed = SHA256.HashData(Encoding.UTF8.GetBytes(seedMaterial));
        var png = BuildDeterministicTruecolorPng(request.Width, request.Height, seed);
        var validated = WeddingPlannerPngValidator.ValidateExactCanvas(png, request.Width, request.Height);

        var receipt = JsonSerializer.Serialize(new
        {
            deterministic = true,
            variantId = request.VariantId,
            format = request.Format,
            width = validated.Width,
            height = validated.Height,
            sha256 = validated.Sha256
        });

        return Task.FromResult(new WeddingPlannerCreativeAssetGenerationResult(
            validated.Bytes,
            ProviderKey,
            AdapterVersion,
            "local-" + validated.Sha256[..16],
            WorkerKey,
            receipt,
            _options.LocalEstimatedCostUsd));
    }

    /// <summary>
    /// Builds a minimal IHDR/IDAT/IEND PNG (8-bit truecolor, non-interlaced, filter 0)
    /// with deterministic RGB derived from the seed. Uses ZLibStream only.
    /// </summary>
    public static byte[] BuildDeterministicTruecolorPng(int width, int height, byte[] seed)
    {
        const int bytesPerPixel = 3;
        var raw = new byte[height * (1 + width * bytesPerPixel)];
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * (1 + width * bytesPerPixel);
            raw[rowOffset] = 0; // None filter
            for (var x = 0; x < width; x++)
            {
                var i = rowOffset + 1 + (x * bytesPerPixel);
                var t = (byte)((seed[(x + y) % seed.Length] + x + y) & 0xFF);
                raw[i] = (byte)((seed[0] + t) & 0xFF);
                raw[i + 1] = (byte)((seed[1] + (t / 2)) & 0xFF);
                raw[i + 2] = (byte)((seed[2] + (255 - t)) & 0xFF);
            }
        }

        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using (var zlib = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                zlib.Write(raw, 0, raw.Length);
            }

            compressed = ms.ToArray();
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..], height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 2;  // truecolor
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace
        WriteChunk(png, "IHDR", ihdr);

        // Split IDAT if needed but keep single IDAT for simplicity when small.
        WriteChunk(png, "IDAT", compressed);
        WriteChunk(png, "IEND", ReadOnlySpan<byte>.Empty);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthBytes, data.Length);
        stream.Write(lengthBytes);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);

        stream.Write(data);

        var crcInput = new byte[4 + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput.AsSpan(4));
        var crc = Crc32(crcInput);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFF;
    }
}
