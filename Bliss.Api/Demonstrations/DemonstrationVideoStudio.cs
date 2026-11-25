using System.Diagnostics;
using System.Security.Cryptography;
using Bliss.Domain.Demonstrations;
using QRCoder;

namespace Bliss.Api.Demonstrations;

public sealed class DemonstrationVideoStudio
{
    private const string Font = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf";
    // Decoder inputs are untrusted. Hard caps reduce resource exhaustion risk.
    private static readonly TimeSpan MediaProcessDeadline = TimeSpan.FromSeconds(180);
    private const int MaxOutputBytes = 64 * 1024;
    private const int MaxErrorBytes = 32 * 1024;

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public async Task<double> ProbeDurationAsync(string path, CancellationToken cancellationToken)
    {
        var stdout = await RunCaptureAsync(
            "ffprobe",
            [
                "-v", "error",
                "-protocol_whitelist", "file,pipe",
                "-show_entries", "format=duration",
                "-of", "default=nw=1:nk=1",
                path
            ],
            captureBinary: false,
            cancellationToken);
        return double.TryParse(stdout.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            ? seconds
            : 0;
    }

    public async Task<string> FrameHashAsync(string path, CancellationToken cancellationToken)
    {
        var captured = await RunCaptureAsync(
            "ffmpeg",
            [
                "-nostdin", "-loglevel", "error", "-y", "-ss", "0.4",
                "-protocol_whitelist", "file,pipe", "-i", path,
                "-frames:v", "1",
                "-vf", "scale=8:8,format=gray",
                "-f", "rawvideo",
                "pipe:1"
            ],
            captureBinary: true,
            cancellationToken);
        return SourceMediaRules.AverageHash(captured.Bytes);
    }

    public async Task CreateStudioSliceAsync(
        string outputPath,
        string market,
        string country,
        int seconds,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(outputPath)!;
        var marketFile = Path.Combine(directory, Path.GetFileName(outputPath) + ".market.txt");
        var countryFile = Path.Combine(directory, Path.GetFileName(outputPath) + ".country.txt");
        await File.WriteAllTextAsync(marketFile, market, cancellationToken);
        await File.WriteAllTextAsync(countryFile, country + " · source slice", cancellationToken);
        var filter =
            $"[0:v]drawtext=fontfile={Font}:textfile={marketFile}:fontsize=52:fontcolor=white:x=64:y=250,"
            + $"drawtext=fontfile={Font}:textfile={countryFile}:fontsize=26:fontcolor=0xD5E4F7:x=64:y=330,"
            + "drawbox=x=64:y=400:w=520:h=10:color=0x2F6FED:t=fill[v]";
        await RunAsync(
            "ffmpeg",
            [
                "-nostdin", "-loglevel", "error", "-y",
                "-f", "lavfi", "-i", $"color=c=0x10233F:s=1280x720:d={seconds}",
                "-filter_complex", filter,
                "-map", "[v]",
                "-an",
                "-c:v", "libx264",
                "-preset", "ultrafast",
                "-pix_fmt", "yuv420p",
                "-movflags", "+faststart",
                outputPath
            ],
            cancellationToken);
    }

    public byte[] CreateQrPng(string destination)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(destination, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(10);
    }

    public async Task CompositeAsync(
        string sourcePath,
        string qrPath,
        string outputPath,
        OverlayConcept concept,
        string businessName,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(outputPath)!;
        var stem = Path.Combine(directory, concept.Id + "-" + Guid.NewGuid().ToString("N"));
        var brand = stem + ".b.txt";
        var headline = stem + ".h.txt";
        var subhead = stem + ".s.txt";
        var detail = stem + ".d.txt";
        var cta = stem + ".c.txt";
        await File.WriteAllTextAsync(brand, businessName, cancellationToken);
        await File.WriteAllTextAsync(headline, concept.Headline, cancellationToken);
        await File.WriteAllTextAsync(subhead, concept.Subhead, cancellationToken);
        await File.WriteAllTextAsync(detail, concept.Detail, cancellationToken);
        await File.WriteAllTextAsync(cta, concept.CallToAction, cancellationToken);
        var accent = concept.AccentHex;
        var filter =
            "[0:v]scale=1280:720,setsar=1[base];"
            + "[base]drawbox=x=760:y=36:w=484:h=648:color=white@0.96:t=fill[card];"
            + $"[card]drawbox=x=760:y=36:w=16:h=648:color=0x{accent}:t=fill[bar];"
            + $"[bar]drawtext=fontfile={Font}:textfile={brand}:fontsize=22:fontcolor=0x{accent}:x=796:y=64[b];"
            + $"[b]drawtext=fontfile={Font}:textfile={headline}:fontsize=36:fontcolor=0x0C3F86:x=796:y=110[h];"
            + $"[h]drawtext=fontfile={Font}:textfile={subhead}:fontsize=26:fontcolor=0x16324F:x=796:y=168[s];"
            + $"[s]drawtext=fontfile={Font}:textfile={detail}:fontsize=20:fontcolor=0x3D5166:x=796:y=230[d];"
            + $"[d]drawtext=fontfile={Font}:textfile={cta}:fontsize=24:fontcolor=0x0E7A45:x=796:y=560[t];"
            + "[1:v]scale=150:150[qr];"
            + "[t][qr]overlay=1048:470[out]";
        await RunAsync(
            "ffmpeg",
            [
                "-nostdin", "-loglevel", "error", "-y",
                "-protocol_whitelist", "file,pipe", "-i", sourcePath,
                "-protocol_whitelist", "file,pipe", "-i", qrPath,
                "-filter_complex", filter,
                "-map", "[out]",
                "-an",
                "-c:v", "libx264",
                "-preset", "ultrafast",
                "-pix_fmt", "yuv420p",
                "-movflags", "+faststart",
                outputPath
            ],
            cancellationToken);
    }

    private static async Task RunAsync(string fileName, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var result = await RunCaptureAsync(fileName, args, captureBinary: false, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(fileName + " failed: " + Trim(result.Text));
        }
    }

    private static async Task<CapturedProcess> RunCaptureAsync(
        string fileName,
        IReadOnlyList<string> args,
        bool captureBinary,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(MediaProcessDeadline);
        process.StartInfo.FileName = fileName;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = true;
        var started = false;
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        try
        {
            process.Start();
            started = true;
            // Concurrent limited pipe draining prevents a decoder from growing
            // application memory without bounds or blocking on a full stderr.
            var stderrTask = ReadBoundedAsync(process.StandardError.BaseStream, MaxErrorBytes, deadline.Token);
            var stdoutTask = ReadBoundedAsync(process.StandardOutput.BaseStream, MaxOutputBytes, deadline.Token);
            await Task.WhenAll(stderrTask, stdoutTask, process.WaitForExitAsync(deadline.Token));
            var stderr = System.Text.Encoding.UTF8.GetString(await stderrTask);
            if (process.ExitCode != 0)
                throw new InvalidOperationException(fileName + " failed: " + Trim(stderr));

            var output = await stdoutTask;
            var text = captureBinary ? string.Empty : System.Text.Encoding.UTF8.GetString(output);
            return new CapturedProcess(process.ExitCode, string.IsNullOrWhiteSpace(text) ? stderr : text,
                captureBinary ? output : []);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Media processing exceeded its execution deadline.");
        }
        finally
        {
            if (started && !process.HasExited)
            {
                // WaitForExitAsync cancellation does not stop the child process.
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { /* exited concurrently */ }
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int limit, CancellationToken token)
    {
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), token);
            if (read == 0) break;
            if (output.Length + read > limit)
                throw new InvalidOperationException("Media processing output exceeded its safety limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static string Trim(string value) =>
        value.Length <= 1800 ? value : value[^1800..];

    private sealed record CapturedProcess(int ExitCode, string Text, byte[] Bytes);
}
