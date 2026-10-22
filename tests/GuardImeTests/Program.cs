using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

// Integration fixture, NEVER shipped. Keystrokes and composition strings below
// belong only to this fixture on a disposable GitHub-hosted VM. Production V3
// does not read input, send keys, change IME mode or reposition composition UI.
internal static class Program
{
    private static nint window;
    private static readonly StringBuilder committed = new();
    private static readonly Native.WindowProc procedure = WndProc;
    private static nint context;
    private static string evidence = "";
    [STAThread] private static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted"
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUNNER_TEMP")))
        { Console.Error.WriteLine("This input fixture is restricted to disposable GitHub-hosted runners."); return 2; }
        if (CompositionGuardClient.TryRunWorker(args)) return Environment.ExitCode;
        evidence = Environment.GetEnvironmentVariable("RUNNER_TEMP")!;
        using var output = new StreamWriter(Path.Combine(evidence, "v3-ime-checks.txt")) { AutoFlush = true };
        Console.SetOut(output);
        try { Run(); Console.WriteLine("PASS Real Japanese IME checks completed."); return 0; }
        catch (Exception ex) { Console.WriteLine(ex); DumpWindows("failure"); return 1; }
        finally
        {
            if (context != 0) Native.ImmReleaseContext(window, context);
            if (window != 0) Native.DestroyWindow(window);
        }
    }

    private static void Run()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Console.WriteLine($"OS={Environment.OSVersion}; process={RuntimeInformation.ProcessArchitecture}; session={Process.GetCurrentProcess().SessionId}");
        var cls = new Native.WindowClass { Proc = procedure, Instance = Native.GetModuleHandleW(null), Name = "ImeGuardRealInputFixture", Background = (nint)6 };
        Check(Native.RegisterClassW(ref cls) != 0, "Own input fixture registered");
        window = Native.CreateWindowExW(0, cls.Name!, "V3 Japanese IME compatibility fixture", 0x10CF0000, 350, 250, 800, 400, 0, 0, cls.Instance, 0);
        Check(window != 0, "Own input fixture created");
        Focus();
        Tsf.ActivateJapanese();
        Pump(1000);
        context = Native.ImmGetContext(window);
        Check(context != 0, "Actual Windows input context exists");
        Console.WriteLine($"HKL=0x{Native.GetKeyboardLayout(0):X}; ImmIsIME={Native.ImmIsIME(Native.GetKeyboardLayout(0))}");
        Native.ImmSetOpenStatus(context, true);
        Native.ImmSetConversionStatus(context, 0x19, 0); // native, full-shape, Roman input on this test context
        Key(0x16); // VK_IME_ON, directed to this fixture
        Pump(500);
        Console.WriteLine($"IME open={Native.ImmGetOpenStatus(context)}");

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
        guard.SetEnabled(true);
        Check(guard.Error == null, "Production V3 helper starts");
        PositionComposition(new Point(0, 0));
        TypeNihon();
        var watch = Stopwatch.StartNew();
        Wait(() => FindComposition() is var h && h != 0 && EmptyRegion(h), "V3 masks the real corner composition popup");
        Console.WriteLine($"Mask observation after completed typing: {watch.ElapsedMilliseconds} ms (not a first-frame latency measurement)");
        Check(Composition() == "にほん", "Masking preserves the uncommitted string");
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
        guard.SetEnabled(false);
        Pump(600);
    }

    private static string ConvertAndCommit(string label, CompositionGuardClient? guard)
    {
        committed.Clear();
        Key(0x20); Pump(300); Key(0x20); Pump(600);
        uint bytes = Native.ImmGetCandidateListW(context, 0, null, 0);
        Console.WriteLine($"{label}: candidate-list bytes={bytes}");
        Check(bytes >= 24 && bytes < 1024 * 1024, label + ": actual IME conversion candidates exist");
        var buffer = new byte[bytes];
        Check(Native.ImmGetCandidateListW(context, 0, buffer, bytes) == bytes && BitConverter.ToUInt32(buffer, 8) > 0,
            label + ": conversion candidate list is nonempty");
        DumpWindows(label + "-candidates");
        if (guard != null) Check(guard.CheckError() == null, "Helper stays alive during conversion");
        Key(0x0D);
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
            if (Class(h) == CompositionGuardPolicy.WindowClass && Native.IsWindowVisible(h)) result = h;
            return true;
        }, 0);
        return result;
    }
    private static string Class(nint h) { var s = new StringBuilder(128); Native.GetClassNameW(h, s, s.Capacity); return s.ToString(); }
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
            if (name.Contains("IME", StringComparison.OrdinalIgnoreCase) || name.Contains("Candidate", StringComparison.OrdinalIgnoreCase))
            {
                uint thread = Native.GetWindowThreadProcessId(h, out uint pid);
                Native.GetWindowRect(h, out var rect);
                Console.WriteLine($"class={name}; visible={Native.IsWindowVisible(h)}; pid={pid}; tid={thread}; rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom}; empty={EmptyRegion(h)}");
            }
            return true;
        }, 0);
    }
    private static nint WndProc(nint h, uint msg, nint wp, nint lp)
    {
        if (msg == 0x102) { committed.Append((char)wp); return 0; }
        return Native.DefWindowProcW(h, msg, wp, lp);
    }
    private static void Pump(int ms) { long end = Environment.TickCount64 + ms; do { Application.DoEvents(); Thread.Sleep(10); } while (Environment.TickCount64 < end); }
    private static void Wait(Func<bool> predicate, string name)
    {
        long end = Environment.TickCount64 + 5000;
        while (!predicate() && Environment.TickCount64 < end) Pump(20);
        Check(predicate(), name);
    }
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException("FAIL " + name); Console.WriteLine("PASS " + name); }
}
