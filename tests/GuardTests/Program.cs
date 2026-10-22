using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;

static class Tests
{
    internal static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
    [STAThread] static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--test-guard-worker")
        {
            NativeChecks.EnsureIsolated();
            nint fixture = (nint)long.Parse(args[1], CultureInfo.InvariantCulture);
            var recovery = new CompositionRecovery(args[2]);
            CompositionGuardClient.TryRunWorker(["--composition-guard-worker"], () =>
            {
                NativeChecks.EnsureWorkerDpi();
                return new CompositionGuard(() => fixture, recovery);
            }, recovery);
            return Environment.ExitCode;
        }
        if (CompositionGuardClient.TryRunWorker(args)) return Environment.ExitCode;
        if (args.Length == 2 && args[0] == "--fixture")
        {
            using var output = new StreamWriter(args[1]) { AutoFlush = true };
            Console.SetOut(output);
            try { NativeChecks.Run(); return 0; } catch (Exception ex) { Console.WriteLine(ex); return 1; }
        }
        var screen = new Rectangle(0, 0, 1920, 1080);
        var corner = new Rectangle(0, 0, 130, 30);
        bool Eligible(string name = CompositionGuardPolicy.WindowClass, Rectangle? bounds = null, Rectangle? monitor = null,
            Rectangle? caret = null, bool visible = true, bool sameThread = true, bool popup = true, uint dpi = 96)
            => CompositionGuardPolicy.ShouldMask(name, bounds ?? corner, monitor ?? screen, caret, visible, sameThread, popup, dpi);
        Check(Eligible(), "Corner fallback composition is eligible");
        Check(Eligible(CompositionGuardPolicy.FrameClass), "Verified TSF composition frames use the same corner policy");
        Check(!Eligible("IME") && !Eligible("MSCTFIME UI") && !Eligible("CandidateWindow") && !Eligible("Notepad"), "Candidates, IME manager and ordinary windows are excluded");
        Check(!Eligible(visible: false) && !Eligible(sameThread: false) && !Eligible(popup: false), "Hidden, background and child/non-popup windows are excluded");
        Check(!Eligible(bounds: new Rectangle(200, 200, 100, 30)), "Normal caret-positioned popup is excluded");
        Check(!Eligible(caret: new Rectangle(10, 30, 1, 20)), "Legitimate caret at screen corner keeps popup visible");
        Check(Eligible(monitor: new Rectangle(-1920, -1080, 1920, 1080), bounds: new Rectangle(-1920, -1080, 100, 30)), "Negative-coordinate monitor corner is supported");
        Check(Eligible(bounds: new Rectangle(30, 30, 130, 30), dpi: 192) && !Eligible(bounds: new Rectangle(30, 30, 130, 30)), "DPI margin is bounded and scales");
        Check(!Eligible(bounds: Rectangle.Empty) && !Eligible(monitor: Rectangle.Empty), "Invalid geometry fails closed");
        foreach (string invalid in new[] { "{}", "null", "[]", "{", "{\"Version\":2,\"Enabled\":true}", "{\"Version\":1}", "{\"Version\":\"1\",\"Enabled\":true}", "{\"Version\":1,\"Enabled\":null}" })
            Check(GuardSettings.FromJson(invalid) == null, "Invalid settings do not enable suppression: " + invalid);
        foreach (bool enabled in new[] { false, true })
            Check(GuardSettings.FromJson(JsonSerializer.Serialize(new GuardSettings { Enabled = enabled }))?.Enabled == enabled, "Settings round-trip " + enabled);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        foreach (string culture in new[] { "ja-JP", "en-US" })
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            using var form = new GuardSettingsForm(new GuardSettings { Enabled = false }, false);
            Check(!form.Selection.Enabled && !form.StartWithWindows && form.Icon != null, "V3 settings preserve choices and original icon: " + culture);
            Check(form.Text.Contains("V3"), "V3 form edition identity: " + culture);
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true"
                && Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") == "github-hosted"
                && Environment.GetEnvironmentVariable("RUNNER_TEMP") is { Length: > 0 } evidence)
            {
                // Render only this test form, not the runner's screen. The
                // images make the two languages and original icon reviewable.
                form.Show(); Application.DoEvents(); form.PerformLayout(); form.Update();
                if (!form.Controls.Cast<Control>().Any(control => control.Visible)) throw new Exception("Settings render has no visible controls");
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(Path.Combine(evidence, "v3-settings-" + culture + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                form.Hide();
            }
        }
        string keyPath = @"Software\ImeLayoutRouter.Tests\" + Guid.NewGuid().ToString("N");
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue("ImeLayoutRouter", "existing V2 registration");
            GuardStartup.SetEnabled(key, true, "v3-command");
            GuardStartup.SetEnabled(key, false, "different-command");
            Check(key.GetValue(GuardStartup.ValueName) as string == "v3-command", "Startup removal verifies command ownership");
            GuardStartup.SetEnabled(key, false, "v3-command");
            Check(key.GetValue(GuardStartup.ValueName) == null && key.GetValue("ImeLayoutRouter") as string == "existing V2 registration", "V3 startup never replaces or removes V1/V2 startup");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(keyPath, false); }
        Console.WriteLine("All V3 policy/settings checks passed. Only fixture forms and a temporary registry key were used; no user input or application windows were operated.");
        return 0;
    }
}
