using Bliss.Api.Demonstrations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bliss.Tests;

public sealed class DemonstrationMediaUploadLimitsTests
{
    [Fact]
    public void SourceUploadHasBoundedSizeBeforeCopyingIntoMemory()
    {
        Assert.Equal(32 * 1024 * 1024, ProspectDemonstrationService.MaxUploadedClipBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(33554433)]
    public async Task EmptyOrOversizeUploadIsRejectedBeforeAnyDiskOrDecoderAccess(int fileLength)
    {
        // Every dependency is intentionally null: the controller MUST reject
        // the upload from metadata before it opens or reads the input stream.
        var controller = new DemonstrationsController(
            null!, null!, null!, null!, null!, null!);
        var formFile = new FormFile(Stream.Null, 0, fileLength, "file", "untrusted.mp4");

        var result = await controller.Upload(formFile, "Manila", "Philippines", null, CancellationToken.None);
        if (fileLength == 0)
        {
            Assert.IsType<BadRequestObjectResult>(result);
        }
        else
        {
            var tooLarge = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status413PayloadTooLarge, tooLarge.StatusCode);
        }
    }

    [Fact]
    public async Task OversizeBytesAreRejectedBeforeServiceUsesTheFileStore()
    {
        var service = new ProspectDemonstrationService(null!, new DemonstrationVideoStudio());
        var overLimit = new byte[ProspectDemonstrationService.MaxUploadedClipBytes + 1];

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveUploadAsync(
                "untrusted.mp4", overLimit, "Manila", "Philippines", "",
                "synthetic test", CancellationToken.None));
        Assert.Contains("32 MiB", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyBytesAreRejectedBeforeServiceUsesTheFileStore()
    {
        var service = new ProspectDemonstrationService(null!, new DemonstrationVideoStudio());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveUploadAsync(
                "empty.mp4", [], "Manila", "Philippines", "",
                "synthetic test", CancellationToken.None));
    }
}
