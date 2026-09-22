using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Strict PNG subset validator for Phase 6 creative assets.
/// Accepts only IHDR/IDAT/IEND, 8-bit truecolor or truecolor-alpha, non-interlaced,
/// exact canvas dimensions, bounded zlib inflation, and valid filter bytes 0..4.
/// </summary>
public static class WeddingPlannerPngValidator
{
    public const int MaxAssetBytes = 2 * 1024 * 1024;
    public const int MaxAssetsPerPackage = 4;

    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static WeddingPlannerValidatedPng ValidateExactCanvas(byte[] pngBytes, int expectedWidth, int expectedHeight)
    {
        if (pngBytes is null || pngBytes.Length == 0)
        {
            throw new InvalidOperationException("PNG bytes are required.");
        }

        if (pngBytes.Length > MaxAssetBytes)
        {
            throw new InvalidOperationException($"PNG must be ≤ {MaxAssetBytes} bytes.");
        }

        if (pngBytes.Length < 8 || !pngBytes.AsSpan(0, 8).SequenceEqual(Signature))
        {
            throw new InvalidOperationException("PNG signature is invalid.");
        }

        var offset = 8;
        var sawIhdr = false;
        var sawIdat = false;
        var sawIend = false;
        var width = 0;
        var height = 0;
        var colorType = 0;
        var bitDepth = 0;
        var idatChunks = new List<byte[]>();

        while (offset < pngBytes.Length)
        {
            if (sawIend)
            {
                throw new InvalidOperationException("PNG data after IEND is forbidden.");
            }

            if (offset + 12 > pngBytes.Length)
            {
                throw new InvalidOperationException("PNG chunk is truncated.");
            }

            var length = BinaryPrimitives.ReadInt32BigEndian(pngBytes.AsSpan(offset, 4));
            if (length < 0 || length > pngBytes.Length - offset - 12)
            {
                throw new InvalidOperationException("PNG chunk length is invalid.");
            }

            var type = System.Text.Encoding.ASCII.GetString(pngBytes, offset + 4, 4);
            var dataOffset = offset + 8;
            var data = pngBytes.AsSpan(dataOffset, length);
            var storedCrc = BinaryPrimitives.ReadUInt32BigEndian(pngBytes.AsSpan(dataOffset + length, 4));
            var computedCrc = ComputeCrc(pngBytes.AsSpan(offset + 4, 4 + length));
            if (storedCrc != computedCrc)
            {
                throw new InvalidOperationException($"PNG chunk '{type}' has invalid CRC.");
            }

            switch (type)
            {
                case "IHDR":
                    if (sawIhdr || sawIdat || offset != 8)
                    {
                        throw new InvalidOperationException("IHDR must be the first and only IHDR chunk.");
                    }

                    if (length != 13)
                    {
                        throw new InvalidOperationException("IHDR length must be 13.");
                    }

                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    bitDepth = data[8];
                    colorType = data[9];
                    var compression = data[10];
                    var filter = data[11];
                    var interlace = data[12];

                    if (width != expectedWidth || height != expectedHeight)
                    {
                        throw new InvalidOperationException(
                            $"PNG IHDR dimensions must be exactly {expectedWidth}x{expectedHeight}.");
                    }

                    if (bitDepth != 8)
                    {
                        throw new InvalidOperationException("PNG bit depth must be 8.");
                    }

                    if (colorType is not (2 or 6))
                    {
                        throw new InvalidOperationException("PNG color type must be 2 (truecolor) or 6 (truecolor-alpha).");
                    }

                    if (compression != 0 || filter != 0)
                    {
                        throw new InvalidOperationException("PNG compression/filter methods must be 0.");
                    }

                    if (interlace != 0)
                    {
                        throw new InvalidOperationException("PNG must be non-interlaced.");
                    }

                    sawIhdr = true;
                    break;

                case "IDAT":
                    if (!sawIhdr || sawIend)
                    {
                        throw new InvalidOperationException("IDAT must follow IHDR and precede IEND.");
                    }

                    idatChunks.Add(data.ToArray());
                    sawIdat = true;
                    break;

                case "IEND":
                    if (!sawIhdr || !sawIdat || length != 0)
                    {
                        throw new InvalidOperationException("IEND must be last after one or more IDAT chunks and have zero length.");
                    }

                    sawIend = true;
                    break;

                default:
                    throw new InvalidOperationException(
                        $"PNG ancillary/unknown chunk '{type}' is forbidden; only IHDR/IDAT/IEND are allowed.");
            }

            offset = dataOffset + length + 4;
        }

        if (!sawIhdr || !sawIdat || !sawIend || offset != pngBytes.Length)
        {
            throw new InvalidOperationException("PNG must contain IHDR, one or more IDAT, and IEND in order with no trailing bytes.");
        }

        var bytesPerPixel = colorType == 6 ? 4 : 3;
        var expectedInflated = checked(height * (1 + width * bytesPerPixel));
        // Bound inflation to exactly expected size (plus tiny slack for zlib trailer bookkeeping during stream copy).
        var inflated = InflateExact(idatChunks, expectedInflated);
        if (inflated.Length != expectedInflated)
        {
            throw new InvalidOperationException(
                $"PNG IDAT inflation must yield exactly {expectedInflated} bytes.");
        }

        for (var y = 0; y < height; y++)
        {
            var filterByte = inflated[y * (1 + width * bytesPerPixel)];
            if (filterByte > 4)
            {
                throw new InvalidOperationException($"PNG scanline filter byte must be 0..4 (got {filterByte}).");
            }
        }

        var sha = Convert.ToHexString(SHA256.HashData(pngBytes)).ToLowerInvariant();
        return new WeddingPlannerValidatedPng(
            pngBytes,
            pngBytes.Length,
            width,
            height,
            colorType,
            bitDepth,
            sha);
    }

    private static byte[] InflateExact(IReadOnlyList<byte[]> idatChunks, int expectedLength)
    {
        // Concatenate IDAT payloads (zlib stream). Bound total compressed size already by MaxAssetBytes.
        var totalCompressed = idatChunks.Sum(c => c.Length);
        if (totalCompressed == 0)
        {
            throw new InvalidOperationException("PNG IDAT payload is empty.");
        }

        using var compressed = new MemoryStream(totalCompressed);
        foreach (var chunk in idatChunks)
        {
            compressed.Write(chunk, 0, chunk.Length);
        }

        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress, leaveOpen: true);
        using var output = new MemoryStream(expectedLength);
        var buffer = new byte[8192];
        var total = 0;
        while (true)
        {
            var remaining = expectedLength - total;
            if (remaining <= 0)
            {
                // Any extra inflated byte is a failure.
                var peek = zlib.Read(buffer, 0, 1);
                if (peek > 0)
                {
                    throw new InvalidOperationException("PNG IDAT inflation exceeded expected scanline byte count.");
                }

                break;
            }

            var read = zlib.Read(buffer, 0, Math.Min(buffer.Length, remaining));
            if (read <= 0)
            {
                break;
            }

            output.Write(buffer, 0, read);
            total += read;
        }

        if (total != expectedLength)
        {
            throw new InvalidOperationException(
                $"PNG IDAT inflation undersized: expected {expectedLength}, got {total}.");
        }

        return output.ToArray();
    }

    private static uint ComputeCrc(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}

public sealed record WeddingPlannerValidatedPng(
    byte[] Bytes,
    int ByteSize,
    int Width,
    int Height,
    int ColorType,
    int BitDepth,
    string Sha256);
