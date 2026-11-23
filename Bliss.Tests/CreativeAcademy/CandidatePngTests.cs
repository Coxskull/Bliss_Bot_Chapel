using Bliss.Domain.CreativeAcademy;

namespace Bliss.Tests;

public sealed class CandidatePngTests
{
    [Fact]
    public void Candidate_validation_accepts_pixels_but_rejects_corrupt_truncated_and_oversized_files()
    {
        var pixel = Convert.FromBase64String(Api.AcademyProductionTests.Pixel);
        CandidatePng.Validate(pixel);
        var corrupt = pixel.ToArray(); corrupt[45] ^= 1;
        Assert.Throws<InvalidOperationException>(() => CandidatePng.Validate(corrupt));
        Assert.Throws<InvalidOperationException>(() => CandidatePng.Validate(pixel[..^1]));
        Assert.Throws<InvalidOperationException>(() => CandidatePng.Validate(new byte[CandidatePng.MaxBytes + 1]));
    }

    [Fact]
    public void Paid_generator_png_allows_bounded_larger_file_but_public_upload_stays_one_megabyte()
    {
        var original = Convert.FromBase64String(Api.AcademyProductionTests.Pixel);
        CandidatePng.Validate(original);

        // Extend a structurally valid PNG with a private ancillary chunk,
        // preserving the exact existing compressed scanlines and IEND.
        // This is a synthetic offline fixture, not a paid/generated image.
        var payload = new byte[CandidatePng.MaxBytes + 128];
        Array.Fill(payload, (byte)'A');
        var chunk = new byte[payload.Length + 12];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(0, 4), (uint)payload.Length);
        System.Text.Encoding.ASCII.GetBytes("pILt").CopyTo(chunk, 4);
        payload.CopyTo(chunk, 8);
        var crc = uint.MaxValue;
        foreach (var value in chunk.AsSpan(4, payload.Length + 4))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        }
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(payload.Length + 8, 4), ~crc);

        var largerPng = new byte[original.Length + chunk.Length];
        original.AsSpan(0, original.Length - 12).CopyTo(largerPng);
        chunk.CopyTo(largerPng, original.Length - 12);
        original.AsSpan(original.Length - 12).CopyTo(largerPng.AsSpan(original.Length - 12 + chunk.Length));
        Assert.True(largerPng.Length > CandidatePng.MaxBytes);
        Assert.Throws<InvalidOperationException>(() => CandidatePng.Validate(largerPng));
        CandidatePng.ValidateGenerated(largerPng);

        var corrupt = largerPng.ToArray();
        corrupt[original.Length + 4] ^= 1;
        Assert.Throws<InvalidOperationException>(() => CandidatePng.ValidateGenerated(corrupt));
        Assert.Throws<InvalidOperationException>(() => CandidatePng.ValidateGenerated(new byte[CandidatePng.MaxGeneratedBytes + 1]));
    }
}
