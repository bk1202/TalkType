using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace LockIn.Desktop;

internal sealed class EngineManager
{
    private const string ReleasesApi = "https://api.github.com/repos/ggml-org/whisper.cpp/releases/latest";
    private const string ModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en-q5_1.bin";
    private const string ModelSha1 = "20f54878d608f94e4a8ee3ae56016571d47cba34";
    private static readonly HttpClient Client = CreateClient();

    public bool IsReady => File.Exists(AppPaths.WhisperExecutable) && File.Exists(AppPaths.DefaultModel);

    public async Task InstallAsync(IProgress<SetupProgress> progress, CancellationToken cancellationToken)
    {
        AppPaths.EnsureDirectories();
        if (!File.Exists(AppPaths.WhisperExecutable))
            await InstallEngineAsync(progress, cancellationToken);
        if (!File.Exists(AppPaths.DefaultModel))
            await InstallModelAsync(progress, cancellationToken);
        progress.Report(new("Ready", 100));
    }

    private static async Task InstallEngineAsync(IProgress<SetupProgress> progress, CancellationToken cancellationToken)
    {
        progress.Report(new("Finding the latest speech engine…", 0));
        var release = await Client.GetFromJsonAsync<GitHubRelease>(ReleasesApi, cancellationToken)
            ?? throw new InvalidOperationException("The speech-engine release could not be found.");
        var asset = release.Assets.FirstOrDefault(item => item.Name.Equals("whisper-bin-x64.zip", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The Windows x64 speech engine is unavailable.");
        var archive = Path.Combine(AppPaths.Root, "engine.zip");
        try
        {
            await DownloadAsync(asset.DownloadUrl, archive, "Downloading speech engine", 0, 10, progress, cancellationToken);
            ZipFile.ExtractToDirectory(archive, AppPaths.EngineDirectory, true);
            var executable = Directory.EnumerateFiles(AppPaths.EngineDirectory, "whisper-cli.exe", SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new InvalidDataException("The speech-engine archive did not contain whisper-cli.exe.");
            if (!Path.GetFullPath(executable).Equals(Path.GetFullPath(AppPaths.WhisperExecutable), StringComparison.OrdinalIgnoreCase))
            {
                var sourceDirectory = Path.GetDirectoryName(executable)!;
                foreach (var file in Directory.EnumerateFiles(sourceDirectory))
                    File.Copy(file, Path.Combine(AppPaths.EngineDirectory, Path.GetFileName(file)), true);
            }
        }
        finally
        {
            if (File.Exists(archive)) File.Delete(archive);
        }
    }

    private static async Task InstallModelAsync(IProgress<SetupProgress> progress, CancellationToken cancellationToken)
    {
        var partial = AppPaths.DefaultModel + ".download";
        try
        {
            await DownloadAsync(ModelUrl, partial, "Downloading fast English model", 10, 88, progress, cancellationToken);
            progress.Report(new("Verifying model…", 99));
            string actual;
            await using (var stream = File.OpenRead(partial))
            {
                actual = Convert.ToHexString(await SHA1.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
            }
            if (!actual.Equals(ModelSha1, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded model failed its integrity check.");
            File.Move(partial, AppPaths.DefaultModel, true);
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }

    private static async Task DownloadAsync(string url, string path, string label, int start, int range,
        IProgress<SetupProgress> progress, CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var length = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(path);
        var buffer = new byte[1024 * 128];
        long received = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            received += count;
            var percent = length is > 0 ? start + (int)(received * range / length.Value) : start;
            var amount = length is > 0 ? $" ({received / 1048576} / {length.Value / 1048576} MiB)" : "";
            progress.Report(new(label + amount, Math.Min(start + range, percent)));
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromHours(2) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TalkType/0.1");
        return client;
    }

    private sealed record GitHubRelease([property: JsonPropertyName("assets")] GitHubAsset[] Assets);
    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string DownloadUrl);
}

internal sealed record SetupProgress(string Message, int Percent);
