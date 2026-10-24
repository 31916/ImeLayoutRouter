using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

// Integration fixture, NEVER shipped. Keystrokes and composition strings below
// belong only to this fixture on a disposable GitHub-hosted VM. Production V3
// does not read input, send keys, change IME mode or reposition composition UI.
internal static class Program
{
    private static nint window;
    private static nint backdrop;
    private static readonly StringBuilder committed = new();
    private static readonly Native.WindowProc procedure = WndProc;
    private static nint context;
    private static string evidence = "";
    private static readonly HashSet<string> baselineCandidateClasses = [];
    private static readonly List<string> inputMessages = [];
    private static MaskObservation? observation;
    private static CompositionGuardClient? activeGuard;
    [STAThread] private static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted"
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUNNER_TEMP")))
        { Console.Error.WriteLine("This input fixture is restricted to disposable GitHub-hosted runners."); return 2; }
        if (CompositionGuardClient.TryRunWorker(args)) return Environment.ExitCode;
        evidence = Environment.GetEnvironmentVariable("RUNNER_TEMP")!;
        bool browserOnly = args.Length == 1 && args[0] == "--browser-only";
        using var output = new StreamWriter(Path.Combine(evidence, browserOnly ? "v3-ime-browser-checks.txt" : "v3-ime-checks.txt")) { AutoFlush = true };
        Console.SetOut(TextWriter.Synchronized(new TeeWriter(Console.Out, output)));
        bool desktopOnly = args.Length == 1 && args[0] == "--check-desktop";
        try
        {
            if (args.Length == 1 && args[0] == "--configure-vm-display")
            { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); VmDisplays.Configure(false); return 0; }
            Run(desktopOnly, browserOnly, args.Length == 1 && args[0] == "--game-only");
            Console.WriteLine(desktopOnly ? "PASS Disposable desktop accepts fixture focus. No IME input tested." :
                browserOnly ? "PASS Ordinary browser IME compatibility checks completed; fallback suppression was not tested." : "PASS Real Japanese IME checks completed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            if (context != 0)
            {
                Native.ImmGetConversionStatus(context, out uint mode, out uint sentence);
                Console.WriteLine($"Fixture failure state: composition={Composition()}; committed={committed}; open={Native.ImmGetOpenStatus(context)}; conversion=0x{mode:X}; sentence=0x{sentence:X}");
                Console.WriteLine("Own fixture input-message trace: " + string.Join("; ", inputMessages));
            }
            DumpWindows("failure"); return 1;
        }
        finally
        {
            if (context != 0) Native.ImmReleaseContext(window, context);
            if (window != 0) Native.DestroyWindow(window);
            if (backdrop != 0) Native.DestroyWindow(backdrop);
        }
    }

    private static void Run(bool desktopOnly, bool browserOnly, bool gameOnly)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Console.WriteLine($"OS={Environment.OSVersion}; process={RuntimeInformation.ProcessArchitecture}; session={Process.GetCurrentProcess().SessionId}");
        var cls = new Native.WindowClass { Proc = procedure, Instance = Native.GetModuleHandleW(null), Name = "ImeGuardRealInputFixture", Background = (nint)6 };
        Check(Native.RegisterClassW(ref cls) != 0, "Own input fixture registered");
        var desktop = Screen.PrimaryScreen!.Bounds;
        // Keep unrelated runner console/desktop content out of test screenshots.
        backdrop = Native.CreateWindowExW(0x08000000, cls.Name!, "Disposable IME test backdrop", 0x90000000,
            desktop.Left, desktop.Top, desktop.Width, desktop.Height, 0, 0, cls.Instance, 0);
        Check(backdrop != 0, "Own screenshot backdrop created");
        window = Native.CreateWindowExW(0, cls.Name!, "V3 Japanese IME compatibility fixture", 0x10CF0000, 350, 250, 800, 400, 0, 0, cls.Instance, 0);
        Check(window != 0, "Own input fixture created");
        Focus();
        if (desktopOnly) return;
        if (gameOnly)
        {
            using var gameGuard = new CompositionGuardClient();
            GameChecks.Run(gameGuard, Focus);
            return;
        }
        if (browserOnly)
        {
            using var browserGuard = new CompositionGuardClient();
            int starts = int.TryParse(Environment.GetEnvironmentVariable("GUARD_BROWSER_STARTS"), out int requested) ? requested : 1;
            if (starts < 1 || starts > 20) throw new InvalidOperationException("Browser cold-start count must be 1..20");
            for (int start = 0; start < starts; start++)
            {
                Focus();
                Console.WriteLine($"Browser cold start {start + 1}/{starts}; mode={Environment.GetEnvironmentVariable("GUARD_BROWSER_READY_MODE") ?? "native-ready"}");
                ChromeChecks.Run(browserGuard, enabledFirst: start % 2 == 1);
            }
            return;
        }
        bool microsoftIme = string.Equals(Environment.GetEnvironmentVariable("GUARD_TEST_IME_CLSID"),
            "03B5835F-F03C-411B-9CE2-AA23E1171E36", StringComparison.OrdinalIgnoreCase);
        Tsf.ActivateJapanese(forTestDesktop: microsoftIme);
        Pump(1000);
        context = Native.ImmGetContext(window);
        Check(context != 0, "Actual Windows input context exists");
        Console.WriteLine($"HKL=0x{Native.GetKeyboardLayout(0):X}; ImmIsIME={Native.ImmIsIME(Native.GetKeyboardLayout(0))}");
        Native.ImmSetOpenStatus(context, true);
        Native.ImmSetConversionStatus(context, 0x19, 0); // native, full-shape, Roman input on this test context
        Key(0x16); // VK_IME_ON, directed to this fixture
        // The modern Microsoft IME may not honor IMM conversion-mode setters.
        // Use its documented Hiragana shortcut on this fixture as a real user
        // would. The shortcut and input configuration never run in production.
        if (microsoftIme) HiraganaShortcut();
        Pump(500);
        Console.WriteLine($"IME open={Native.ImmGetOpenStatus(context)}");
        Native.ImmGetConversionStatus(context, out uint conversionMode, out uint sentenceMode);
        Console.WriteLine($"Fixture initial conversion=0x{conversionMode:X}; sentence=0x{sentenceMode:X}");

        // Deliberately simulate an application's wrong composition coordinates
        // through its own input context. No synthetic composition window/string.
        PositionComposition(new Point(0, 0));
        TypeNihon();
        Check(Composition() == "にほん", "Real keystrokes produce Japanese uncommitted text");
        var original = FindComposition();
        Check(original != 0, "Windows created a visible fallback composition popup");
        Check(!EmptyRegion(original), "Baseline composition is drawable before enabling V3");
        DumpWindows("baseline");
        string baseline = ConvertAndCommit("baseline", null);
        Check(baseline.Length > 0 && baseline.Any(c => c > 0x3000), "Baseline conversion commits Japanese text");

        using var guard = new CompositionGuardClient();
        activeGuard = guard;
        guard.SetEnabled(true);
        Check(guard.Error == null, "Production V3 helper starts");
        PositionComposition(new Point(0, 0));
        TypeNihon();
        var watch = Stopwatch.StartNew();
        try { Wait(() => FindComposition() is var h && h != 0 && EmptyRegion(h), "V3 masks the real corner composition popup"); }
        catch
        {
            Console.WriteLine("Helper state before cleanup: " + (guard.CheckError() ?? "running/no reported error"));
            var gui = new CompositionGuard.Native.GuiInfo { Size = (uint)Marshal.SizeOf<CompositionGuard.Native.GuiInfo>() };
            bool queried = CompositionGuard.Native.GetGUIThreadInfo(Native.GetCurrentThreadId(), ref gui);
            Console.WriteLine($"Failure native input: queried={queried}; foreground=0x{Native.GetForegroundWindow():X}; own=0x{window:X}; focus=0x{gui.Focus:X}; caret=0x{gui.Caret:X}; caretClass={Class(gui.Caret)}; caretRect={gui.CaretRect.Left},{gui.CaretRect.Top},{gui.CaretRect.Right},{gui.CaretRect.Bottom}");
            throw;
        }
        Console.WriteLine($"Mask observation after completed typing: {watch.ElapsedMilliseconds} ms (not a first-frame latency measurement)");
        Check(Composition() == "にほん", "Masking preserves the uncommitted string");
        DumpWindows("masked");
        string enabled = ConvertAndCommit("enabled", guard);
        Check(enabled == baseline, "V3 preserves exactly the baseline committed result");

        committed.Clear();
        PositionComposition(new Point(0, 0));
        TypeNihon();
        Key(0x1B); Pump(300); Key(0x1B);
        Wait(() => Composition().Length == 0, "Escape cancels composition while V3 is enabled");
        Check(committed.Length == 0, "Cancellation does not insert text");

        PositionComposition(new Point(0, 0));
        TypeNihon();
        Wait(() => FindComposition() is var h && h != 0 && EmptyRegion(h), "Composition is masked before focus changes");
        var beforeSwitch = FindComposition();
        using (var other = new ForegroundFixture())
        {
            Native.SetForegroundWindow(other.Handle);
            Wait(() => Native.GetForegroundWindow() == other.Handle, "Separate fixture thread owns the foreground");
            Wait(() => !Native.IsWindow(beforeSwitch) || !EmptyRegion(beforeSwitch), "Leaving the input thread restores its real composition surface");
            Focus();
        }
        Key(0x1B); Pump(100); Key(0x1B); Pump(100);

        PositionComposition(new Point(0, 0));
        TypeNihon();
        Wait(() => FindComposition() is var h && h != 0 && EmptyRegion(h), "Next real composition is masked");
        var masked = FindComposition();
        guard.SetEnabled(false);
        Wait(() => !EmptyRegion(masked), "Disabling V3 restores the real composition surface");
        Check(Composition() == "にほん", "Disabling V3 preserves the pending text");
        Key(0x1B); Pump(300); Key(0x1B); Pump(300);

        PositionComposition(new Point(500, 400));
        guard.SetEnabled(true);
        TypeNihon();
        Pump(600);
        var normal = FindComposition();
        Check(normal != 0 && !EmptyRegion(normal), "Normally positioned real composition remains drawable");
        Key(0x1B); Pump(300); Key(0x1B);
        Native.SetWindowPos(window, 0, 0, 0, 0, 0, 0x15);
        Check(Native.CreateCaret(window, 0, 1, 20) && Native.SetCaretPos(0, 0), "Fixture exposes a real Win32 caret near the screen corner");
        Native.ShowCaret(window);
        try
        {
            PositionComposition(new Point(0, 0));
            TypeNihon();
            Pump(400);
            Check(FindComposition() is var atCaret && atCaret != 0 && !EmptyRegion(atCaret),
                "Legitimate composition beside a corner caret remains drawable");
            Key(0x1B); Pump(100); Key(0x1B);
        }
        finally
        {
            Native.DestroyCaret();
            Native.SetWindowPos(window, 0, 350, 250, 0, 0, 0x15);
        }
        // Exercise window reuse and repeated start/end composition with one
        // continuously running production helper, using only this test input.
        var timings = new List<uint>();
        int cycles = int.TryParse(Environment.GetEnvironmentVariable("GUARD_IME_CYCLES"), out int requestedCycles) ? requestedCycles : 10;
        if (cycles is < 1 or > 500) throw new InvalidOperationException("Real composition cycle count must be 1..500");
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            PositionComposition(new Point(0, 0));
            using (var timing = new MaskObservation())
            {
                observation = timing;
                try
                {
                    TypeNihon();
                    Wait(() => FindComposition() is var h && h != 0 && EmptyRegion(h), "Repeated composition is masked: " + cycle);
                    timing.Sample();
                    Check(timing.Milliseconds.HasValue, "Real popup show-to-mask observation exists: " + cycle);
                    timings.Add(timing.Milliseconds!.Value);
                }
                finally { observation = null; }
            }
            Key(0x1B); Pump(100); Key(0x1B); Pump(100);
            Check(Composition().Length == 0 && guard.CheckError() == null, "Helper survives repeated input/cancellation: " + cycle);
        }
        Console.WriteLine("Real popup show-event to empty-region observations (ms; not video/first-frame latency): " + string.Join(", ", timings));
        RunFullscreenChecks(guard);
        if (Environment.GetEnvironmentVariable("GUARD_TEST_DPI") == "1") RunDpiChecks(guard);
        if (Environment.GetEnvironmentVariable("GUARD_TEST_VM_DISPLAYS") == "1") RunMonitorChecks(guard);
        if (Environment.GetEnvironmentVariable("GUARD_TEST_VM_SLEEP") == "1") RunResumeChecks(guard);
        guard.SetEnabled(false);
        Pump(600);
        if (Environment.GetEnvironmentVariable("GUARD_TEST_BROWSER") == "chrome") ChromeChecks.Run(guard);
    }

    private static void RunDpiChecks(CompositionGuardClient guard)
    {
        using var scaling = new VmDpi();
        scaling.PrepareResolution(Screen.PrimaryScreen!.DeviceName); Pump(1000);
        var desktop = Screen.PrimaryScreen!.Bounds;
        Native.SetWindowPos(backdrop, 0, desktop.Left, desktop.Top, desktop.Width, desktop.Height, 0x14);
        string? baseline = null;
        foreach (int percent in new[] { 100, 150 })
        {
            guard.SetEnabled(false);
            scaling.Set(Screen.PrimaryScreen!.DeviceName, percent);
            Wait(() => GetDpiForWindow(window) == percent * 96 / 100, "Windows reports actual fixture DPI for " + percent + "%");
            Focus();
            foreach (bool enabled in new[] { false, true })
            {
                guard.SetEnabled(enabled); Pump(400);
                PositionComposition(new Point(0, 0)); TypeNihon();
                Wait(() => FindComposition() is var h && h != 0 && EmptyRegion(h) == enabled,
                    $"Real IME at actual {percent}% DPI respects suppression={enabled}");
                Check(Composition() == "にほん", "Actual DPI transition preserves preedit");
                string result = ConvertAndCommit($"dpi-{percent}-{enabled}", enabled ? guard : null);
                baseline ??= result;
                Check(result == baseline, "Actual display scaling preserves converted text");
            }
        }
        guard.SetEnabled(false);
    }

    private static void RunFullscreenChecks(CompositionGuardClient guard)
    {
        // Real IME input in our own borderless full-screen Win32 window.
        // This does not claim coverage of exclusive DirectX or anti-cheat games.
        int style = Native.GetWindowLongW(window, -16);
        Check(Native.GetWindowRect(window, out var previous), "Full-screen fixture saves its own bounds");
        var screen = Screen.FromHandle(window).Bounds;
        string? baseline = null;
        try
        {
            SetWindowLongW(window, -16, unchecked((int)0x90000000));
            Check(Native.SetWindowPos(window, 0, screen.Left, screen.Top, screen.Width, screen.Height, 0x34),
                "Own fixture enters borderless full-screen mode");
            Focus(); Pump(300);
            foreach (bool enabled in new[] { false, true })
            {
                guard.SetEnabled(enabled); Pump(400);
                PositionComposition(screen.Location); TypeNihon();
                Wait(() => FindComposition() is var h && h != 0 && EmptyRegion(h) == enabled,
                    "Full-screen real composition matches suppression selection: " + enabled);
                Check(Composition() == "にほん", "Full-screen masking preserves real pending text");
                string result = ConvertAndCommit(enabled ? "fullscreen-enabled" : "fullscreen-baseline", enabled ? guard : null);
                if (!enabled) baseline = result;
                else Check(result == baseline, "Full-screen conversion and candidates preserve the baseline result");
            }
        }
        finally
        {
            guard.SetEnabled(false);
            SetWindowLongW(window, -16, style);
            Native.SetWindowPos(window, 0, previous.Left, previous.Top, previous.Right - previous.Left, previous.Bottom - previous.Top, 0x34);
            Focus(); Pump(400);
        }
    }
    [DllImport("user32.dll")] private static extern int SetWindowLongW(nint h, int index, int value);

    private static void RunMonitorChecks(CompositionGuardClient guard)
    {
        guard.SetEnabled(false);
        VmDisplays.Configure(true); Pump(1000); Focus();
        using (var scaling = new VmDpi())
        {
            foreach (var screen in Screen.AllScreens) scaling.Set(screen.DeviceName, screen.Primary ? 100 : 125);
            Pump(1000);
            foreach (var screen in Screen.AllScreens.OrderByDescending(s => s.Primary))
            {
                Console.WriteLine("Testing real input on monitor: " + screen.Bounds);
                Native.SetWindowPos(window, 0, screen.Bounds.Left + 350, screen.Bounds.Top + 250, 600, 350, 0x14);
                Focus();
                Wait(() => GetDpiForWindow(window) == (screen.Primary ? 96 : 120),
                    "Actual per-monitor DPI transition reaches " + screen.DeviceName);
                foreach (bool enabled in new[] { false, true })
                {
                    committed.Clear();
                    guard.SetEnabled(enabled); Pump(300);
                    PositionComposition(screen.Bounds.Location); TypeNihon();
                    Wait(() => FindComposition() is var h && h != 0 && EmptyRegion(h) == enabled,
                        "Composition suppression matches selected state on " + screen.DeviceName);
                    nint popup = FindComposition();
                    Check(Native.GetWindowRect(popup, out var bounds) && Math.Abs(bounds.Left - screen.Bounds.Left) < 16
                        && Math.Abs(bounds.Top - screen.Bounds.Top) < 16, "Real IME popup occupies the actual monitor corner");
                    Console.WriteLine($"Popup DPI={GetDpiForWindow(popup)}; physical corner={bounds.Left},{bounds.Top}");
                    Key(0x0D); Pump(300);
                    Check(committed.ToString() == "にほん" && Composition().Length == 0, "Monitor transition preserves actual committed input");
                }
                // Move the real pending popup away from this monitor's corner.
                guard.SetEnabled(true); PositionComposition(screen.Bounds.Location); TypeNihon();
                Wait(() => FindComposition() is var h && h != 0 && EmptyRegion(h), "Monitor corner is masked before position correction");
                PositionComposition(new Point(screen.Bounds.Left + 300, screen.Bounds.Top + 180)); Pump(500);
                Wait(() => FindComposition() is var h && h != 0 && !EmptyRegion(h), "Corrected composition position is restored on this monitor");
                Check(Composition() == "にほん", "Moving a real composition does not change pending text");
                Key(0x1B); Pump(100); Key(0x1B); Pump(100);
            }
            guard.SetEnabled(false);
            }
        VmDisplays.Configure(false); Pump(500); Focus();
    }
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint h);

    private static void RunResumeChecks(CompositionGuardClient guard)
    {
        string? baselineResume = null;
        foreach (bool enabled in new[] { false, true })
        {
            committed.Clear();
            guard.SetEnabled(enabled); Pump(400);
            PositionComposition(new Point(0, 0)); TypeNihon();
            if (enabled) Wait(() => FindComposition() is var h && h != 0 && EmptyRegion(h), "Real composition masked before suspend");
            VmLifecycle.Suspend(Pump, enabled ? 2 : 1);
            Focus(); Pump(300);
            string resumed = committed + "|" + Composition();
            Console.WriteLine($"Suspend/resume enabled={enabled}: committed|pending={resumed}");
            Check((committed.ToString() + Composition()) == "にほん", "Actual sleep/resume preserves the fixed test input");
            if (!enabled) baselineResume = resumed;
            else Check(resumed == baselineResume && guard.CheckError() == null, "V3 preserves baseline resume behavior and helper lifetime");
            guard.SetEnabled(false); Pump(400);
            nint popup = FindComposition();
            Check(popup == 0 || !EmptyRegion(popup), "Disable after resume restores any surviving surface");
            Key(0x1B); Pump(100); Key(0x1B); Pump(100);
            committed.Clear();
            guard.SetEnabled(true);
            PositionComposition(new Point(0, 0)); TypeNihon();
            Wait(() => FindComposition() is var h && h != 0 && EmptyRegion(h), "New composition is masked after resume");
            Key(0x0D); Pump(300);
            Check(committed.ToString() == "にほん" && Composition().Length == 0, "New input commits correctly after resume");
        }
    }

    private static string ConvertAndCommit(string label, CompositionGuardClient? guard)
    {
        committed.Clear();
        bool microsoft = string.Equals(Environment.GetEnvironmentVariable("GUARD_TEST_IME_CLSID"),
            "03B5835F-F03C-411B-9CE2-AA23E1171E36", StringComparison.OrdinalIgnoreCase);
        using var beforeCandidates = microsoft ? CandidateRaster.Capture() : null;
        Key(0x20); Pump(300); Key(0x20); Pump(600);
        uint bytes = Native.ImmGetCandidateListW(context, 0, null, 0);
        Console.WriteLine($"{label}: candidate-list bytes={bytes}");
        var tsf = bytes == 0 ? TsfCandidates.Read() : null;
        bool accessible = bytes == 0 && tsf == null && AccessibleCandidates.AreVisible(() => Pump(10));
        bool raster = bytes == 0 && tsf == null && !accessible && beforeCandidates != null
            && CandidateRaster.Verify(beforeCandidates, label, guard != null);
        bool observed = accessible || raster;
        Check((bytes >= 24 && bytes < 1024 * 1024) || tsf is { Count: > 0, Shown: true } || observed,
            label + ": actual IME conversion candidates exist via IMM, TSF, accessibility or compared panel pixels");
        var buffer = new byte[bytes];
        Check(observed || tsf != null || (Native.ImmGetCandidateListW(context, 0, buffer, bytes) == bytes && BitConverter.ToUInt32(buffer, 8) > 0),
            label + ": conversion candidate list is nonempty");
        DumpWindows(label + "-candidates");
        var candidates = CandidateSurfaces();
        if (accessible) candidates.Add("UIA:Microsoft candidate panel with visible 日本 and 二本");
        if (raster) candidates.Add("Raster:Microsoft candidate panel, baseline glyph pixels preserved");
        if (tsf is { Shown: true })
        {
            Check(tsf.Sample.Contains("日本"), label + ": TSF candidates contain the fixed test word's conversion");
            // GetGUID identifies this UI element, not a guaranteed stable
            // class across separate compositions. Compare the verified
            // candidate interface, visibility and test-word content instead.
            candidates.Add("TSF:ITfCandidateListUIElement");
        }
        Check(candidates.Count > 0, label + ": conversion candidate UI is visible and drawable");
        if (guard == null) baselineCandidateClasses.UnionWith(candidates);
        else Check(baselineCandidateClasses.IsSubsetOf(candidates), "V3 preserves the baseline candidate UI classes");
        // Always choose the first candidate so learning from the baseline does
        // not change a later 'second candidate' and create a false difference.
        if (observed) Check(Composition() == "二本", label + ": second candidate is the expected fixed-word conversion");
        uint selection = observed ? 1 : tsf?.Selection ?? BitConverter.ToUInt32(buffer, 12);
        for (int i = 0; selection != 0 && i < 20; i++)
        {
            Key(0x26); Pump(100);
            if (observed)
            {
                Check(Composition() == "日本", label + ": Up selects the expected first candidate");
                selection = 0;
            }
            else if (tsf != null)
            {
                tsf = TsfCandidates.Read() ?? throw new InvalidOperationException("TSF candidate UI disappeared during selection");
                selection = tsf.Selection;
            }
            else
            {
                Check(Native.ImmGetCandidateListW(context, 0, buffer, bytes) == bytes, "Candidate list remains available during selection");
                selection = BitConverter.ToUInt32(buffer, 12);
            }
        }
        Check(selection == 0, label + ": first conversion candidate can be selected");
        if (guard != null)
        {
            Check(guard.CheckError() == null, "Helper stays alive during conversion");
            Check(FindComposition() is var popup && popup != 0 && EmptyRegion(popup), "Real composition remains masked during candidate selection");
        }
        Key(0x0D);
        Console.WriteLine(label + ": after Enter composition=" + Composition() + "; committed=" + committed);
        Wait(() => committed.Length > 0 && Composition().Length == 0, label + ": Enter commits composition");
        Console.WriteLine(label + ": committed fixture text=" + committed);
        return committed.ToString();
    }

    private static void TypeNihon()
    {
        foreach (char c in "NIHON") { Key(c); Pump(100); }
        // A final n is pending in Roman input; a second n resolves it to ん.
        Key('N');
        Wait(() => Composition().Length > 0, "IME reports an actual composition string");
        Pump(300);
        Console.WriteLine("Fixture composition=" + Composition());
    }
    private static void PositionComposition(Point screen)
    {
        var point = new Native.Point { X = screen.X, Y = screen.Y };
        Check(Native.ScreenToClient(window, ref point), "Fixture coordinates translated");
        var form = new Native.CompositionForm { Style = 0x20, Current = point };
        Check(Native.ImmSetCompositionWindow(context, ref form), "Fixture composition position set");
    }
    private static void Focus()
    {
        Native.SetForegroundWindow(window); Native.SetFocus(window); Pump(200);
        if (Native.GetForegroundWindow() != window)
        {
            // Windows client images can deny foreground activation after their
            // shell starts. Click only an uncovered point belonging exactly to
            // our own raw HWND, on this disposable VM. Never click setup UI or
            // an unrelated window to obtain focus.
            var point = new Native.Point { X = 40, Y = 80 };
            Check(Native.ClientToScreen(window, ref point), "Own fixture client point maps to the desktop");
            var blocker = new StringBuilder(128);
            Native.GetClassNameW(Native.WindowFromPoint(point), blocker, blocker.Capacity);
            Console.WriteLine($"Fixture activation preparation: foreground=0x{Native.GetForegroundWindow():X}; hitClass={blocker}; point={point.X},{point.Y}");
            // Foreground-lock rules can leave a newly created test HWND behind
            // the disposable shell. Raise only our HWND before clicking it,
            // then remove topmost immediately so it cannot cover IME candidates.
            Check(Native.SetWindowPos(window, (nint)(-1), 0, 0, 0, 0, 0x13), "Own fixture is raised without changing another window");
            try
            {
                Wait(() => Native.WindowFromPoint(point) == window,
                    "Fixture focus click belongs only to its own uncovered client area");
                Check(Native.SetCursorPos(point.X, point.Y) && Native.WindowFromPoint(point) == window,
                    "Disposable pointer is positioned on the fixture only");
                var down = new Native.Input { Type = 0, Data = new Native.InputUnion { Mouse = new Native.Mouse { Flags = 2 } } };
                var up = down; up.Data.Mouse.Flags = 4;
                Check(Native.SendInput(2, [down, up], Marshal.SizeOf<Native.Input>()) == 2, "Own fixture focus click is accepted");
                Pump(200);
            }
            finally { Native.SetWindowPos(window, (nint)(-2), 0, 0, 0, 0, 0x13); }
        }
        Console.WriteLine($"Fixture focus: foreground=0x{Native.GetForegroundWindow():X}; focus=0x{Native.GetFocus():X}; own=0x{window:X}");
        Check(Native.GetForegroundWindow() == window && Native.GetFocus() == window, "Only the fixture owns input focus");
    }
    private static void Key(int key)
    {
        if (Native.GetForegroundWindow() != window || Native.GetFocus() != window)
            throw new InvalidOperationException("Focus left the test fixture; input stopped.");
        var down = new Native.Input { Type = 1, Data = new Native.InputUnion { Keyboard = new Native.Keyboard { Key = (ushort)key } } };
        var up = down; up.Data.Keyboard.Flags = 2;
        if (Native.SendInput(2, [down, up], Marshal.SizeOf<Native.Input>()) != 2)
            throw new InvalidOperationException("Fixture keystroke was not accepted");
        Pump(50);
    }
    private static void HiraganaShortcut()
    {
        if (Native.GetForegroundWindow() != window || Native.GetFocus() != window)
            throw new InvalidOperationException("Focus left the fixture; IME shortcut stopped");
        Native.Input Input(ushort key, uint flags) => new() { Type = 1, Data = new Native.InputUnion { Keyboard = new Native.Keyboard { Key = key, Flags = flags } } };
        if (Native.SendInput(4, [Input(0x11, 0), Input(0x14, 0), Input(0x14, 2), Input(0x11, 2)], Marshal.SizeOf<Native.Input>()) != 4)
        {
            Native.SendInput(2, [Input(0x14, 2), Input(0x11, 2)], Marshal.SizeOf<Native.Input>());
            throw new InvalidOperationException("Fixture Hiragana shortcut was not accepted");
        }
        Pump(300);
    }
    private static string Composition()
    {
        int count = Native.ImmGetCompositionStringW(context, 8, null, 0);
        if (count <= 0) return "";
        if (count > 4096) throw new InvalidOperationException("Unexpected fixture composition size");
        var bytes = new byte[count];
        Native.ImmGetCompositionStringW(context, 8, bytes, (uint)bytes.Length);
        return Encoding.Unicode.GetString(bytes);
    }
    private static nint FindComposition()
    {
        nint result = 0;
        Native.EnumThreadWindows(Native.GetCurrentThreadId(), (h, _) => {
            if (CompositionGuard.IsCompositionSurface(h) && Native.IsWindowVisible(h)) result = h;
            return true;
        }, 0);
        return result;
    }
    private static string Class(nint h) { var s = new StringBuilder(128); Native.GetClassNameW(h, s, s.Capacity); return s.ToString(); }
    private static HashSet<string> CandidateSurfaces()
    {
        var result = new HashSet<string>();
        Native.EnumWindows((h, _) => {
            string name = Class(h);
            if (name.Contains("Candidate", StringComparison.OrdinalIgnoreCase) && Native.IsWindowVisible(h)
                && Native.GetWindowRect(h, out var bounds) && bounds.Right > bounds.Left && bounds.Bottom > bounds.Top
                && !EmptyRegion(h)) result.Add(name);
            return true;
        }, 0);
        return result;
    }
    private static bool EmptyRegion(nint h)
    {
        nint region = Native.CreateRectRgn(0, 0, 0, 0);
        try { return Native.GetWindowRgn(h, region) == 1; }
        finally { Native.DeleteObject(region); }
    }
    private static void DumpWindows(string label)
    {
        Console.WriteLine("Window evidence: " + label);
        Native.EnumWindows((h, _) => {
            string name = Class(h);
            Native.GetWindowRect(h, out var rect);
            if (name.Contains("IME", StringComparison.OrdinalIgnoreCase) || name.Contains("Candidate", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("GoogleJapaneseInput", StringComparison.Ordinal)
                || (Native.IsWindowVisible(h) && rect.Right > rect.Left && rect.Bottom > rect.Top && Math.Abs(rect.Left) < 20 && Math.Abs(rect.Top) < 20))
            {
                uint thread = Native.GetWindowThreadProcessId(h, out uint pid);
                nint owner = Native.GetWindow(h, 4);
                uint ownerThread = Native.GetWindowThreadProcessId(owner, out uint ownerPid);
                Console.WriteLine($"class={name}; visible={Native.IsWindowVisible(h)}; pid={pid}; tid={thread}; rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom}; empty={EmptyRegion(h)}; style=0x{Native.GetWindowLongW(h, -16):X}; classStyle=0x{Native.GetClassLongW(h, -26):X}; ownerClass={Class(owner)}; ownerPid={ownerPid}; ownerTid={ownerThread}");
                if (name == "CiceroUIWndFrame") Native.EnumChildWindows(h, (child, _) => {
                    Native.GetWindowRect(child, out var childRect);
                    Console.WriteLine($"  child class={Class(child)}; visible={Native.IsWindowVisible(child)}; rect={childRect.Left},{childRect.Top},{childRect.Right},{childRect.Bottom}; style=0x{Native.GetWindowLongW(child, -16):X}; classStyle=0x{Native.GetClassLongW(child, -26):X}");
                    return true;
                }, 0);
            }
            return true;
        }, 0);
        // Capture only this disposable VM, after the entry-point environment
        // checks. These images allow visual review beyond WS_VISIBLE flags.
        try
        {
            Rectangle bounds = Screen.PrimaryScreen!.Bounds;
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var graphics = Graphics.FromImage(bitmap))
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            bitmap.Save(Path.Combine(evidence, "v3-ime-" + label + ".png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        catch (Exception ex) { Console.WriteLine("Screenshot unavailable: " + ex.Message); }
    }
    private static nint WndProc(nint h, uint msg, nint wp, nint lp)
    {
        if (h == window) VmLifecycle.Observe(msg, wp);
        if (h == window && inputMessages.Count < 80 && msg is 0x102 or 0x10D or 0x10E or 0x10F or 0x286)
            inputMessages.Add($"{msg:X}:wp={wp:X},lp={lp:X}");
        // Unicode windows receive WM_IME_CHAR for default-IME conversion.
        // Like WM_CHAR, consume it here instead of expecting an ANSI split.
        if (h == window && msg is 0x102 or 0x286)
        {
            // Like a single-line edit field, do not insert Escape/Enter controls.
            if ((long)wp >= 0x20 && wp != 0x7F) committed.Append((char)wp);
            return 0;
        }
        return Native.DefWindowProcW(h, msg, wp, lp);
    }
    private static void Pump(int ms) { long end = Environment.TickCount64 + ms; do { Native.PumpMessages(); observation?.Sample(); Thread.Sleep(10); } while (Environment.TickCount64 < end); }
    private static void Wait(Func<bool> predicate, string name)
    {
        long end = Environment.TickCount64 + 5000;
        do
        {
            // A successful native observation is the result. Re-querying it
            // twice outside the wait loop created false failures during an
            // owner's transient GetWindowRgn query/move (run 40063353493).
            if (predicate()) { Check(true, name); return; }
            Pump(20);
        } while (Environment.TickCount64 < end);
        var gui = new CompositionGuard.Native.GuiInfo { Size = (uint)Marshal.SizeOf<CompositionGuard.Native.GuiInfo>() };
        bool queried = CompositionGuard.Native.GetGUIThreadInfo(Native.GetCurrentThreadId(), ref gui);
        Console.WriteLine($"Wait failure state: helper={activeGuard?.CheckError() ?? "no reported error"}; queried={queried}; foreground=0x{Native.GetForegroundWindow():X}; own=0x{window:X}; caret=0x{gui.Caret:X}; caretClass={Class(gui.Caret)}; caretRect={gui.CaretRect.Left},{gui.CaretRect.Top},{gui.CaretRect.Right},{gui.CaretRect.Bottom}");
        Check(false, name);
    }
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException("FAIL " + name); Console.WriteLine("PASS " + name); }
    private sealed class TeeWriter(TextWriter console, TextWriter file) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value) { console.Write(value); file.Write(value); }
        public override void Write(string? value) { console.Write(value); file.Write(value); }
        public override void WriteLine(string? value) { console.WriteLine(value); file.WriteLine(value); }
    }
    private sealed class ForegroundFixture : IDisposable
    {
        private readonly Thread thread;
        private Form? form;
        internal nint Handle { get; private set; }
        internal ForegroundFixture()
        {
            using var ready = new ManualResetEventSlim();
            thread = new Thread(() => {
                using var view = new Form { Text = "V3 alternate foreground fixture", StartPosition = FormStartPosition.Manual,
                    Location = new Point(400, 300), Size = new Size(400, 200) };
                form = view;
                view.Shown += (_, _) => { Handle = view.Handle; ready.Set(); };
                Application.Run(view);
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!ready.Wait(5000)) throw new TimeoutException("Alternate fixture could not start");
        }
        public void Dispose()
        {
            if (form is { IsDisposed: false }) form.BeginInvoke(() => form.Close());
            if (!thread.Join(5000)) throw new TimeoutException("Alternate fixture could not stop");
        }
    }
}
