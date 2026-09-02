using System.Text.Json;

namespace LockIn.Desktop;

internal sealed class AppSettings
{
    public bool RemoveFillers { get; set; } = true;
    public bool SaveHistory { get; set; } = true;
    public bool LaunchAtLogin { get; set; }
    public bool ShowFloatingButton { get; set; } = true;
    public bool AlwaysShowFloatingButton { get; set; }
    public bool DockToMessagingApps { get; set; } = true;
    public string Language { get; set; } = "en";
    public string Vocabulary { get; set; } = "";
    public Keys HotkeyKey { get; set; } = Keys.Space;
    public bool HotkeyControl { get; set; } = true;
    public bool HotkeyAlt { get; set; }
    public bool HotkeyShift { get; set; }
    public bool HotkeyWindows { get; set; } = true;

    public uint HotkeyModifiers =>
        (HotkeyControl ? NativeMethods.ModControl : 0) |
        (HotkeyAlt ? NativeMethods.ModAlt : 0) |
        (HotkeyShift ? NativeMethods.ModShift : 0) |
        (HotkeyWindows ? NativeMethods.ModWin : 0);

    public string HotkeyLabel
    {
        get
        {
            var parts = new List<string>();
            if (HotkeyControl) parts.Add("Ctrl");
            if (HotkeyAlt) parts.Add("Alt");
            if (HotkeyShift) parts.Add("Shift");
            if (HotkeyWindows) parts.Add("Win");
            parts.Add(HotkeyKey.ToString());
            return string.Join('+', parts);
        }
    }
}

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        AppPaths.EnsureDirectories();
        try
        {
            return File.Exists(AppPaths.SettingsFile)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), JsonOptions) ?? new()
                : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public static void Save(AppSettings settings)
    {
        AppPaths.EnsureDirectories();
        File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
