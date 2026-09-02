namespace LockIn.Desktop;

internal static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LockIn");
    public static string EngineDirectory => Path.Combine(Root, "engine");
    public static string ModelDirectory => Path.Combine(Root, "models");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string HistoryFile => Path.Combine(Root, "history.txt");
    public static string WhisperExecutable => Path.Combine(EngineDirectory, "whisper-cli.exe");
    public static string DefaultModel => Path.Combine(ModelDirectory, "ggml-small.en-q8_0.bin");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(EngineDirectory);
        Directory.CreateDirectory(ModelDirectory);
    }
}
