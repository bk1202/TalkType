namespace TalkType.Desktop;

internal sealed class TalkTypeContext : ApplicationContext
{
    private readonly NotifyIcon tray;
    private readonly HotkeyWindow hotkey;
    private readonly WhisperCppTranscriber transcriber = new();
    private readonly EngineManager engine = new();
    private readonly AppSettings settings;
    private readonly SettingsForm settingsForm;
    private readonly FloatingButtonForm floatingButton;
    private ToolStripMenuItem? showEverywhereMenuItem;
    private WaveRecorder? recorder;
    private IntPtr destinationWindow;
    private bool busy;
    private bool previewSession;

    public TalkTypeContext()
    {
        settings = SettingsStore.Load();
        hotkey = new HotkeyWindow();
        var shortcutChanged = EnsureAvailableHotkey();
        tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = $"TalkType — {settings.HotkeyLabel}",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        hotkey.Pressed += async (_, _) => await ToggleAsync();
        settingsForm = new SettingsForm(settings, engine);
        settingsForm.SettingsChanged += (_, _) => ApplySettings();
        settingsForm.ToggleRequested += async (_, _) => await ToggleAsync(true);
        tray.DoubleClick += (_, _) => ShowSettings();
        floatingButton = new FloatingButtonForm();
        floatingButton.SetDockingEnabled(settings.DockToMessagingApps);
        floatingButton.SetAlwaysVisible(settings.AlwaysShowFloatingButton);
        floatingButton.ToggleRequested += async (_, _) => await ToggleAsync();
        floatingButton.SettingsRequested += (_, _) => ShowSettings();
        settingsForm.Show();
        if (shortcutChanged)
        {
            settingsForm.Show();
            MessageBox.Show(settingsForm,
                $"Ctrl+Win+Space is already used by another app, so TalkType switched to {settings.HotkeyLabel}. You can change it in Settings.",
                "Shortcut changed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        Notify("Ready", engine.IsReady
            ? $"Press {settings.HotkeyLabel} to start talking."
            : "Open Settings to download the local speech engine.");
    }

    private bool EnsureAvailableHotkey()
    {
        if (hotkey.TryRegister(settings)) return false;

        var fallbacks = new[]
        {
            new { Key = Keys.Space, Control = true, Alt = true, Shift = false, Windows = false },
            new { Key = Keys.F8, Control = true, Alt = false, Shift = false, Windows = false },
            new { Key = Keys.F9, Control = true, Alt = false, Shift = false, Windows = false }
        };
        foreach (var fallback in fallbacks)
        {
            settings.HotkeyKey = fallback.Key;
            settings.HotkeyControl = fallback.Control;
            settings.HotkeyAlt = fallback.Alt;
            settings.HotkeyShift = fallback.Shift;
            settings.HotkeyWindows = fallback.Windows;
            if (!hotkey.TryRegister(settings)) continue;
            SettingsStore.Save(settings);
            return true;
        }

        throw new InvalidOperationException("TalkType could not reserve any global shortcut. Close other voice-typing apps and try again.");
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Start / stop", null, async (_, _) => await ToggleAsync());
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        showEverywhereMenuItem = new ToolStripMenuItem("Show Talk button everywhere")
        {
            Checked = settings.AlwaysShowFloatingButton,
            CheckOnClick = true
        };
        showEverywhereMenuItem.CheckedChanged += (_, _) =>
        {
            settings.AlwaysShowFloatingButton = showEverywhereMenuItem.Checked;
            SettingsStore.Save(settings);
            floatingButton.SetAlwaysVisible(settings.AlwaysShowFloatingButton);
        };
        menu.Items.Add(showEverywhereMenuItem);
        menu.Items.Add("Open transcript history", null, (_, _) => OpenHistory());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    private async Task ToggleAsync(bool preview = false)
    {
        if (busy) return;
        try
        {
            if (recorder is null)
            {
                if (!engine.IsReady)
                {
                    settingsForm.Show();
                    settingsForm.Activate();
                    Notify("Setup required", "Download the local speech engine first.");
                    return;
                }
                destinationWindow = NativeMethods.GetForegroundWindow();
                previewSession = preview || destinationWindow == settingsForm.Handle;
                recorder = new WaveRecorder();
                recorder.Start();
                floatingButton.SetState(TalkButtonState.Listening);
                settingsForm.SetRecordingState(TalkButtonState.Listening);
                tray.Text = "TalkType — listening…";
                Notify("Listening", $"Press {settings.HotkeyLabel} when you are done.");
                return;
            }

            busy = true;
            floatingButton.SetState(TalkButtonState.Working);
            settingsForm.SetRecordingState(TalkButtonState.Working);
            tray.Text = "TalkType — transcribing…";
            var wavePath = recorder.Stop();
            recorder.Dispose();
            recorder = null;
            try
            {
                var raw = await transcriber.TranscribeAsync(wavePath, settings, CancellationToken.None);
                var text = settings.RemoveFillers ? TranscriptCleaner.Clean(raw) : raw.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    Notify("Nothing heard", "No speech was detected.");
                    settingsForm.ShowFeedback("No speech detected. Check your microphone and try again.");
                    return;
                }
                settingsForm.SetTranscript(text);
                if (!previewSession)
                {
                    if (settings.SaveHistory) SaveHistory(text);
                    PasteIntoDestination(text);
                    Notify("Pasted", "Your transcript is ready in the destination app.");
                }
            }
            finally
            {
                if (File.Exists(wavePath)) File.Delete(wavePath);
            }
        }
        catch (Exception exception)
        {
            recorder?.Dispose();
            recorder = null;
            Notify("TalkType error", exception.Message);
            settingsForm.ShowFeedback("Recording couldn't finish: " + exception.Message);
        }
        finally
        {
            busy = false;
            var state = recorder is null ? TalkButtonState.Ready : TalkButtonState.Listening;
            floatingButton.SetState(state);
            settingsForm.SetRecordingState(state);
            tray.Text = recorder is null ? $"TalkType — {settings.HotkeyLabel}" : "TalkType — listening…";
        }
    }

    private void ApplySettings()
    {
            hotkey.Register(settings);
            tray.Text = $"TalkType — {settings.HotkeyLabel}";
            floatingButton.SetAlwaysVisible(settings.AlwaysShowFloatingButton);
            floatingButton.SetDockingEnabled(settings.DockToMessagingApps);
            if (showEverywhereMenuItem is not null)
                showEverywhereMenuItem.Checked = settings.AlwaysShowFloatingButton;
    }

    private void ShowSettings()
    {
        settingsForm.Show();
        settingsForm.Activate();
    }

    private static void SaveHistory(string text)
    {
        AppPaths.EnsureDirectories();
        File.AppendAllText(AppPaths.HistoryFile, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}]{Environment.NewLine}{text}{Environment.NewLine}{Environment.NewLine}");
    }

    private static void OpenHistory()
    {
        AppPaths.EnsureDirectories();
        if (!File.Exists(AppPaths.HistoryFile)) File.WriteAllText(AppPaths.HistoryFile, "");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppPaths.HistoryFile) { UseShellExecute = true });
    }

    private void PasteIntoDestination(string text)
    {
        Clipboard.SetText(text);
        if (destinationWindow != IntPtr.Zero) NativeMethods.SetForegroundWindow(destinationWindow);
        Thread.Sleep(80);
        const byte control = 0x11;
        const byte v = 0x56;
        const uint keyUp = 0x0002;
        NativeMethods.keybd_event(control, 0, 0, 0);
        NativeMethods.keybd_event(v, 0, 0, 0);
        NativeMethods.keybd_event(v, 0, keyUp, 0);
        NativeMethods.keybd_event(control, 0, keyUp, 0);
    }

    private void Notify(string title, string message) =>
        tray.ShowBalloonTip(2500, title, message, ToolTipIcon.Info);

    protected override void ExitThreadCore()
    {
        recorder?.Dispose();
        settingsForm.Dispose();
        floatingButton.Dispose();
        hotkey.Dispose();
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }
}
