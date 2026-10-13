using System.Buffers.Binary;
using System.IO.Compression;

namespace Bliss.Domain.CreativeAcademy;

/// <summary>Bounded PNG structure and scanline validation, not visual quality assessment.</summary>
public static class CandidatePng
{
    public const int MaxBytes = 1024 * 1024;
    public static void Validate(byte[] bytes)
    {
        if (bytes.Length > MaxBytes || bytes.Length < 45 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            throw new InvalidOperationException("Upload a PNG image of at most 1 MiB.");
        using var compressed = new MemoryStream();
        var offset = 8; var width = 0; var height = 0; var channels = 0; var ended = false;
        while (offset + 12 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            if (length > bytes.Length - offset - 12) throw new InvalidOperationException("PNG chunk is truncated.");
            var size = (int)length;
            var type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
            var crc = uint.MaxValue;
            foreach (var value in bytes.AsSpan(offset + 4, size + 4))
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
            }
            if (~crc != BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + size, 4)))
                throw new InvalidOperationException("PNG chunk checksum is invalid.");
            var payload = bytes.AsSpan(offset + 8, size);
            if (offset == 8 && type != "IHDR") throw new InvalidOperationException("PNG header is missing.");
            if (type == "IHDR")
            {
                if (offset != 8 || size != 13) throw new InvalidOperationException("PNG header is invalid.");
                var w = BinaryPrimitives.ReadUInt32BigEndian(payload[..4]);
                var h = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4));
                if (w is 0 or > 2048 || h is 0 or > 2048 || payload[8] != 8 || payload[10] != 0 || payload[11] != 0 || payload[12] != 0)
                    throw new InvalidOperationException("Use a noninterlaced 8-bit PNG up to 2048 by 2048 pixels.");
                width = (int)w; height = (int)h;
                channels = payload[9] switch { 0 => 1, 2 => 3, 4 => 2, 6 => 4, _ => 0 };
                if (channels == 0) throw new InvalidOperationException("Use grayscale, RGB, grayscale-alpha, or RGBA PNG, without a palette.");
            }
            else if (type == "IDAT") compressed.Write(payload);
            else if (type == "IEND")
            {
                if (size != 0 || offset + 12 != bytes.Length) throw new InvalidOperationException("PNG end is invalid.");
                ended = true; break;
            }
            else if (type.Length != 4 || type[0] is >= 'A' and <= 'Z' && type != "PLTE")
                throw new InvalidOperationException("Unsupported critical PNG chunk.");
            offset += size + 12;
        }
        if (!ended || compressed.Length == 0) throw new InvalidOperationException("PNG image data or end is missing.");
        compressed.Position = 0;
        try
        {
            using var decoded = new ZLibStream(compressed, CompressionMode.Decompress);
            var scanline = new byte[width * channels + 1];
            for (var row = 0; row < height; row++)
            {
                decoded.ReadExactly(scanline);
                if (scanline[0] > 4) throw new InvalidOperationException("PNG scanline filter is invalid.");
            }
            if (decoded.ReadByte() != -1) throw new InvalidOperationException("PNG image dimensions do not match its data.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        { throw new InvalidOperationException("PNG image data is invalid or truncated."); }
    }
}
