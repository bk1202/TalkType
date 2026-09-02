using Microsoft.Win32;

namespace LockIn.Desktop;

internal sealed class SettingsForm : Form
{
    private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly AppSettings settings;
    private readonly EngineManager engine;
    private readonly Label status = new() { AutoSize = true };
    private readonly ProgressBar progress = new() { Width = 620, Height = 8, Style = ProgressBarStyle.Continuous };
    private readonly Button install = new() { Text = "Download local speech engine", AutoSize = true };
    private readonly CheckBox removeFillers = new() { Text = "Remove conservative fillers (um, uh, erm)", AutoSize = true };
    private readonly CheckBox saveHistory = new() { Text = "Keep a local transcript history", AutoSize = true };
    private readonly CheckBox launchAtLogin = new() { Text = "Launch TalkType when I sign in", AutoSize = true };
    private readonly CheckBox showFloatingButton = new() { Text = "Show the Talk button everywhere (not only Discord/WhatsApp)", AutoSize = true };
    private readonly CheckBox dockToMessagingApps = new() { Text = "Dock beside Discord and WhatsApp message controls", AutoSize = true };
    private readonly TextBox vocabulary = new() { Multiline = true, Width = 620, Height = 90, ScrollBars = ScrollBars.Vertical };
    private readonly ComboBox key = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly CheckBox control = new() { Text = "Ctrl", AutoSize = true };
    private readonly CheckBox alt = new() { Text = "Alt", AutoSize = true };
    private readonly CheckBox shift = new() { Text = "Shift", AutoSize = true };
    private readonly CheckBox windows = new() { Text = "Win", AutoSize = true };

    public event EventHandler? SettingsChanged;

