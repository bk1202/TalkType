namespace TalkType.Desktop;

using System.Diagnostics;
using System.Drawing.Drawing2D;

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
        Size = new Size(50, 50);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(
            Math.Max(0, Screen.PrimaryScreen!.WorkingArea.Right - Width - 24),
            Math.Max(0, Screen.PrimaryScreen.WorkingArea.Bottom - Height - 24));
        freeLocation = Location;
        BackColor = Color.FromArgb(24, 27, 39);
        Padding = Padding.Empty;

        talkButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = string.Empty,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(24, 27, 39),
            ForeColor = Color.White,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 11, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        talkButton.FlatAppearance.BorderColor = Color.FromArgb(126, 105, 255);
        talkButton.FlatAppearance.BorderSize = 1;
        talkButton.Paint += DrawTalkIcon;
        talkButton.Click += (_, _) => { if (!dragging) ToggleRequested?.Invoke(this, EventArgs.Empty); };
        talkButton.MouseDown += OnDragStart;
        talkButton.MouseMove += OnDragMove;
        talkButton.MouseUp += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Right) SettingsRequested?.Invoke(this, EventArgs.Empty);
            BeginInvoke(() => dragging = false);
        };
        Controls.Add(talkButton);
        ApplyFloatingAppearance();
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
    }

    private void ApplyStateAppearance()
    {
        if (docked)
        {
            talkButton.Text = string.Empty;
            talkButton.ForeColor = currentState == TalkButtonState.Listening
                ? Color.FromArgb(242, 63, 67)
                : Color.FromArgb(196, 181, 253);
            talkButton.BackColor = dockedBackground;
            talkButton.Invalidate();
            return;
        }

        talkButton.Text = string.Empty;
        talkButton.BackColor = currentState switch
        {
            TalkButtonState.Listening => Color.FromArgb(68, 29, 39),
            TalkButtonState.Working => Color.FromArgb(31, 43, 78),
            _ => Color.FromArgb(24, 27, 39)
        };
        talkButton.ForeColor = currentState == TalkButtonState.Listening
            ? Color.FromArgb(255, 104, 117)
            : Color.FromArgb(196, 181, 253);
        talkButton.Invalidate();
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
        ApplyFloatingAppearance();
        Location = freeLocation;
    }

    private void ApplyFloatingAppearance()
    {
        Size = new Size(50, 50);
        Padding = Padding.Empty;
        BackColor = Color.FromArgb(24, 27, 39);
        Region?.Dispose();
        using (var circle = new GraphicsPath())
        {
            circle.AddEllipse(ClientRectangle);
            Region = new Region(circle);
        }
        talkButton.FlatAppearance.BorderColor = Color.FromArgb(126, 105, 255);
        talkButton.FlatAppearance.BorderSize = 1;
        talkButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(39, 43, 61);
        ApplyStateAppearance();
    }

    private void ApplyDockedAppearance(Color background)
    {
        dockedBackground = background;
        Size = new Size(34, 34);
        Padding = Padding.Empty;
        Region?.Dispose();
        Region = null;
        TransparencyKey = Color.Empty;
        BackColor = background;
        talkButton.BackColor = background;
        talkButton.Font = new Font(SystemFonts.DefaultFont.FontFamily, 12, FontStyle.Bold);
        talkButton.FlatAppearance.BorderSize = 0;
        talkButton.FlatAppearance.MouseOverBackColor = ControlPaint.Light(background, 0.12f);
        ApplyStateAppearance();
    }

    private void DrawTalkIcon(object? sender, PaintEventArgs eventArgs)
    {
        var graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var offsetX = (talkButton.ClientSize.Width - 34) / 2f;
        var offsetY = (talkButton.ClientSize.Height - 34) / 2f;
        var color = currentState == TalkButtonState.Listening
            ? Color.FromArgb(255, 104, 117)
            : Color.FromArgb(196, 181, 253);

        if (currentState == TalkButtonState.Working)
        {
            using var brush = new SolidBrush(color);
            for (var index = 0; index < 3; index++)
                graphics.FillEllipse(brush, offsetX + 10 + index * 6, offsetY + 15, 3.5f, 3.5f);
            return;
        }

        if (currentState == TalkButtonState.Listening)
        {
            using var brush = new SolidBrush(color);
            graphics.FillRectangle(brush, offsetX + 12, offsetY + 12, 10, 10);
            return;
        }

        using var pen = new Pen(color, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var microphone = new GraphicsPath();
        microphone.AddArc(offsetX + 13, offsetY + 7, 8, 8, 180, 180);
        microphone.AddLine(offsetX + 21, offsetY + 11, offsetX + 21, offsetY + 17);
        microphone.AddArc(offsetX + 13, offsetY + 13, 8, 8, 0, 180);
        microphone.AddLine(offsetX + 13, offsetY + 17, offsetX + 13, offsetY + 11);
        graphics.DrawPath(pen, microphone);
        graphics.DrawArc(pen, offsetX + 10, offsetY + 12, 14, 12, 0, 180);
        graphics.DrawLine(pen, offsetX + 17, offsetY + 24, offsetX + 17, offsetY + 27);
        graphics.DrawLine(pen, offsetX + 13, offsetY + 27, offsetX + 21, offsetY + 27);
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
        if (disposing)
        {
            dockingTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
