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
    private Color chatBackground = AppTheme.Surface;

    public event EventHandler? ToggleRequested;
    public event EventHandler? SettingsRequested;

    public FloatingButtonForm()
    {
        Text = "TalkType";
        Size = new Size(124, 44);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(
            Math.Max(0, Screen.PrimaryScreen!.WorkingArea.Right - Width - 24),
            Math.Max(0, Screen.PrimaryScreen.WorkingArea.Bottom - Height - 24));
        freeLocation = Location;
        BackColor = AppTheme.Surface;
        Padding = Padding.Empty;

        talkButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = string.Empty,
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.Surface,
            ForeColor = Color.White,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 11, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        talkButton.FlatAppearance.BorderColor = AppTheme.AccentLight;
        talkButton.FlatAppearance.BorderSize = 0;
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
        talkButton.Text = string.Empty;
        talkButton.AccessibleName = currentState switch
        {
            TalkButtonState.Listening => "Stop recording and transcribe",
            TalkButtonState.Working => "Transcribing",
            _ => "TalkType: click to start recording"
        };
        talkButton.BackColor = docked ? chatBackground : currentState switch
        {
            TalkButtonState.Listening => Color.FromArgb(60, 24, 24),
            TalkButtonState.Working => Color.FromArgb(18, 42, 62),
            _ => AppTheme.Surface
        };
        talkButton.ForeColor = currentState == TalkButtonState.Listening
            ? AppTheme.RecordingLight
            : docked ? Color.White : AppTheme.AccentLight;
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

        if (!dockEnabled)
        {
            ShowOrHideAwayFromMessagingApps();
            return;
        }

        var isDiscord = processName.Contains("Discord", StringComparison.OrdinalIgnoreCase) ||
            processName.Contains("Vesktop", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Discord", StringComparison.OrdinalIgnoreCase);
        if (!ComposerLocator.TryFindMicBounds(foreground, isDiscord, out var buttonBounds))
        {
            // A Discord/WhatsApp window alone is not enough. Search, settings,
            // pop-outs, and other text fields must never receive the button.
            // No safe slot: don't substitute the global button over this app.
            if (Visible) Hide();
            return;
        }

        if (!docked || Bounds != buttonBounds)
        {
            if (Visible) Hide();
            if (!docked) freeLocation = Location;
            chatBackground = SampleBackground(buttonBounds);
            docked = true;
            ApplyDockedAppearance();
            Bounds = buttonBounds;
        }
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
        ApplyButtonAppearance(new Size(124, 44));
    }

    private void ApplyButtonAppearance(Size size)
    {
        Size = size;
        Padding = Padding.Empty;
        BackColor = AppTheme.Surface;
        var oldRegion = Region;
        using var shape = Capsule(new RectangleF(0, 0, Width, Height));
        Region = new Region(shape);
        oldRegion?.Dispose();
        talkButton.FlatAppearance.BorderColor = AppTheme.AccentLight;
        talkButton.FlatAppearance.BorderSize = 0;
        talkButton.FlatAppearance.MouseOverBackColor = AppTheme.Hover;
        talkButton.FlatAppearance.MouseDownBackColor = AppTheme.Hover;
        ApplyStateAppearance();
    }

    private void ApplyDockedAppearance()
    {
        ApplyButtonAppearance(new Size(ChatMicPlacement.Width, ChatMicPlacement.Height));
        var oldRegion = Region;
        Region = null;
        oldRegion?.Dispose();
        BackColor = chatBackground;
        talkButton.FlatAppearance.MouseOverBackColor = chatBackground;
        talkButton.FlatAppearance.MouseDownBackColor = chatBackground;
    }

    private static Color SampleBackground(Rectangle bounds)
    {
        try
        {
            using var bitmap = new Bitmap(1, 1);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.Left - 4, bounds.Top + bounds.Height / 2, 0, 0, new Size(1, 1));
            return bitmap.GetPixel(0, 0);
        }
        catch { return SystemColors.ControlDarkDark; }
    }

    private void DrawTalkIcon(object? sender, PaintEventArgs eventArgs)
    {
        var graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(currentState == TalkButtonState.Listening ? AppTheme.RecordingLight : AppTheme.AccentLight, 1);
        using var outline = Capsule(new RectangleF(1, 1, talkButton.Width - 3, talkButton.Height - 3));
        if (!docked) graphics.DrawPath(border, outline);
        var caption = currentState switch
        {
            TalkButtonState.Listening => "Stop",
            TalkButtonState.Working => "Working",
            _ => "Talk"
        };
        if (!docked) TextRenderer.DrawText(graphics, caption, talkButton.Font,
            new Rectangle(42, 0, talkButton.Width - 48, talkButton.Height), talkButton.ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        var offsetX = docked ? (talkButton.ClientSize.Width - 34) / 2f : 5f;
        var offsetY = (talkButton.ClientSize.Height - 34) / 2f;
        var color = currentState == TalkButtonState.Listening
            ? AppTheme.RecordingLight
            : docked ? Color.White : AppTheme.AccentLight;

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

    private void OnDragStart(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left) return;
        // A docked toolbar control is click-only. Treating mouse-down as the
        // beginning of a drag caused it to expand into the standalone button.
        if (docked) return;
        dragOrigin = eventArgs.Location;
        dragging = false;
    }

    private static GraphicsPath Capsule(RectangleF rectangle)
    {
        var path = new GraphicsPath();
        var diameter = rectangle.Height;
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 90, 180);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 180);
        path.CloseFigure();
        return path;
    }

    private void OnDragMove(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left || docked) return;
        if (Math.Abs(eventArgs.X - dragOrigin.X) + Math.Abs(eventArgs.Y - dragOrigin.Y) > 5) dragging = true;
        if (dragging)
        {
            var delta = new Point(eventArgs.X - dragOrigin.X, eventArgs.Y - dragOrigin.Y);
            Location = new Point(Location.X + delta.X, Location.Y + delta.Y);
            freeLocation = Location;
        }
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
