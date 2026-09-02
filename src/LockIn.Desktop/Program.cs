namespace LockIn.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            Application.Run(new LockInContext());
        }
        catch (Exception exception)
        {
            try
            {
                AppPaths.EnsureDirectories();
                File.AppendAllText(Path.Combine(AppPaths.Root, "crash.log"),
                    $"[{DateTimeOffset.Now:O}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                // Preserve the original startup failure.
            }
            MessageBox.Show($"TalkType could not start.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "TalkType startup error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
