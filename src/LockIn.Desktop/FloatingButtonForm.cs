namespace LockIn.Desktop;

using System.Diagnostics;

internal enum TalkButtonState
{
    Ready,
    Listening,
    Working
}

internal sealed class FloatingButtonForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private readonly Button talkButton;
    private readonly System.Windows.Forms.Timer dockingTimer;
    private Point dragOrigin;
    private bool dragging;
    private bool dockEnabled = true;
    private bool alwaysVisible;
    private bool docked;
    private Point freeLocation;
    private TalkButtonState currentState;
    private Color dockedBackground = Color.FromArgb(43, 45, 49);

    public event EventHandler? ToggleRequested;
    public event EventHandler? SettingsRequested;

    public FloatingButtonForm()
    {
        Text = "TalkType";
        Size = new Size(82, 82);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(
            Math.Max(0, Screen.PrimaryScreen!.WorkingArea.Right - Width - 24),
            Math.Max(0, Screen.PrimaryScreen.WorkingArea.Bottom - Height - 24));
        freeLocation = Location;
        BackColor = Color.FromArgb(28, 28, 30);
        Padding = new Padding(6);

        talkButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "Talk",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(44, 44, 48),
            ForeColor = Color.White,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 11, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        talkButton.FlatAppearance.BorderColor = Color.FromArgb(95, 95, 105);
        talkButton.FlatAppearance.BorderSize = 1;
        talkButton.Click += (_, _) => { if (!dragging) ToggleRequested?.Invoke(this, EventArgs.Empty); };
        talkButton.MouseDown += OnDragStart;
        talkButton.MouseMove += OnDragMove;
        talkButton.MouseUp += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Right) SettingsRequested?.Invoke(this, EventArgs.Empty);
            BeginInvoke(() => dragging = false);
        };
        var toolTip = new ToolTip();
        toolTip.SetToolTip(talkButton, "TalkType — click once to listen, click again to transcribe.");
        Controls.Add(talkButton);
        dockingTimer = new System.Windows.Forms.Timer { Interval = 300 };
        dockingTimer.Tick += (_, _) => UpdateDocking();
        dockingTimer.Start();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExNoActivate | WsExToolWindow;
            return parameters;
        }
    }

    public void SetState(TalkButtonState state)
    {
        currentState = state;
        ApplyStateAppearance();
        talkButton.Enabled = state != TalkButtonState.Working;
    }

    private void ApplyStateAppearance()
    {
        if (docked)
        {
            talkButton.Text = currentState switch
            {
                TalkButtonState.Listening => "■",
                TalkButtonState.Working => "…",
                _ => "\uE720"
            };
            talkButton.ForeColor = currentState == TalkButtonState.Listening
                ? Color.FromArgb(242, 63, 67)
                : Color.FromArgb(219, 222, 225);
            talkButton.BackColor = dockedBackground;
            return;
        }

        (talkButton.Text, talkButton.BackColor) = currentState switch
        {
            TalkButtonState.Listening => ("Stop", Color.FromArgb(190, 45, 55)),
            TalkButtonState.Working => ("…", Color.FromArgb(65, 95, 180)),
            _ => ("Talk", Color.FromArgb(44, 44, 48))
        };
        talkButton.ForeColor = Color.White;
    }

    public void SetDockingEnabled(bool enabled)
    {
        dockEnabled = enabled;
        UpdateDocking();
    }

    public void SetAlwaysVisible(bool enabled)
    {
        alwaysVisible = enabled;
        UpdateDocking();
    }

    private void UpdateDocking()
    {
        if (dragging) return;
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == Handle || NativeMethods.IsIconic(foreground))
        {
            ShowOrHideAwayFromMessagingApps();
            return;
        }

        NativeMethods.GetWindowThreadProcessId(foreground, out var processId);
        string processName;
        string title;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;
            title = process.MainWindowTitle;
        }
        catch
        {
            ShowOrHideAwayFromMessagingApps();
            return;
        }

        if (!IsMessagingApp(processName, title))
        {
            ShowOrHideAwayFromMessagingApps();
            return;
        }

        if (!dockEnabled || !NativeMethods.GetWindowRect(foreground, out var rectangle))
        {
            ShowOrHideAwayFromMessagingApps();
            return;
        }

        var isDiscord = processName.Contains("Discord", StringComparison.OrdinalIgnoreCase) ||
            processName.Contains("Vesktop", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Discord", StringComparison.OrdinalIgnoreCase);
        if (!ComposerLocator.TryFind(foreground, isDiscord, out var composer))
        {
            // A Discord/WhatsApp window alone is not enough. Search, settings,
            // pop-outs, and other text fields must never receive the button.
            ShowOrHideAwayFromMessagingApps();
            return;
        }

        if (!docked) freeLocation = Location;
        docked = true;
        // Anchor to the confirmed chat composer, so sidebars, reply previews,
        // and multiline expansion move TalkType correctly.
        var x = composer.Right + 8;
        var y = composer.Top + Math.Max(0, (composer.Height - 34) / 2);
        var safeRight = isDiscord ? rectangle.Right - 277 : rectangle.Right - 126;
        x = Math.Min(x, safeRight);
        var sampledBackground = SampleScreenColor(x - 4, y + 17);
        ApplyDockedAppearance(sampledBackground);
        Location = new Point(x, y);
        // Prepare the final docked size and position before displaying the
        // window. Showing first caused a one-frame flash of the old 82px tile.
        if (!Visible) Show();
    }

    private void ShowOrHideAwayFromMessagingApps()
    {
        if (alwaysVisible)
        {
            Undock();
            if (!Visible) Show();
        }
        else
        {
            // Hide before restoring the optional free-floating appearance.
            // Otherwise Windows can paint the large Talk tile during Alt+Tab.
            if (Visible) Hide();
            Undock();
        }
    }

    private static bool IsMessagingApp(string processName, string title)
    {
        if (processName.Contains("Discord", StringComparison.OrdinalIgnoreCase) ||
            processName.Contains("Vesktop", StringComparison.OrdinalIgnoreCase) ||
            processName.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase)) return true;

        var browser = processName.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
            processName.Equals("msedge", StringComparison.OrdinalIgnoreCase) ||
            processName.Equals("firefox", StringComparison.OrdinalIgnoreCase) ||
            processName.Equals("brave", StringComparison.OrdinalIgnoreCase) ||
            processName.Equals("opera", StringComparison.OrdinalIgnoreCase);
        return browser && (title.Contains("Discord", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase));
    }

    private void Undock()
    {
        if (!docked) return;
        docked = false;
        TransparencyKey = Color.Empty;
        BackColor = Color.FromArgb(28, 28, 30);
        Size = new Size(82, 82);
        Padding = new Padding(6);
        talkButton.Font = new Font(SystemFonts.DefaultFont.FontFamily, 11, FontStyle.Bold);
        talkButton.FlatAppearance.BorderSize = 1;
        talkButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(58, 58, 64);
        ApplyStateAppearance();
        Location = freeLocation;
    }

    private void ApplyDockedAppearance(Color background)
    {
        dockedBackground = background;
        Size = new Size(34, 34);
        Padding = Padding.Empty;
        TransparencyKey = Color.Empty;
        BackColor = background;
        talkButton.BackColor = background;
        talkButton.Font = currentState == TalkButtonState.Ready
            ? new Font("Segoe MDL2 Assets", 14, FontStyle.Regular)
            : new Font(SystemFonts.DefaultFont.FontFamily, 12, FontStyle.Bold);
        talkButton.FlatAppearance.BorderSize = 0;
        talkButton.FlatAppearance.MouseOverBackColor = ControlPaint.Light(background, 0.12f);
        ApplyStateAppearance();
    }

    private static Color SampleScreenColor(int x, int y)
    {
        try
        {
            using var bitmap = new Bitmap(1, 1);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(x, y, 0, 0, new Size(1, 1), CopyPixelOperation.SourceCopy);
            return bitmap.GetPixel(0, 0);
        }
        catch
        {
            return Color.FromArgb(43, 45, 49);
        }
    }

    private void OnDragStart(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left) return;
        // A docked toolbar control is click-only. Treating mouse-down as the
        // beginning of a drag caused it to expand into the standalone button.
        if (docked) return;
        dragOrigin = eventArgs.Location;
        dragging = false;
    }

    private void OnDragMove(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left || docked) return;
        if (Math.Abs(eventArgs.X - dragOrigin.X) + Math.Abs(eventArgs.Y - dragOrigin.Y) > 5) dragging = true;
        if (dragging) Location = new Point(Location.X + eventArgs.X - dragOrigin.X, Location.Y + eventArgs.Y - dragOrigin.Y);
        if (dragging) freeLocation = Location;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) dockingTimer.Dispose();
        base.Dispose(disposing);
    }
}
