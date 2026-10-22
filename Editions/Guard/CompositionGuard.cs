using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

// Deliberately limited to the Windows TSF fallback composition surface. Never
// match a generic IME/renderer/candidate window or inspect a composition string.
internal static class CompositionGuardPolicy
{
    internal const string WindowClass = "MSCTFIME Composition";
    internal static bool ShouldMask(string windowClass, Rectangle bounds, Rectangle monitor,
        Rectangle? caret, bool visible, bool sameThread, bool popup, uint dpi)
    {
        if (windowClass != WindowClass || !visible || !sameThread || !popup
            || bounds.Width <= 0 || bounds.Height <= 0 || monitor.Width <= 0 || monitor.Height <= 0) return false;
        int margin = (int)(16 * Math.Clamp(dpi, 96u, 480u) / 96);
        if (Math.Abs((long)bounds.Left - monitor.Left) > margin
            || Math.Abs((long)bounds.Top - monitor.Top) > margin) return false;
        // A legitimate caret at the screen edge must keep its composition UI.
        if (caret is { } position)
        {
            position.Inflate(margin * 2, margin * 2);
            if (position.IntersectsWith(bounds)) return false;
        }
        return true;
    }
}

// Separate helper: closing stdin (including parent failure) restores all masks.
// Main routing/UI threads never wait on another application's window procedure.
internal sealed class CompositionGuardClient : IDisposable
{
    private Process? worker;
    private bool recoveryRequested;
    internal string? Error { get; private set; }
    internal string? CheckError()
    {
        if (worker is { HasExited: true })
        {
            Error = "Composition helper stopped; reopen Settings to retry.";
            RequestRecovery();
        }
        return Error;
    }
    internal void SetEnabled(bool enabled)
    {
        if (!enabled) { Error = null; Dispose(); return; }
        if (worker is { HasExited: false }) return;
        if (worker != null) { Error = "Composition helper stopped; reopen Settings to retry."; return; }
        try
        {
            worker = Process.Start(StartInfo(false)) ?? throw new InvalidOperationException("Could not start composition helper.");
            recoveryRequested = false;
            Error = null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        { Error = "Could not start composition helper."; }
    }
    public void Dispose()
    {
        if (worker != null)
        {
            try { worker.StandardInput.Close(); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException) { }
            // Do not kill the helper: it owns the restoration records.
            worker.Dispose(); worker = null;
        }
        RequestRecovery();
    }
    private static ProcessStartInfo StartInfo(bool recovery)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = !recovery };
        if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(CompositionGuardClient).Assembly.Location);
        start.ArgumentList.Add(recovery ? "--composition-guard-recover" : "--composition-guard-worker");
        return start;
    }
    private void RequestRecovery()
    {
        if (recoveryRequested) return;
        try
        {
            if (new CompositionRecovery().HasRecords)
            { using var recovery = Process.Start(StartInfo(true)); recoveryRequested = true; }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { Error = "Display recovery could not start. Restart V3 to retry."; }
    }
    internal static bool TryRunWorker(string[] args, Func<CompositionGuard>? guardFactory = null, CompositionRecovery? recovery = null)
    {
        if (args.Length != 1 || args[0] is not ("--composition-guard-worker" or "--composition-guard-recover")) return false;
        bool restoreOnly = args[0] == "--composition-guard-recover";
        // A manually launched helper must never run unattended on the desktop.
        if (!restoreOnly && !Console.IsInputRedirected) return true;
        // Worker entry precedes the GUI's DPI setup. Read all window/monitor
        // coordinates in physical pixels, including on mixed-DPI monitors.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        // Console.In's synchronized TextReader can perform ReadLineAsync
        // synchronously. Keep pipe waiting off the monitoring thread.
        var end = restoreOnly ? null : Task.Run(() => Console.In.Read());
        using var instance = new Mutex(false, @"Local\ImeLayoutRouter.CompositionGuard");
        long deadline = Environment.TickCount64 + 5000;
        try
        {
            while (!instance.WaitOne(100))
            {
                if (end?.IsCompleted == true) return true;
                if (restoreOnly && Environment.TickCount64 > deadline) { Environment.ExitCode = 3; return true; }
            }
        }
        catch (AbandonedMutexException) { }
        try
        {
            var store = recovery ?? new CompositionRecovery();
            if (!store.Recover()) { Environment.ExitCode = 2; return true; }
            if (restoreOnly) return true;
            using var guard = guardFactory?.Invoke() ?? new CompositionGuard(recovery: store);
            using var events = new CompositionGuardEvents();
            events.Run(guard, end!);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        { Environment.ExitCode = 1; }
        finally { instance.ReleaseMutex(); }
        return true;
    }
}

internal sealed class CompositionGuard : IDisposable
{
    private readonly Dictionary<nint, CompositionWindowMask> masks = [];
    private readonly HashSet<nint> yielded = [];
    private readonly Func<nint> foregroundReader;
    private readonly CompositionRecovery recovery;
    internal CompositionGuard(Func<nint>? foregroundReader = null, CompositionRecovery? recovery = null)
    { this.foregroundReader = foregroundReader ?? Native.GetForegroundWindow; this.recovery = recovery ?? new CompositionRecovery(); }
    internal void Tick()
    {
        var foreground = foregroundReader();
        uint thread = Native.GetWindowThreadProcessId(foreground, out uint pid);
        var info = new Native.GuiInfo { Size = (uint)Marshal.SizeOf<Native.GuiInfo>() };
        Rectangle? caret = null;
        if (thread != 0 && Native.GetGUIThreadInfo(thread, ref info) && info.Caret != 0)
        {
            var point = new Native.Point { X = info.CaretRect.Left, Y = info.CaretRect.Top };
            if (Native.ClientToScreen(info.Caret, ref point))
                caret = new Rectangle(point.X, point.Y, Math.Max(1, info.CaretRect.Right - info.CaretRect.Left),
                    Math.Max(1, info.CaretRect.Bottom - info.CaretRect.Top));
        }
        var eligible = new HashSet<nint>();
        if (thread != 0)
            Native.EnumThreadWindows(thread, (window, _) =>
            {
                if (Eligible(window, thread, pid, caret)) eligible.Add(window);
                return true;
            }, 0);
        foreach (var pair in masks.ToArray())
        {
            if (eligible.Contains(pair.Key) && pair.Value.IsIntact) continue;
            if (eligible.Contains(pair.Key)) yielded.Add(pair.Key);
            if (pair.Value.TryRestore()) { pair.Value.Dispose(); masks.Remove(pair.Key); }
        }
        yielded.IntersectWith(eligible);
        foreach (var window in eligible)
        {
            if (masks.ContainsKey(window) || yielded.Contains(window) || masks.Count >= 8) continue;
            // Recheck the foreground immediately before any change.
            if (foregroundReader() != foreground) break;
            if (CompositionWindowMask.TryCreate(window, recovery) is { } mask) masks.Add(window, mask);
        }
    }
    private static bool Eligible(nint window, uint thread, uint pid, Rectangle? caret)
    {
        uint candidateThread = Native.GetWindowThreadProcessId(window, out uint candidatePid);
        var name = new StringBuilder(128);
        Native.GetClassNameW(window, name, name.Capacity);
        if (name.ToString() != CompositionGuardPolicy.WindowClass || (Native.GetClassLongW(window, -26) & 0x10000) == 0
            || !Native.GetWindowRect(window, out var rect)) return false;
        var monitor = new Native.MonitorInfo { Size = (uint)Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfoW(Native.MonitorFromWindow(window, 0), ref monitor)) return false;
        var style = unchecked((uint)Native.GetWindowLongW(window, -16));
        return CompositionGuardPolicy.ShouldMask(name.ToString(), rect.Rectangle, monitor.Bounds.Rectangle, caret,
            Native.IsWindowVisible(window), candidateThread == thread && candidatePid == pid,
            (style & 0x80000000) != 0 && (style & 0x40000000) == 0, Native.GetDpiForWindow(window));
    }
    public void Dispose()
    {
        foreach (var mask in masks.Values) mask.Dispose();
        masks.Clear();
    }
    internal static class Native
    {
        internal delegate bool EnumProc(nint window, nint parameter);
        [StructLayout(LayoutKind.Sequential)] internal struct Rect
        { internal int Left, Top, Right, Bottom; internal readonly Rectangle Rectangle => Rectangle.FromLTRB(Left, Top, Right, Bottom); }
        [StructLayout(LayoutKind.Sequential)] internal struct Point { internal int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct GuiInfo
        { internal uint Size, Flags; internal nint Active, Focus, Capture, Menu, Move, Caret; internal Rect CaretRect; }
        [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo
        { internal uint Size; internal Rect Bounds, Work; internal uint Flags; }
        [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint pid);
        [DllImport("user32.dll")] internal static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
        [DllImport("user32.dll")] internal static extern bool ClientToScreen(nint window, ref Point point);
        [DllImport("user32.dll")] internal static extern bool EnumThreadWindows(uint thread, EnumProc callback, nint parameter);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassNameW(nint window, StringBuilder name, int count);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint window, out Rect rect);
        [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint window, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);
        [DllImport("user32.dll")] internal static extern int GetWindowLongW(nint window, int index);
        [DllImport("user32.dll")] internal static extern uint GetClassLongW(nint window, int index);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint window);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint window);
    }
}
