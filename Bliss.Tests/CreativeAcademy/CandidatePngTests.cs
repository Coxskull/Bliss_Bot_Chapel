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
}
