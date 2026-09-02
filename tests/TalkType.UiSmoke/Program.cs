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
        var placement = floatingType.GetMethod("TryGetChatButtonBounds", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var composer in new[] {
            new Rectangle(560, 1010, 1050, 40), new Rectangle(560, 930, 700, 120),
            new Rectangle(180, 650, 450, 40), new Rectangle(-1500, 820, 900, 80) })
        {
            object[] arguments = [composer, new Rectangle(composer.Left - 100, 0, 1400, 1100), Rectangle.Empty];
            if (!(bool)placement.Invoke(null, arguments)!) throw new Exception("Expected safe placement");
            var result = (Rectangle)arguments[2];
            if (result.Bottom > composer.Top - 24 || result.IntersectsWith(composer))
                throw new Exception("Button overlaps composer toolbar zone");
        }
        object[] unsafeArguments = [new Rectangle(20, 30, 500, 40), new Rectangle(0, 0, 800, 600), Rectangle.Empty];
        if ((bool)placement.Invoke(null, unsafeArguments)!) throw new Exception("Must hide when no space above composer");
        Console.WriteLine("PASS: Button states render; resized/multiline/negative-monitor placement avoids composer; unsafe placement rejected.");
        var choose = floatingType.GetMethod("TryChooseChatPosition", BindingFlags.NonPublic | BindingFlags.Static)!;
        var sampleComposer = new Rectangle(560, 1010, 700, 40);
        object[] alternate = [sampleComposer, new Rectangle(0, 0, 1920, 1080),
            (Func<Rectangle, bool>)(candidate => candidate.X > 1000), Rectangle.Empty];
        if (!(bool)choose.Invoke(null, alternate)! || ((Rectangle)alternate[3]).X > 1000)
            throw new Exception("Blocked first anchor should use another location");
        object[] fallback = [sampleComposer, new Rectangle(0, 0, 1920, 1080),
            (Func<Rectangle, bool>)(_ => true), Rectangle.Empty];
        if ((bool)choose.Invoke(null, fallback)! || ((Rectangle)fallback[3]).Bottom > sampleComposer.Top - 24)
            throw new Exception("All-blocked fallback must remain outside toolbar");
        Console.WriteLine("PASS: blocked anchor relocates; all-blocked case retains fallback bounds.");
        floating.Hide();
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        yield return control;
        foreach (Control child in control.Controls)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
}
