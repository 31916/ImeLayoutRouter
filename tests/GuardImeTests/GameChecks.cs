using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

// Fixed input only, in an official portable OpenTTD instance on the test VM.
// Game-owned console logs establish committed text; screenshots retain preedit
// and candidate evidence. Production never reads input or sends any keys.
internal static class GameChecks
{
    internal static void Run(CompositionGuardClient guard, Action restoreFixtureFocus)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            throw new InvalidOperationException("Game input is restricted to the disposable runner");
        string directory = Environment.GetEnvironmentVariable("GUARD_TEST_GAME_DIR") ?? throw new InvalidOperationException("Pinned test game missing");
        string evidence = Environment.GetEnvironmentVariable("RUNNER_TEMP")!;
        Directory.CreateDirectory(Path.Combine(directory, "scripts"));
        foreach (bool fullscreen in new[] { false, true })
        {
            restoreFixtureFocus(); Tsf.ActivateJapanese(forTestDesktop: true); Pump(500);
            string label = fullscreen ? "fullscreen" : "windowed";
            string log = Path.Combine(directory, "v3-" + label + ".log");
            File.WriteAllText(Path.Combine(directory, "scripts", "autoexec.scr"), $"script v3-{label}.log\necho V3_READY\n", new UTF8Encoding(false));
            string config = Path.Combine(directory, "v3.cfg");
            File.WriteAllText(config, $"[misc]\nfullscreen = {fullscreen.ToString().ToLowerInvariant()}\nresolution = 1024,768\n[game_creation]\nmap_x = 6\nmap_y = 6\n[gui]\nshow_survey = false\n", new UTF8Encoding(false));
            var start = new ProcessStartInfo(Path.Combine(directory, "openttd.exe")) { UseShellExecute = false, WorkingDirectory = directory };
            foreach (string value in new[] { "-X", "-x", "-c", config, "-v", "win32", "-b", "32bpp-anim", "-m", "null", "-s", "null", "-g", "-G", "17" }) start.ArgumentList.Add(value);
            using var game = Process.Start(start) ?? throw new InvalidOperationException("Test game did not launch");
            nint window = 0;
            try
            {
                Wait(() => {
                    if (game.HasExited) throw new InvalidOperationException("OpenTTD exited during startup");
                    Native.EnumWindows((h, _) => {
                        Native.GetWindowThreadProcessId(h, out uint pid);
                        var cls = new StringBuilder(128); Native.GetClassNameW(h, cls, cls.Capacity);
                        if (pid == game.Id && cls.ToString() == "OTTD" && Native.IsWindowVisible(h)) window = h;
                        return true;
                    }, 0);
                    return window != 0 && File.Exists(log) && Read(log).Contains("V3_READY", StringComparison.Ordinal);
                }, "Official OpenTTD 15.3 launched its own local game", 60000);
                Native.SetForegroundWindow(window);
                Wait(() => Native.GetForegroundWindow() == window, "Only the test game receives keys");
                Pump(1000);
                if (fullscreen)
                {
                    Check(Native.GetWindowRect(window, out var bounds) && bounds.Left == 0 && bounds.Top == 0
                        && (unchecked((uint)Native.GetWindowLongW(window, -16)) & 0x80000000) != 0,
                        "OpenTTD actually entered its fullscreen display mode");
                }
                Key(window, 0x1A); // VK_IME_OFF for the ASCII console command
                Send(window, new Native.Keyboard { Scan = 41, Flags = 8 }); // game's documented physical console key
                Pump(500);
                foreach (bool enabled in new[] { false, true })
                {
                    guard.SetEnabled(enabled); Pump(400);
                    Key(window, 0x1A);
                    foreach (char key in "ECHO ") Key(window, key);
                    Key(window, 0x16); Pump(500);
                    foreach (char key in "NIHONN") { Key(window, key); Pump(100); }
                    Capture(evidence, $"v3-ime-game-{label}-{enabled}-preedit.png");
                    Key(window, 0x20); Pump(200); Key(window, 0x20); Pump(200);
                    Capture(evidence, $"v3-ime-game-{label}-{enabled}-candidates.png");
                    Key(window, 0x26); Key(window, 0x0D); Pump(200); Key(window, 0x0D);
                    int expected = enabled ? 2 : 1;
                    Wait(() => Read(log).Split('\n').Count(line => line.Trim() == "日本") == expected,
                        $"OpenTTD commits the actual Japanese conversion with V3 enabled={enabled}, fullscreen={fullscreen}");
                    Check(guard.CheckError() == null, "V3 helper remains healthy during actual game input");
                }
                File.Copy(log, Path.Combine(evidence, $"v3-ime-game-{label}.txt"), true);
            }
            catch
            {
                Capture(evidence, $"v3-ime-game-{label}-failure.png");
                if (File.Exists(log)) File.Copy(log, Path.Combine(evidence, $"v3-ime-game-{label}.txt"), true);
                throw;
            }
            finally
            {
                guard.SetEnabled(false);
                if (!game.HasExited) { game.Kill(entireProcessTree: true); game.WaitForExit(5000); }
                restoreFixtureFocus();
            }
        }
    }
    private static string Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8); return reader.ReadToEnd();
    }
    private static void Capture(string directory, string name)
    {
        var bounds = Screen.PrimaryScreen!.Bounds;
        using var image = new Bitmap(bounds.Width, bounds.Height);
        using (var drawing = Graphics.FromImage(image)) drawing.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        image.Save(Path.Combine(directory, name));
    }
    private static void Key(nint window, int key) => Send(window, new Native.Keyboard { Key = (ushort)key });
    private static void Send(nint window, Native.Keyboard keyboard)
    {
        if (Native.GetForegroundWindow() != window) throw new InvalidOperationException("Test game lost focus; input stopped");
        var down = new Native.Input { Type = 1, Data = new Native.InputUnion { Keyboard = keyboard } };
        var up = down; up.Data.Keyboard.Flags |= 2;
        Check(Native.SendInput(2, [down, up], Marshal.SizeOf<Native.Input>()) == 2, "Own game receives its fixed test key"); Pump(80);
    }
    private static void Pump(int ms) { long end = Environment.TickCount64 + ms; do { Native.PumpMessages(); Thread.Sleep(10); } while (Environment.TickCount64 < end); }
    private static void Wait(Func<bool> condition, string label, int ms = 10000)
    { long end = Environment.TickCount64 + ms; while (!condition() && Environment.TickCount64 < end) Pump(100); Check(condition(), label); }
    private static void Check(bool condition, string label)
    { if (!condition) throw new InvalidOperationException("FAIL " + label); Console.WriteLine("PASS " + label); }
}
