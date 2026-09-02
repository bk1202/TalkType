using Microsoft.Win32;

namespace TalkType.Desktop;

internal sealed class SettingsForm : Form
{
    private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static readonly Color Surface = AppTheme.Surface;
    private static readonly Color Muted = AppTheme.Muted;
    private readonly AppSettings settings;
    private readonly EngineManager engine;
    private readonly Label status = Copy("", 11, true);
    private readonly Label recordingStatus = Copy("Try a sentence below. Nothing is sent to another app.");
    private readonly Label shortcutHint = Copy("");
    private readonly Label feedback = Copy("");
    private readonly Label saveFeedback = Copy("");
    private readonly ProgressBar progress = new() { Height = 8 };
    private readonly Button install = ActionButton("Download voice model · 252 MiB", true);
    private readonly Button record = ActionButton("Start test recording", true);
    private readonly Button copy = ActionButton("Copy text");
    private readonly TextBox preview = new()
    {
        Multiline = true, AutoSize = false, Height = 112, MinimumSize = new Size(0, 112), ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        BackColor = AppTheme.Input, ForeColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Latest transcript",
        PlaceholderText = "Your words will appear here after you stop recording."
    };
    private readonly CheckBox removeFillers = Check("Remove fillers like “um” and “uh”");
    private readonly CheckBox saveHistory = Check("Save transcript history on this computer");
    private readonly CheckBox launchAtLogin = Check("Start TalkType when I sign in");
    private readonly CheckBox showFloatingButton = Check("Show the floating microphone in all apps");
    private readonly CheckBox dockToMessagingApps = Check("Show a microphone beside Discord / WhatsApp messages");
    private readonly TextBox vocabulary = new()
    {
        Multiline = true, AutoSize = false, Height = 80, MinimumSize = new Size(0, 80), ScrollBars = ScrollBars.Vertical,
        BackColor = AppTheme.Input, ForeColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Personal vocabulary",
        PlaceholderText = "Names or specialist terms, one per line"
    };
    private readonly ComboBox key = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly CheckBox control = Check("Ctrl");
    private readonly CheckBox alt = Check("Alt");
    private readonly CheckBox shift = Check("Shift");
    private readonly CheckBox windows = Check("Win");
    private readonly FlowLayoutPanel home = Page();
    private readonly FlowLayoutPanel preferences = Page();
    private TalkButtonState recordingState;
    private bool installing;

    public event EventHandler? SettingsChanged;
    public event EventHandler? ToggleRequested;

