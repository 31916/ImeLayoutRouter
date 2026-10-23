using System.Diagnostics;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

// Ordinary Chrome inputs, using real OS keystrokes on the disposable VM.
// CDP only prepares/reads our local fixture; it never supplies composition text.
internal static class ChromeChecks
{
    internal static void Run(CompositionGuardClient guard)
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
        string chrome = new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 }
            .Select(folder => Path.Combine(Environment.GetFolderPath(folder), "Google", "Chrome", "Application", "chrome.exe"))
            .FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("The hosted image must provide Chrome");
        string root = Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!, "guard-chrome-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string page = Path.Combine(root, "input.html");
        File.WriteAllText(page, """
            <!doctype html><meta charset="utf-8"><title>V3 local browser input fixture</title>
            <style>body{font:24px sans-serif;margin:80px}input,textarea{font:28px sans-serif;display:block;margin:20px 0;width:600px}</style>
            <h1>Disposable V3 input validation</h1><label>Text<input id="text"></label><label>Multiline<textarea id="area"></textarea></label>
            <script>window.imeEvents=[];window.keyEvents=[];for(const type of ['compositionstart','compositionupdate','compositionend'])document.addEventListener(type,e=>imeEvents.push({type,data:e.data}));document.addEventListener('keydown',e=>keyEvents.push({key:e.key,target:e.target.id,composing:e.isComposing}));</script>
            """, Encoding.UTF8);
        string profile = Path.Combine(root, "profile");
        var start = new ProcessStartInfo(chrome) { UseShellExecute = false };
        foreach (string arg in new[] { "--user-data-dir=" + profile, "--remote-debugging-port=0", "--remote-debugging-address=127.0.0.1",
            "--no-first-run", "--no-default-browser-check", "--disable-background-networking", "--disable-component-update", "--new-window", new Uri(page).AbsoluteUri })
            start.ArgumentList.Add(arg);
        using var browser = Process.Start(start) ?? throw new InvalidOperationException("Could not start fixture Chrome");
        try
        {
            string portFile = Path.Combine(profile, "DevToolsActivePort");
            Wait(() => {
                if (browser.HasExited) throw new InvalidOperationException($"Fixture Chrome exited during startup: {browser.ExitCode}");
                return File.Exists(portFile);
            }, "Chrome exposes only its isolated profile debugger", 30000);
            int port = int.Parse(File.ReadAllLines(portFile)[0], System.Globalization.CultureInfo.InvariantCulture);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var targets = JsonDocument.Parse(http.GetStringAsync($"http://127.0.0.1:{port}/json/list").GetAwaiter().GetResult());
            var target = targets.RootElement.EnumerateArray().Single(t => t.GetProperty("type").GetString() == "page");
            using var cdp = new Cdp(target.GetProperty("webSocketDebuggerUrl").GetString()!);
            Wait(() => cdp.Value("document.title") == "V3 local browser input fixture", "Chrome loaded only the local test page");
            nint window = 0;
            Wait(() => {
                Native.EnumWindows((h, _) => {
                    Native.GetWindowThreadProcessId(h, out uint pid);
                    if (pid != browser.Id || !Native.IsWindowVisible(h)) return true;
                    var name = new StringBuilder(128); var title = new StringBuilder(256);
                    Native.GetClassNameW(h, name, name.Capacity); Native.GetWindowTextW(h, title, title.Capacity);
                    // Chrome also owns visible utility/compositor windows.
                    // Select the actual main window for our local document.
                    if (name.ToString() == "Chrome_WidgetWin_1"
                        && title.ToString().Contains("V3 local browser input fixture", StringComparison.Ordinal)) window = h;
                    return true;
                }, 0);
                return window != 0;
            }, "The new Chrome process owns a visible test window");
            Native.SetForegroundWindow(window);
            Wait(() => Native.GetForegroundWindow() == window, "Only the isolated Chrome window receives browser test keys");
            uint browserThread = Native.GetWindowThreadProcessId(window, out _);
            nint browserLayout = Native.GetKeyboardLayout(browserThread);
            Console.WriteLine($"Chrome input thread HKL=0x{browserLayout:X}");
            Check((browserLayout.ToInt64() & 0xFFFF) == 0x411, "Chrome's input thread selected Japanese before typing");
            var baseline = new Dictionary<string, string>();
            foreach (bool enabled in new[] { false, true })
            {
                guard.SetEnabled(enabled); Pump(400);
                foreach (string field in new[] { "text", "area" })
                {
                    cdp.Value($"(()=>{{const e=document.getElementById('{field}');e.value='';window.imeEvents=[];window.keyEvents=[];e.focus();return e.id;}})()");
                    Check(cdp.Value("document.activeElement.id") == field && cdp.Value("String(document.hasFocus())") == "true",
                        "Local Chrome field owns document focus: " + field);
                    bool legacy = Environment.GetEnvironmentVariable("GUARD_BROWSER_READY_MODE") == "legacy";
                    if (!legacy) WaitForNativeInput(window, browserThread, false);
                    Key(window, 0x16); // VK_IME_ON in this browser's input context
                    if (string.Equals(Environment.GetEnvironmentVariable("GUARD_TEST_IME_CLSID"),
                        "03B5835F-F03C-411B-9CE2-AA23E1171E36", StringComparison.OrdinalIgnoreCase)) HiraganaShortcut(window);
                    if (!legacy) WaitForNativeInput(window, browserThread, true);
                    Console.WriteLine("Browser input immediately before first key: " + DescribeInput(browserThread));
                    foreach (char key in "NIHONN") { Key(window, key); Pump(100); }
                    string pending = cdp.Value($"document.getElementById('{field}').value");
                    Console.WriteLine($"Chrome {field}, enabled={enabled}: pending={pending}; events=" + cdp.Value("JSON.stringify(window.imeEvents)"));
                    if (pending != "にほん") Console.WriteLine("Own browser key trace: " + cdp.Value("JSON.stringify(window.keyEvents)"));
                    Check(pending == "にほん", "Chrome receives real Japanese preedit in " + field);
                    Key(window, 0x20); Pump(200); Key(window, 0x20); Pump(200); Key(window, 0x26); Key(window, 0x0D); Pump(300);
                    string result = cdp.Value($"document.getElementById('{field}').value");
                    Check(cdp.Value("String(window.imeEvents.some(e=>e.type==='compositionend'))") == "true" && result.Any(c => c > 0x3000),
                        "Chrome receives an actual IME composition-end and Japanese commit in " + field);
                    if (!enabled) baseline[field] = result;
                    else Check(result == baseline[field] && guard.CheckError() == null, "V3 preserves Chrome's baseline conversion in " + field);
                    Console.WriteLine($"Chrome {field}, enabled={enabled}: committed={result}");
                }
            }
            Check(true, "Ordinary Chrome text input and textarea work with V3 off/on");
        }
        catch
        {
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
            if (!browser.HasExited) { browser.Kill(entireProcessTree: true); browser.WaitForExit(5000); }
        }
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
        nint lastFocus = 0; long stableSince = Environment.TickCount64;
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
