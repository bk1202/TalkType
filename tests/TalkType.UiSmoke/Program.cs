using System.Reflection;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        var assembly = Assembly.Load("TalkType");
        object Create(string name) => Activator.CreateInstance(assembly.GetType("TalkType.Desktop." + name)!)!;
        if (args.Contains("--overlay"))
        {
            using var overlay = (Form)Create("FloatingButtonForm");
            using var timeout = new System.Windows.Forms.Timer { Interval = 45000 };
            timeout.Tick += (_, _) => Application.ExitThread();
            timeout.Start();
            Application.Run();
            return;
        }
        var type = assembly.GetType("TalkType.Desktop.SettingsForm")!;
        using var form = (Form)Activator.CreateInstance(type, Create("AppSettings"), Create("EngineManager"))!;
        Control Field(string name) => (Control)type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
        var output = Path.GetFullPath("artifacts/ui-smoke");
        Directory.CreateDirectory(output);
        // Render offscreen: no microphone, downloads, clipboard, settings writes or application context.
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-10000, -10000);
        form.Show();
        void Capture(string name)
        {
            form.PerformLayout();
            Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(Path.Combine(output, name + ".png"));
        }
        Capture("home");
        if (Field("preview").Height < 100 || Field("copy").Top < Field("preview").Bottom)
            throw new Exception("Transcript preview collapsed or overlaps Copy");
        var stateType = assembly.GetType("TalkType.Desktop.TalkButtonState")!;
        foreach (var state in new[] { "Listening", "Working", "Ready" })
        {
            type.GetMethod("SetRecordingState")!.Invoke(form, [Enum.Parse(stateType, state)]);
            if (state == "Listening" && Field("record").Text != "Stop and transcribe") throw new Exception("Missing stop state");
            if (state == "Working" && Field("record").Enabled) throw new Exception("Transcribe button must be disabled");
        }
        type.GetMethod("SetTranscript")!.Invoke(form, ["This is a test transcript."]);
        if (!Field("copy").Enabled) throw new Exception("Copy should be enabled");
        form.Size = form.MinimumSize;
        Capture("home-small");
        Button Find(Control root, string text) => root.Controls.Cast<Control>()
            .SelectMany(Descendants).OfType<Button>().First(button => button.Text == text);
        Find(form, "Preferences").PerformClick();
        Capture("preferences-small");
        form.ClientSize = new Size(760, 740);
        Capture("preferences");
        Console.WriteLine("PASS: Home/Preferences render at both sizes; recording states and copy availability.");
        form.Hide();
        var floatingType = assembly.GetType("TalkType.Desktop.FloatingButtonForm")!;
        using var floating = (Form)Create("FloatingButtonForm");
        ((System.Windows.Forms.Timer)floatingType.GetField("dockingTimer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(floating)!).Stop();
        floating.Location = new Point(-10000, -10000);
        floating.Show();
        foreach (var state in new[] { "Ready", "Listening", "Working" })
        {
            floatingType.GetMethod("SetState")!.Invoke(floating, [Enum.Parse(stateType, state)]);
            Application.DoEvents();
            using var bitmap = new Bitmap(floating.Width, floating.Height);
            floating.DrawToBitmap(bitmap, new Rectangle(Point.Empty, floating.Size));
            bitmap.Save(Path.Combine(output, "button-" + state + ".png"));
        }
        floatingType.GetField("docked", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(floating, true);
        floatingType.GetMethod("ApplyDockedAppearance", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(floating, null);
        foreach (var state in new[] { "Ready", "Listening", "Working" })
        {
            floatingType.GetMethod("SetState")!.Invoke(floating, [Enum.Parse(stateType, state)]);
            Application.DoEvents();
            if (floating.Size != new Size(34, 34)) throw new Exception("Chat mic must remain compact in every state");
            using var bitmap = new Bitmap(34, 34);
            floating.DrawToBitmap(bitmap, new Rectangle(Point.Empty, floating.Size));
            bitmap.Save(Path.Combine(output, "chat-mic-" + state + ".png"));
        }
        floatingType.GetMethod("Undock", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(floating, null);
        if (floating.Size != new Size(124, 44)) throw new Exception("Global button must restore its labelled size");
        Console.WriteLine("PASS: compact chat microphone states and global-size restoration.");
        var placement = assembly.GetType("TalkType.Desktop.ChatMicPlacement")!.GetMethod("GetBounds")!;
        var window = new Rectangle(0, 0, 1920, 1080);
        var composer = new Rectangle(570, 1010, 700, 40);
        var bounds = (Rectangle)placement.Invoke(null, [composer, window, true])!;
        if (bounds != new Rectangle(1278, 1013, 34, 34))
            throw new Exception("Original in-bar offsets changed");
        var wide = new Rectangle(570, 1010, 1300, 40);
        if (((Rectangle)placement.Invoke(null, [wide, window, true])!).X != 1643)
            throw new Exception("Discord right clamp changed");
        if (((Rectangle)placement.Invoke(null, [wide, window, false])!).X != 1794)
            throw new Exception("WhatsApp right clamp changed");
        var multiline = new Rectangle(570, 950, 700, 100);
        if (((Rectangle)placement.Invoke(null, [multiline, window, true])!).Y != 983)
            throw new Exception("Original multiline centering changed");
        Console.WriteLine("PASS: restored in-bar offsets, Discord/WhatsApp clamps and multiline centering.");
        floating.Hide();
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        yield return control;
        foreach (Control child in control.Controls)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
}
