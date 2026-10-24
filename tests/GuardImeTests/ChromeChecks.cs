using System.Diagnostics;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

// Ordinary Chrome inputs, using real OS keystrokes on the disposable VM.
// CDP only prepares/reads our local fixture; it never supplies composition text.
internal static class ChromeChecks
{
    internal static void Run(CompositionGuardClient guard, bool enabledFirst = false)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            throw new InvalidOperationException("Browser input tests require a disposable GitHub-hosted VM");
        Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out uint preparationPid);
        Check(preparationPid == Environment.ProcessId, "Own fixture holds focus while preparing the browser input language");
        // On Windows 8+, LoadKeyboardLayout has no effect when the caller
        // does not own keyboard focus. Prepare Japanese before Chrome takes
        // focus, and verify Chrome's own input language after it launches.
        Tsf.ActivateJapanese(forTestDesktop: true);
        Pump(500);
        bool edge = Environment.GetEnvironmentVariable("GUARD_TEST_BROWSER") == "edge";
        string chrome = new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 }
            .Select(folder => edge ? Path.Combine(Environment.GetFolderPath(folder), "Microsoft", "Edge", "Application", "msedge.exe")
                : Path.Combine(Environment.GetFolderPath(folder), "Google", "Chrome", "Application", "chrome.exe"))
            .FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("The hosted image must provide Chrome");
        Console.WriteLine($"Browser executable: {chrome}; version={FileVersionInfo.GetVersionInfo(chrome).FileVersion}; V3 enabled at startup={enabledFirst}");
        guard.SetEnabled(enabledFirst);
        string root = Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!, "guard-chrome-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string page = Path.Combine(root, "input.html");
        string pageTitle = "V3 local browser input fixture " + Guid.NewGuid().ToString("N");
        File.WriteAllText(page, """
            <!doctype html><meta charset="utf-8"><title>V3 local browser input fixture</title>
            <style>body{font:24px sans-serif;margin:80px}input,textarea{font:28px sans-serif;display:block;margin:20px 0;width:600px}</style>
            <i aria-hidden="true" style="position:fixed;left:12px;top:12px;width:12px;height:12px;background:rgb(33,77,155)"></i>
            <h1>Disposable V3 input validation</h1><label>Text<input id="text"></label><label>Multiline<textarea id="area"></textarea></label>
            <script>window.imeEvents=[];window.keyEvents=[];for(const type of ['compositionstart','compositionupdate','compositionend'])document.addEventListener(type,e=>imeEvents.push({type,data:e.data}));document.addEventListener('keydown',e=>keyEvents.push({key:e.key,target:e.target.id,composing:e.isComposing}));</script>
            """.Replace("V3 local browser input fixture", pageTitle, StringComparison.Ordinal), Encoding.UTF8);
        string profile = Path.Combine(root, "profile");
        var startupMessages = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var start = new ProcessStartInfo(chrome) { UseShellExecute = false, RedirectStandardError = true };
        foreach (string arg in new[] { "--user-data-dir=" + profile, "--remote-debugging-port=0", "--remote-debugging-address=127.0.0.1",
            "--no-first-run", "--no-default-browser-check", "--disable-background-networking", "--disable-component-update", "--enable-logging=stderr", "--new-window", new Uri(page).AbsoluteUri })
            start.ArgumentList.Add(arg);
        using var browser = Process.Start(start) ?? throw new InvalidOperationException("Could not start fixture Chrome");
        int browserPid = 0;
        Process? verifiedBrowser = null;
        browser.ErrorDataReceived += (_, e) => { if (e.Data != null && startupMessages.Count < 200) startupMessages.Enqueue(e.Data); };
        browser.BeginErrorReadLine();
        try
        {
            string portFile = Path.Combine(profile, "DevToolsActivePort");
            int port = 0;
            Wait(() => {
                // Chromium can successfully relaunch at normal user privilege
                // and exit this initial launcher with code 0. Keep its normal
                // security behavior; identify the replacement by our unique
                // profile endpoint, exact local URL and unique window title.
                if (browser.HasExited && browser.ExitCode != 0) throw new InvalidOperationException($"Fixture Chrome exited during startup: {browser.ExitCode}");
                // Existence precedes the browser closing/flushing this file.
                // Wait for a complete endpoint, not a retry of failed typing.
                try
                {
                    string[] lines = File.ReadAllLines(portFile);
                    return lines.Length >= 2 && lines[1].StartsWith("/devtools/browser/", StringComparison.Ordinal)
                        && int.TryParse(lines[0], out port) && port is > 0 and <= 65535;
                }
                catch (IOException) { return false; }
            }, "Chrome exposes only its isolated profile debugger", 60000);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            string? debugger = null;
            // The port file can be complete before Chrome's HTTP target service
            // answers. This bounded readiness probe precedes every test key.
            Wait(() => {
                if (browser.HasExited && browser.ExitCode != 0) throw new InvalidOperationException("Fixture browser exited before exposing its page");
                try
                {
                    using var targets = JsonDocument.Parse(http.GetStringAsync($"http://127.0.0.1:{port}/json/list").GetAwaiter().GetResult());
                    var pages = targets.RootElement.EnumerateArray().Where(t => t.GetProperty("type").GetString() == "page"
                        && t.GetProperty("url").GetString() == new Uri(page).AbsoluteUri).ToArray();
                    if (pages.Length != 1) return false;
                    debugger = pages[0].GetProperty("webSocketDebuggerUrl").GetString();
                    return debugger != null;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return false; }
            }, "Browser debugger serves exactly the local fixture target", 60000);
            using var cdp = new Cdp(debugger!);
            Wait(() => cdp.Value("document.title") == pageTitle, "Chrome loaded only the local test page");
            nint window = 0;
            Wait(() => {
                Native.EnumWindows((h, _) => {
                    Native.GetWindowThreadProcessId(h, out uint pid);
                    if (!Native.IsWindowVisible(h)) return true;
                    var name = new StringBuilder(128); var title = new StringBuilder(256);
                    Native.GetClassNameW(h, name, name.Capacity); Native.GetWindowTextW(h, title, title.Capacity);
                    // Chrome also owns visible utility/compositor windows.
                    // Select the actual main window for our local document.
                    if (name.ToString() == "Chrome_WidgetWin_1"
                        && title.ToString().Contains(pageTitle, StringComparison.Ordinal))
                    {
                        var candidate = Process.GetProcessById(checked((int)pid));
                        if (!string.Equals(candidate.MainModule?.FileName, chrome, StringComparison.OrdinalIgnoreCase))
                        { candidate.Dispose(); throw new InvalidOperationException("Unique fixture window is not owned by the installed browser"); }
                        // Hold its process handle through cleanup so a recycled
                        // numeric PID can never target an unrelated process.
                        _ = candidate.Handle;
                        verifiedBrowser?.Dispose(); verifiedBrowser = candidate;
                        window = h; browserPid = candidate.Id;
                    }
                    return true;
                }, 0);
                return window != 0;
            }, "The new Chrome process owns a visible test window");
            Console.WriteLine($"Verified local-page browser PID={browserPid}; initial launcher PID={browser.Id}");
            Native.SetForegroundWindow(window);
            Wait(() => Native.GetForegroundWindow() == window, "Only the isolated Chrome window receives browser test keys");
            uint browserThread = Native.GetWindowThreadProcessId(window, out _);
            nint browserLayout = Native.GetKeyboardLayout(browserThread);
            Console.WriteLine($"Chrome input thread HKL=0x{browserLayout:X}");
            Check((browserLayout.ToInt64() & 0xFFFF) == 0x411, "Chrome's input thread selected Japanese before typing");
            var baseline = new Dictionary<string, string>();
            foreach (bool enabled in new[] { enabledFirst, !enabledFirst })
            {
                guard.SetEnabled(enabled); Pump(400);
                foreach (string field in new[] { "text", "area" })
                {
                    bool legacy = Environment.GetEnvironmentVariable("GUARD_BROWSER_READY_MODE") == "legacy";
                    cdp.Value($"(()=>{{const e=document.getElementById('{field}');e.value='';window.imeEvents=[];window.keyEvents=[];{(legacy ? "e.focus();" : "")}return e.id;}})()");
                    if (!legacy) ClickInput(cdp, window, browserPid, field);
                    Wait(() => cdp.Value("document.activeElement.id") == field && cdp.Value("String(document.hasFocus())") == "true",
                        "Local Chrome field owns document focus: " + field);
                    if (!legacy) WaitForNativeInput(window, browserThread, false);
                    Key(window, 0x16); // VK_IME_ON in this browser's input context
                    if (string.Equals(Environment.GetEnvironmentVariable("GUARD_TEST_IME_CLSID"),
                        "03B5835F-F03C-411B-9CE2-AA23E1171E36", StringComparison.OrdinalIgnoreCase)) HiraganaShortcut(window);
                    if (!legacy) WaitForNativeInput(window, browserThread, true);
                    Console.WriteLine("Browser input immediately before first key: " + DescribeInput(browserThread));
                    foreach (char key in "NIHONN") { Key(window, key); Pump(100); }
                    string pending = cdp.Value($"document.getElementById('{field}').value");
                    // SendInput queues OS input; acceptance is not a renderer
                    // composition-delivery fence. Observe the one word already
                    // sent for a bounded interval, without sending any extra key
                    // or restarting an unsuccessful composition.
                    long deliveredBy = Environment.TickCount64 + 3000;
                    while (pending != "にほん" && Environment.TickCount64 < deliveredBy)
                    {
                        Pump(25);
                        pending = cdp.Value($"document.getElementById('{field}').value");
                    }
                    Console.WriteLine($"Chrome {field}, enabled={enabled}: pending={pending}; events=" + cdp.Value("JSON.stringify(window.imeEvents)"));
                    if (pending != "にほん") Console.WriteLine("Own browser key trace: " + cdp.Value("JSON.stringify(window.keyEvents)"));
                    Check(pending == "にほん", "Chrome receives real Japanese preedit in " + field
                        + (pending == "にほん" ? "" : $"; enabled={enabled}; pending={pending}; native={DescribeInput(browserThread)}; events="
                            + cdp.Value("JSON.stringify(window.imeEvents)") + "; keys=" + cdp.Value("JSON.stringify(window.keyEvents)")));
                    Key(window, 0x20); Pump(200); Key(window, 0x20); Pump(200); Key(window, 0x26); Key(window, 0x0D); Pump(300);
                    Wait(() => cdp.Value("String(window.imeEvents.some(e=>e.type==='compositionend'))") == "true",
                        "The already-sent conversion reaches the browser renderer", 5000);
                    string result = cdp.Value($"document.getElementById('{field}').value");
                    Check(cdp.Value("String(window.imeEvents.some(e=>e.type==='compositionend'))") == "true" && result.Any(c => c > 0x3000),
                        "Chrome receives an actual IME composition-end and Japanese commit in " + field);
                    if (enabled == enabledFirst) baseline[field] = result;
                    else Check(result == baseline[field] && guard.CheckError() == null, "V3 preserves Chrome's baseline conversion in " + field
                        + $"; enabled={enabled}; expected={JsonSerializer.Serialize(baseline[field])}; actual={JsonSerializer.Serialize(result)}; helper={guard.CheckError() ?? "running"}; events="
                        + cdp.Value("JSON.stringify(window.imeEvents)") + "; keys=" + cdp.Value("JSON.stringify(window.keyEvents)"));
                    Console.WriteLine($"Chrome {field}, enabled={enabled}: committed={result}");
                }
            }
            Check(true, "Ordinary Chrome text input and textarea work with V3 off/on");
        }
        catch
        {
            Console.WriteLine("Browser startup diagnostics: " + string.Join(Environment.NewLine, startupMessages));
            // Entry is restricted to the disposable VM. Preserve the failing
            // browser state before closing this unique test profile.
            var bounds = Screen.PrimaryScreen!.Bounds;
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            bitmap.Save(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!, "v3-ime-browser-failure.png"));
            throw;
        }
        finally
        {
            guard.SetEnabled(false);
            // Only the process launched with this new, unique test profile.
            if (verifiedBrowser != null)
            {
                try
                {
                    if (!verifiedBrowser.HasExited) { verifiedBrowser.Kill(entireProcessTree: true); verifiedBrowser.WaitForExit(5000); }
                }
                finally { verifiedBrowser.Dispose(); }
            }
            if (!browser.HasExited) { browser.Kill(entireProcessTree: true); browser.WaitForExit(5000); }
        }
    }
    private static void ClickInput(Cdp cdp, nint window, int process, string field)
    {
        // Real pointer focus updates Chrome's native text-input client as well
        // as renderer DOM focus. Only the disposable browser can receive it.
        using var geometry = JsonDocument.Parse(cdp.Value($"JSON.stringify((()=>{{const r=document.getElementById('{field}').getBoundingClientRect();return {{x:r.x+r.width/2,y:r.y+r.height/2,scale:devicePixelRatio}};}})())"));
        nint render = 0;
        Native.EnumChildWindows(window, (h, _) => {
            var cls = new StringBuilder(128); Native.GetClassNameW(h, cls, cls.Capacity);
            if (cls.ToString() == "Chrome_RenderWidgetHostHWND" && Native.IsWindowVisible(h)) render = h;
            return true;
        }, 0);
        Check(render != 0, "The local browser owns a native render view");
        var g = geometry.RootElement;
        var marker = new Native.Point { X = (int)(18 * g.GetProperty("scale").GetDouble()), Y = (int)(18 * g.GetProperty("scale").GetDouble()) };
        Check(Native.ClientToScreen(render, ref marker), "Own rendered-page marker maps to this browser");
        // A loaded DOM and existing HWND can precede Chrome's first visible
        // frame. Do not click an unpainted startup surface and call it focus.
        using (var sample = new Bitmap(1, 1))
        using (var graphics = Graphics.FromImage(sample))
            Wait(() => {
                graphics.CopyFromScreen(new Point(marker.X, marker.Y), Point.Empty, new Size(1, 1));
                var pixel = sample.GetPixel(0, 0);
                return Math.Abs(pixel.R - 33) <= 2 && Math.Abs(pixel.G - 77) <= 2 && Math.Abs(pixel.B - 155) <= 2;
            }, "The local fixture has actually painted before any input", 30000);
        var point = new Native.Point { X = (int)(g.GetProperty("x").GetDouble() * g.GetProperty("scale").GetDouble()),
            Y = (int)(g.GetProperty("y").GetDouble() * g.GetProperty("scale").GetDouble()) };
        Check(Native.ClientToScreen(render, ref point), "Test field coordinates belong to the browser render view");
        Native.GetWindowThreadProcessId(Native.WindowFromPoint(point), out uint owner);
        Check(owner == process && Native.GetForegroundWindow() == window, "Only this disposable browser receives its focus click");
        Check(Native.SetCursorPos(point.X, point.Y), "Disposable pointer targets the local test field");
        var down = new Native.Input { Type = 0, Data = new Native.InputUnion { Mouse = new Native.Mouse { Flags = 2 } } };
        var up = down; up.Data.Mouse.Flags = 4;
        Check(Native.SendInput(2, [down, up], Marshal.SizeOf<Native.Input>()) == 2, "Local test field receives its native focus click");
        Pump(300);
    }
    private static string DescribeInput(uint thread)
    {
        var info = new CompositionGuard.Native.GuiInfo { Size = (uint)Marshal.SizeOf<CompositionGuard.Native.GuiInfo>() };
        if (!CompositionGuard.Native.GetGUIThreadInfo(thread, ref info)) return "GUI thread unavailable";
        nint ime = ImmGetDefaultIMEWnd(info.Focus);
        nint open = 0, conversion = 0;
        bool queried = ime != 0 && SendMessageTimeoutW(ime, 0x283, 5, 0, 2, 100, out open) != 0;
        if (ime != 0) SendMessageTimeoutW(ime, 0x283, 1, 0, 2, 100, out conversion);
        return $"focus=0x{info.Focus:X}; ime=0x{ime:X}; queried={queried}; open={open}; conversion=0x{conversion:X}";
    }
    private static void WaitForNativeInput(nint window, uint thread, bool requireOpen)
    {
        // DOM focus is renderer state. Chrome updates its Windows text-input
        // client asynchronously, so it is not an input-service readiness fence.
        // Read only this disposable browser's native focus and IME state. Do not
        // type a warm-up word, discard failures or retry the measured input.
        long deadline = Environment.TickCount64 + 5000;
        nint lastFocus = 0; long stableSince = Environment.TickCount64, nextConfigure = stableSince + 500;
        while (Environment.TickCount64 < deadline)
        {
            if (Native.GetForegroundWindow() != window) throw new InvalidOperationException("Chrome lost focus during native input preparation");
            var info = new CompositionGuard.Native.GuiInfo { Size = (uint)Marshal.SizeOf<CompositionGuard.Native.GuiInfo>() };
            bool ready = CompositionGuard.Native.GetGUIThreadInfo(thread, ref info) && info.Focus != 0;
            if (ready) { Native.GetWindowThreadProcessId(info.Focus, out uint pid); Native.GetWindowThreadProcessId(window, out uint owner); ready = pid == owner; }
            if (ready && requireOpen)
            {
                nint ime = ImmGetDefaultIMEWnd(info.Focus);
                ready = ime != 0 && SendMessageTimeoutW(ime, 0x283, 5, 0, 2, 100, out nint open) != 0 && open != 0;
                if (!ready && Environment.TickCount64 >= nextConfigure)
                {
                    // Idempotent mode setup only. Never resend a test character.
                    Console.WriteLine("Waiting for the browser's own IME-on configuration: " + DescribeInput(thread));
                    Key(window, 0x16);
                    nextConfigure = Environment.TickCount64 + 500;
                }
            }
            if (!ready || info.Focus != lastFocus) { stableSince = Environment.TickCount64; lastFocus = info.Focus; }
            if (ready && Environment.TickCount64 - stableSince >= 300)
            { Console.WriteLine("Native browser input ready: " + DescribeInput(thread)); return; }
            Pump(25);
        }
        throw new InvalidOperationException("Native Chrome input preparation failed: " + DescribeInput(thread));
    }
    [DllImport("imm32.dll")] private static extern nint ImmGetDefaultIMEWnd(nint window);
    [DllImport("user32.dll")] private static extern nint SendMessageTimeoutW(nint window, uint message, nint wp, nint lp, uint flags, uint timeout, out nint result);
    private static void Key(nint window, int key)
    {
        if (Native.GetForegroundWindow() != window) throw new InvalidOperationException("Chrome lost focus; browser test input stopped");
        var down = new Native.Input { Type = 1, Data = new Native.InputUnion { Keyboard = new Native.Keyboard { Key = (ushort)key } } };
        var up = down; up.Data.Keyboard.Flags = 2;
        Check(Native.SendInput(2, [down, up], Marshal.SizeOf<Native.Input>()) == 2, "Chrome fixture accepts its test key");
        Pump(60);
    }
    private static void HiraganaShortcut(nint window)
    {
        if (Native.GetForegroundWindow() != window) throw new InvalidOperationException("Chrome lost focus; IME shortcut stopped");
        Native.Input Input(ushort key, uint flags) => new() { Type = 1, Data = new Native.InputUnion { Keyboard = new Native.Keyboard { Key = key, Flags = flags } } };
        if (Native.SendInput(4, [Input(0x11, 0), Input(0x14, 0), Input(0x14, 2), Input(0x11, 2)], Marshal.SizeOf<Native.Input>()) != 4)
        {
            Native.SendInput(2, [Input(0x14, 2), Input(0x11, 2)], Marshal.SizeOf<Native.Input>());
            throw new InvalidOperationException("Chrome fixture Hiragana shortcut was not accepted");
        }
        Pump(300);
    }
    private static void Pump(int milliseconds)
    { long end = Environment.TickCount64 + milliseconds; do { Native.PumpMessages(); Thread.Sleep(10); } while (Environment.TickCount64 < end); }
    private static void Wait(Func<bool> predicate, string name, int timeoutMs = 15000)
    { long end = Environment.TickCount64 + timeoutMs; while (!predicate() && Environment.TickCount64 < end) Pump(50); Check(predicate(), name); }
    private static void Check(bool value, string name)
    { if (!value) throw new InvalidOperationException("FAIL " + name); Console.WriteLine("PASS " + name); }
    private sealed class Cdp : IDisposable
    {
        private readonly ClientWebSocket socket = new();
        private int id;
        internal Cdp(string address)
        {
            var uri = new Uri(address);
            if (!uri.IsLoopback) throw new InvalidOperationException("Fixture debugger must stay on loopback");
            using var timeout = new CancellationTokenSource(5000);
            socket.ConnectAsync(uri, timeout.Token).GetAwaiter().GetResult();
        }
        internal string Value(string expression)
        {
            int request = ++id;
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(new { id = request, method = "Runtime.evaluate", @params = new { expression, returnByValue = true } });
            using var timeout = new CancellationTokenSource(5000);
            socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, timeout.Token).GetAwaiter().GetResult();
            var buffer = new byte[16384];
            while (true)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult part;
                do
                {
                    part = socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token).GetAwaiter().GetResult();
                    if (part.MessageType == WebSocketMessageType.Close) throw new IOException("Fixture debugger disconnected");
                    message.Write(buffer, 0, part.Count);
                    if (message.Length > 1024 * 1024) throw new IOException("Unexpected fixture debugger message size");
                } while (!part.EndOfMessage);
                using var result = JsonDocument.Parse(message.ToArray());
                if (!result.RootElement.TryGetProperty("id", out var response) || response.GetInt32() != request) continue;
                if (result.RootElement.TryGetProperty("error", out _) || result.RootElement.GetProperty("result").TryGetProperty("exceptionDetails", out _))
                    throw new InvalidOperationException("Fixture debugger expression failed");
                return result.RootElement.GetProperty("result").GetProperty("result").GetProperty("value").GetString()!;
            }
        }
        public void Dispose() => socket.Dispose();
    }
}
