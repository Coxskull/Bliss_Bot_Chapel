using System.Diagnostics;
using Bliss.Api.Demonstrations;

namespace Bliss.Tests;

public sealed class DemonstrationVideoStudioProtocolTests
{
    [Fact]
    public async Task LocalSourceVideoStillSupportsDurationAndFrameHash()
    {
        var temporary = Path.Combine(Path.GetTempPath(), "agm-local-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        var input = Path.Combine(temporary, "synthetic.mp4");
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    UseShellExecute = false,
                    RedirectStandardError = true
                }
            };
            foreach (var argument in new[]
            {
                "-hide_banner", "-loglevel", "error", "-nostdin", "-y",
                "-f", "lavfi", "-i", "color=c=blue:s=64x64:d=1",
                "-c:v", "mpeg4", input
            })
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            Assert.True(process.Start());
            var errorRead = process.StandardError.ReadToEndAsync(cancellation.Token);
            await process.WaitForExitAsync(cancellation.Token);
            Assert.True(process.ExitCode == 0, await errorRead);

            var studio = new DemonstrationVideoStudio();
            var duration = await studio.ProbeDurationAsync(input, cancellation.Token);
            Assert.InRange(duration, 0.6, 1.5);
            var hash = await studio.FrameHashAsync(input, cancellation.Token);
            Assert.False(string.IsNullOrWhiteSpace(hash));
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public async Task AsyncRistNetworkProtocolIsRefusedBeforeAStreamCanBeOpened()
    {
        // The target is synthetic loopback; successful behavior is an immediate
        // local denial by FFprobe's file/pipe-only input protocol whitelist.
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var studio = new DemonstrationVideoStudio();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => studio.ProbeDurationAsync("async:rist://127.0.0.1:65535", cancellation.Token));
        Assert.Contains("failed", failure.Message, StringComparison.OrdinalIgnoreCase);
    }
}
