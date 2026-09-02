using System.Diagnostics;

namespace LockIn.Desktop;

internal sealed class WhisperCppTranscriber
{
    public async Task<string> TranscribeAsync(string wavePath, AppSettings settings, CancellationToken cancellationToken)
    {
        var executable = Environment.GetEnvironmentVariable("LOCKIN_WHISPER_EXE") ?? AppPaths.WhisperExecutable;
        var model = Environment.GetEnvironmentVariable("LOCKIN_WHISPER_MODEL") ?? AppPaths.DefaultModel;
        if (!File.Exists(executable))
            throw new FileNotFoundException("Open Settings and download the local speech engine.", executable);
        if (!File.Exists(model))
            throw new FileNotFoundException("Open Settings and download the local accuracy model.", model);

        var outputBase = Path.ChangeExtension(wavePath, null);
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add(model);
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add(wavePath);
        startInfo.ArgumentList.Add("-otxt");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add(outputBase);
        startInfo.ArgumentList.Add("--no-timestamps");
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add(settings.Language);
        var vocabulary = settings.Vocabulary.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (vocabulary.Length > 0)
        {
            startInfo.ArgumentList.Add("--prompt");
            startInfo.ArgumentList.Add(string.Join(", ", vocabulary));
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start whisper.cpp.");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new InvalidOperationException((await process.StandardError.ReadToEndAsync(cancellationToken)).Trim());

        var transcriptPath = outputBase + ".txt";
        try { return (await File.ReadAllTextAsync(transcriptPath, cancellationToken)).Trim(); }
        finally { if (File.Exists(transcriptPath)) File.Delete(transcriptPath); }
    }
}
