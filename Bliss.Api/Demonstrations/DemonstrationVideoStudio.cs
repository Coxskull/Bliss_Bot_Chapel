using System.Diagnostics;
using System.Security.Cryptography;
using Bliss.Domain.Demonstrations;
using QRCoder;

namespace Bliss.Api.Demonstrations;

public sealed class DemonstrationVideoStudio
{
    private const string Font = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf";

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public async Task<double> ProbeDurationAsync(string path, CancellationToken cancellationToken)
    {
        var stdout = await RunCaptureAsync(
            "ffprobe",
            [
                "-v", "error",
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
                "-y", "-ss", "0.4", "-i", path,
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
            $"[0:v]drawtext=fontfile={Font}:textfile={marketFile}:fontsize=62:fontcolor=white:x=(w-text_w)/2:y=250,"
            + $"drawtext=fontfile={Font}:textfile={countryFile}:fontsize=28:fontcolor=0xD5E4F7:x=(w-text_w)/2:y=340,"
            + "drawbox=x=180:y=430:w=920:h=10:color=0x2F6FED:t=fill[v]";
        await RunAsync(
            "ffmpeg",
            [
                "-y",
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
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(outputPath)!;
        var stem = Path.Combine(directory, concept.Id + "-" + Guid.NewGuid().ToString("N"));
        var headline = stem + ".h.txt";
        var subhead = stem + ".s.txt";
        var detail = stem + ".d.txt";
        var cta = stem + ".c.txt";
        await File.WriteAllTextAsync(headline, concept.Headline, cancellationToken);
        await File.WriteAllTextAsync(subhead, concept.Subhead, cancellationToken);
        await File.WriteAllTextAsync(detail, concept.Detail, cancellationToken);
        await File.WriteAllTextAsync(cta, concept.CallToAction, cancellationToken);
        var accent = concept.AccentHex;
        var filter =
            "[0:v]scale=1280:720,setsar=1[base];"
            + "[base]drawbox=x=742:y=28:w=508:h=664:color=white@0.95:t=fill[card];"
            + $"[card]drawbox=x=742:y=28:w=18:h=664:color=0x{accent}:t=fill[bar];"
            + $"[bar]drawtext=fontfile={Font}:textfile={headline}:fontsize=40:fontcolor=0x0C3F86:x=786:y=78[h];"
            + $"[h]drawtext=fontfile={Font}:textfile={subhead}:fontsize=28:fontcolor=0x16324F:x=786:y=150[s];"
            + $"[s]drawtext=fontfile={Font}:textfile={detail}:fontsize=22:fontcolor=0x3D5166:x=786:y=230[d];"
            + $"[d]drawtext=fontfile={Font}:textfile={cta}:fontsize=26:fontcolor=0x0E7A45:x=786:y=560[t];"
            + "[1:v]scale=168:168[qr];"
            + "[t][qr]overlay=1020:430[out]";
        await RunAsync(
            "ffmpeg",
            [
                "-y", "-i", sourcePath, "-i", qrPath,
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
        process.StartInfo.FileName = fileName;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = true;
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        byte[] bytes = [];
        string text = string.Empty;
        if (captureBinary)
        {
            using var memory = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(memory, cancellationToken);
            bytes = memory.ToArray();
        }
        else
        {
            text = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        }

        await process.WaitForExitAsync(cancellationToken);
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(fileName + " failed: " + Trim(stderr));
        }

        return new CapturedProcess(process.ExitCode, string.IsNullOrWhiteSpace(text) ? stderr : text, bytes);
    }

    private static string Trim(string value) =>
        value.Length <= 1800 ? value : value[^1800..];

    private sealed record CapturedProcess(int ExitCode, string Text, byte[] Bytes);
}