    public SettingsForm(AppSettings settings, EngineManager engine)
    {
        this.settings = settings;
        this.engine = engine;
        Text = "TalkType";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(760, 740);
        MinimumSize = new Size(580, 540);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppTheme.Background;
        ForeColor = AppTheme.Text;
        Font = new Font("Segoe UI", 10);

        key.Items.AddRange(new object[] { Keys.Space, Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12 });
        key.SelectedItem = settings.HotkeyKey;
        if (key.SelectedIndex < 0) key.SelectedItem = Keys.Space;
        control.Checked = settings.HotkeyControl;
        alt.Checked = settings.HotkeyAlt;
        shift.Checked = settings.HotkeyShift;
        windows.Checked = settings.HotkeyWindows;
        removeFillers.Checked = settings.RemoveFillers;
        saveHistory.Checked = settings.SaveHistory;
        launchAtLogin.Checked = settings.LaunchAtLogin;
        showFloatingButton.Checked = settings.AlwaysShowFloatingButton;
        dockToMessagingApps.Checked = settings.DockToMessagingApps;
        vocabulary.Text = settings.Vocabulary;
        key.BackColor = Surface;
        key.ForeColor = Color.White;
        key.FlatStyle = FlatStyle.Flat;
        copy.Enabled = false;
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(preview.Text); feedback.Text = "Copied. Paste it wherever you need it."; }
            catch (System.Runtime.InteropServices.ExternalException)
            { feedback.Text = "Clipboard is busy. Try Copy text again."; }
        };
        record.Click += (_, _) => ToggleRequested?.Invoke(this, EventArgs.Empty);
        install.Click += async (_, _) => await InstallAsync();

        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 104, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(28, 14, 28, 12), BackColor = Surface
        };
        header.Controls.Add(Copy("TalkType", 26, true));
        header.Controls.Add(Copy("Your voice, your words. Local and private."));

        var navigation = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 58, Padding = new Padding(24, 4, 0, 4) };
        var homeButton = ActionButton("Home", true);
        var preferencesButton = ActionButton("Preferences");
        navigation.Controls.AddRange([homeButton, preferencesButton]);
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(24, 8, 24, 8) };
        var save = ActionButton("Save preferences", true);
        save.Dock = DockStyle.Right;
        save.Visible = false;
        save.Click += (_, _) => SaveSettings();
        saveFeedback.Text = "Closing this window keeps TalkType in the system tray.";
        saveFeedback.Dock = DockStyle.Fill;
        saveFeedback.AutoSize = false;
        footer.Controls.Add(saveFeedback);
        footer.Controls.Add(save);

        void Navigate(bool showPreferences)
        {
            home.Visible = !showPreferences;
            preferences.Visible = showPreferences;
            save.Visible = showPreferences;
            saveFeedback.Text = showPreferences ? "Changes apply when you save." : "Closing this window keeps TalkType in the system tray.";
            homeButton.BackColor = showPreferences ? Surface : AppTheme.Accent;
            preferencesButton.BackColor = showPreferences ? AppTheme.Accent : Surface;
            if (showPreferences) preferences.BringToFront(); else home.BringToFront();
        }
        homeButton.Click += (_, _) => Navigate(false);
        preferencesButton.Click += (_, _) => Navigate(true);

        home.Controls.Add(Card("Type in any text box", "Click a text box, press your shortcut, speak, then press it again.", status, shortcutHint, progress, install));
        home.Controls.Add(Card("Try it here", "Test your microphone without sending a message.", recordingStatus,
            record, preview, copy, feedback));

        var shortcutRow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = Padding.Empty };
        shortcutRow.Controls.AddRange([control, alt, shift, windows, key]);
        preferences.Controls.Add(Card("Keyboard shortcut", "Tap once to start. Tap again to stop — no need to hold it.", shortcutRow));
        preferences.Controls.Add(Card("Writing", "Keep the words yours. Cleanup does not rewrite your message.", removeFillers,
            saveHistory, Copy("Personal vocabulary"), vocabulary));
        preferences.Controls.Add(Card("Microphone button", "Chat positioning is experimental. The keyboard shortcut works independently.",
            dockToMessagingApps, showFloatingButton));
        preferences.Controls.Add(Card("Startup", "Keep TalkType ready in the system tray.", launchAtLogin));

        var content = new Panel { Dock = DockStyle.Fill };
        content.Controls.Add(home);
        content.Controls.Add(preferences);
        Controls.Add(content);
        Controls.Add(footer);
        Controls.Add(navigation);
        Controls.Add(header);
        home.SizeChanged += (_, _) => FitCards(home);
        preferences.SizeChanged += (_, _) => FitCards(preferences);
        Navigate(false);
        RefreshEngineStatus();
        FormClosing += (_, args) => { args.Cancel = true; Hide(); };
    }

    public void SetRecordingState(TalkButtonState state)
    {
        recordingState = state;
        record.Enabled = engine.IsReady && state != TalkButtonState.Working && !installing;
        record.Text = state switch
        {
            TalkButtonState.Listening => "Stop and transcribe",
            TalkButtonState.Working => "Transcribing…",
            _ => "Start test recording"
        };
        record.BackColor = state == TalkButtonState.Listening ? AppTheme.Recording : AppTheme.Accent;
        recordingStatus.Text = state switch
        {
            TalkButtonState.Listening => "Listening — your microphone is on. Click Stop when you are finished.",
            TalkButtonState.Working => "Turning your speech into text on this computer…",
            _ => "Try a sentence below. Nothing is sent to another app."
        };
        record.AccessibleName = record.Text;
    }

    public void SetTranscript(string text)
    {
        preview.Text = text;
        copy.Enabled = !string.IsNullOrWhiteSpace(text);
        feedback.Text = "Review the result before sharing it. This preview lasts only for this session.";
    }

    public void ShowFeedback(string message) => feedback.Text = message;

    private async Task InstallAsync()
    {
        installing = true;
        install.Enabled = false;
        record.Enabled = false;
        string? error = null;
        try
        {
            await engine.InstallAsync(new Progress<SetupProgress>(value =>
            {
                if (IsDisposed) return;
                status.Text = value.Message;
                progress.Value = Math.Clamp(value.Percent, 0, 100);
            }), CancellationToken.None);
        }
        catch (Exception exception) { error = exception.Message; }
        finally
        {
            installing = false;
            if (!IsDisposed)
            {
                install.Enabled = true;
                RefreshEngineStatus();
                if (error is not null)
                {
                    status.Text = "Setup couldn't finish: " + error;
                    install.Text = "Retry download";
                    install.Visible = true;
                }
            }
        }
    }

    private void SaveSettings()
    {
        if (!control.Checked && !alt.Checked && !shift.Checked && !windows.Checked)
        {
            saveFeedback.Text = "Choose Ctrl, Alt, Shift or Win for your shortcut.";
            return;
        }
        var previous = System.Text.Json.JsonSerializer.Serialize(settings);
        var applied = false;
        try
        {
            settings.RemoveFillers = removeFillers.Checked;
            settings.SaveHistory = saveHistory.Checked;
            settings.LaunchAtLogin = launchAtLogin.Checked;
            settings.AlwaysShowFloatingButton = showFloatingButton.Checked;
            settings.DockToMessagingApps = dockToMessagingApps.Checked;
            settings.Vocabulary = vocabulary.Text.Trim();
            settings.HotkeyKey = (Keys)(key.SelectedItem ?? Keys.Space);
            settings.HotkeyControl = control.Checked;
            settings.HotkeyAlt = alt.Checked;
            settings.HotkeyShift = shift.Checked;
            settings.HotkeyWindows = windows.Checked;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            applied = true;
            SettingsStore.Save(settings);
            SetStartup(settings.LaunchAtLogin);
            RefreshEngineStatus();
            saveFeedback.Text = "Preferences saved.";
        }
        catch (Exception exception)
        {
            if (!applied)
            {
                var original = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(previous)!;
                foreach (var property in typeof(AppSettings).GetProperties().Where(property => property.CanWrite))
                    property.SetValue(settings, property.GetValue(original));
            }
            saveFeedback.Text = "Couldn't save: " + exception.Message;
        }
    }

    private void RefreshEngineStatus()
    {
        status.Text = engine.IsReady ? "Ready · audio stays on this computer" : "One quick setup, then you can work offline.";
        progress.Visible = !engine.IsReady;
        progress.Value = engine.IsReady ? 100 : 0;
        install.Visible = !engine.IsReady;
        shortcutHint.Text = $"Your shortcut: {settings.HotkeyLabel}    •    Change it in Preferences";
        SetRecordingState(recordingState);
    }

    private static Label Copy(string text, float size = 10, bool strong = false) => new()
    {
        Text = text, AutoSize = true, ForeColor = strong ? Color.White : Muted,
        Font = new Font("Segoe UI", size, strong ? FontStyle.Bold : FontStyle.Regular),
        Margin = new Padding(0, 2, 0, 6)
    };

    private static CheckBox Check(string text) => new()
    { Text = text, AutoSize = true, ForeColor = AppTheme.Text, Margin = new Padding(0, 5, 14, 8) };

    private static Button ActionButton(string text, bool primary = false)
    {
        var button = new Button
        {
            Text = text, AccessibleName = text, AutoSize = true, MinimumSize = new Size(100, 38),
            FlatStyle = FlatStyle.Flat, BackColor = primary ? AppTheme.Accent : Surface,
            ForeColor = Color.White, Padding = new Padding(12, 4, 12, 4), Cursor = Cursors.Hand,
            Margin = new Padding(0, 4, 12, 10)
        };
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = AppTheme.Border;
        return button;
    }

    private static FlowLayoutPanel Page() => new()
    {
        Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown,
        WrapContents = false, Padding = new Padding(24, 8, 24, 12)
    };

    private static FlowLayoutPanel Card(string title, string description, params Control[] controls)
    {
        var card = new FlowLayoutPanel
        {
            Width = 680, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Surface,
            Padding = new Padding(20, 12, 20, 12), Margin = new Padding(0, 0, 0, 14)
        };
        card.Controls.Add(Copy(title, 13, true));
        card.Controls.Add(Copy(description));
        foreach (var item in controls) card.Controls.Add(item);
        return card;
    }

    private static void FitCards(FlowLayoutPanel page)
    {
        var width = Math.Max(280, page.ClientSize.Width - page.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        page.SuspendLayout();
        foreach (FlowLayoutPanel card in page.Controls)
        {
            card.MinimumSize = new Size(width, 0);
            card.MaximumSize = new Size(width, 0);
            var inner = width - card.Padding.Horizontal;
            foreach (Control item in card.Controls)
            {
                item.MaximumSize = new Size(inner, item is TextBox ? item.MinimumSize.Height : item is ProgressBar ? 8 : 0);
                if (item is TextBox or ProgressBar or FlowLayoutPanel) item.Width = inner;
            }
        }
        page.ResumeLayout(true);
    }

    private static void SetStartup(bool enabled)
    {
        using var run = Registry.CurrentUser.OpenSubKey(StartupKey, true) ?? Registry.CurrentUser.CreateSubKey(StartupKey);
        if (enabled) run.SetValue("TalkType", $"\"{Environment.ProcessPath}\"");
        else { run.DeleteValue("TalkType", false); run.DeleteValue("LockIn", false); }
    }
}