    public SettingsForm(AppSettings settings, EngineManager engine)
    {
        this.settings = settings;
        this.engine = engine;
        Text = "TalkType";
        Width = 760;
        Height = 820;
        MinimumSize = new Size(720, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(13, 15, 22);
        ForeColor = Color.FromArgb(235, 237, 245);
        Font = new Font("Segoe UI", 9.5f);
        Padding = Padding.Empty;

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

        var shortcutRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent };
        shortcutRow.Controls.AddRange([control, alt, shift, windows, key]);
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var save = new Button { Text = "Save changes", AutoSize = true };
        var close = new Button { Text = "Close", AutoSize = true };
        save.Click += (_, _) => SaveSettings();
        close.Click += (_, _) => Hide();
        buttons.Controls.AddRange([save, close]);
        install.Click += async (_, _) => await InstallAsync();

        var header = new Panel { Dock = DockStyle.Top, Height = 112, BackColor = Color.FromArgb(18, 21, 31), Padding = new Padding(32, 22, 32, 16) };
        var brand = new Label { Text = "TalkType", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 24, FontStyle.Bold), Location = new Point(28, 18) };
        var subtitle = new Label { Text = "Private voice typing that works where you do.", AutoSize = true, ForeColor = Color.FromArgb(157, 164, 184), Location = new Point(32, 67) };
        var privacy = new Label { Text = "  LOCAL • PRIVATE  ", AutoSize = true, ForeColor = Color.FromArgb(186, 176, 255), BackColor = Color.FromArgb(44, 38, 73), Font = new Font("Segoe UI", 8, FontStyle.Bold), Padding = new Padding(4), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(590, 30) };
        header.Controls.AddRange([brand, subtitle, privacy]);

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.FromArgb(13, 15, 22),
            Padding = new Padding(28, 20, 28, 30)
        };
        panel.Controls.Add(Card("Transcription engine", "Runs entirely on this computer. Your recordings are never uploaded.", status, progress, install));
        panel.Controls.Add(Card("Push-to-talk shortcut", "Press once to listen and once more to paste the transcript.", shortcutRow));
        panel.Controls.Add(Card("Writing preferences", "Keep the words yours while removing obvious hesitation.", removeFillers, saveHistory,
            new Label { Text = "Personal vocabulary — one name or term per line", AutoSize = true }, vocabulary));
        panel.Controls.Add(Card("App integrations", "TalkType follows the actual Discord and WhatsApp message composer.", dockToMessagingApps, showFloatingButton));
        panel.Controls.Add(Card("System", "Choose how TalkType behaves when Windows starts.", launchAtLogin));
        panel.Controls.Add(buttons);
        StyleTree(panel);
        StylePrimaryButton(install, false);
        StylePrimaryButton(save, true);
        StylePrimaryButton(close, false);
        vocabulary.BackColor = Color.FromArgb(28, 32, 45);
        vocabulary.ForeColor = Color.White;
        vocabulary.BorderStyle = BorderStyle.FixedSingle;
        key.BackColor = Color.FromArgb(28, 32, 45);
        key.ForeColor = Color.White;
        key.FlatStyle = FlatStyle.Flat;
        Controls.Add(panel);
        Controls.Add(header);
        RefreshEngineStatus();
        FormClosing += (_, eventArgs) => { eventArgs.Cancel = true; Hide(); };
    }

    private async Task InstallAsync()
    {
        install.Enabled = false;
        try
        {
            var reporter = new Progress<SetupProgress>(value =>
            {
                status.Text = value.Message;
                progress.Value = Math.Clamp(value.Percent, 0, 100);
            });
            await engine.InstallAsync(reporter, CancellationToken.None);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Setup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            install.Enabled = true;
            RefreshEngineStatus();
        }
    }

    private void SaveSettings()
    {
        if (!control.Checked && !alt.Checked && !shift.Checked && !windows.Checked)
        {
            MessageBox.Show(this, "Choose at least one modifier key.", "Shortcut", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
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
        SettingsStore.Save(settings);
        SetStartup(settings.LaunchAtLogin);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        MessageBox.Show(this, "Your TalkType settings are saved.", "TalkType", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void RefreshEngineStatus()
    {
        status.Text = engine.IsReady ? "Ready — fast English model, fully local." : "Setup required (approximately 181 MiB).";
        progress.Value = engine.IsReady ? 100 : 0;
        install.Text = engine.IsReady ? "Recheck local engine" : "Download local speech engine";
    }

    private static Panel Card(string title, string description, params Control[] controls)
    {
        var card = new FlowLayoutPanel
        {
            Width = 670,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.FromArgb(22, 25, 36),
            Padding = new Padding(20, 16, 20, 18),
            Margin = new Padding(0, 0, 0, 14)
        };
        card.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold), Margin = new Padding(0, 0, 0, 3) });
        card.Controls.Add(new Label { Text = description, AutoSize = true, ForeColor = Color.FromArgb(148, 155, 177), Margin = new Padding(0, 0, 0, 14) });
        foreach (var control in controls)
        {
            control.Margin = new Padding(0, 3, 0, 6);
            card.Controls.Add(control);
        }
        return card;
    }

    private static void StyleTree(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is CheckBox or Label) control.ForeColor = control.ForeColor == SystemColors.ControlText ? Color.FromArgb(220, 224, 236) : control.ForeColor;
            StyleTree(control);
        }
    }

    private static void StylePrimaryButton(Button button, bool primary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(68, 74, 96);
        button.BackColor = primary ? Color.FromArgb(112, 92, 238) : Color.FromArgb(31, 35, 49);
        button.ForeColor = Color.White;
        button.Padding = new Padding(12, 5, 12, 5);
        button.Cursor = Cursors.Hand;
    }

    private static void SetStartup(bool enabled)
    {
        using var run = Registry.CurrentUser.OpenSubKey(StartupKey, true)
            ?? Registry.CurrentUser.CreateSubKey(StartupKey);
        if (enabled)
            run.SetValue("TalkType", $"\"{Environment.ProcessPath}\"");
        else
        {
            run.DeleteValue("TalkType", false);
            run.DeleteValue("LockIn", false);
        }
    }
}
