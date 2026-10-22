using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static Tests;

// Synthetic native windows, never real user windows. This validates Windows
// API behavior and restoration, NOT keyboard composition/IME compatibility.
static class NativeChecks
{
    private static readonly WindowProc Procedure = DefWindowProcW;
    internal static void EnsureIsolated()
    {
        var name = new StringBuilder(256);
        if (!GetUserObjectInformationW(GetThreadDesktop(GetCurrentThreadId()), 2, name, 512, out _)
            || !name.ToString().StartsWith("ImeGuardTest-", StringComparison.Ordinal))
            throw new InvalidOperationException("Native checks require the dedicated, non-displayed test desktop.");
    }
    internal static void Run()
    {
        EnsureIsolated();
        var recovery = new CompositionRecovery(Path.Combine(AppContext.BaseDirectory, "native-recovery-" + Guid.NewGuid().ToString("N")));
        Register(CompositionGuardPolicy.WindowClass, 0x10000);
        Register("ImeGuardTestCandidate", 0);
        Register("ImeGuardTestOrdinary", 0);
        using var fixture = new Window("ImeGuardTestOrdinary", 400, 300);
        using var corner = new Window(CompositionGuardPolicy.WindowClass, 0, 0);
        using var candidate = new Window("ImeGuardTestCandidate", 0, 0);
        using var ordinary = new Window("ImeGuardTestOrdinary", 0, 0);
        using var normal = new Window(CompositionGuardPolicy.WindowClass, 700, 400);
        using var guard = new CompositionGuard(() => fixture.Handle, recovery);
        guard.Tick();
        Check(RegionType(corner.Handle) == 1, "Native guard masks the eligible corner fixture");
        Check(RegionType(candidate.Handle) == 0 && RegionType(ordinary.Handle) == 0 && RegionType(normal.Handle) == 0,
            "Native candidate, ordinary and normally positioned windows remain unchanged");
        Check(IsWindowVisible(corner.Handle), "Mask preserves the owner's WS_VISIBLE state");
        guard.Dispose();
        Check(RegionType(corner.Handle) == 0, "Stopping restores the original no-region state");

        var initialRegion = CreateRectRgn(2, 3, 90, 22);
        Check(SetWindowRgn(corner.Handle, initialRegion, true) != 0, "Fixture starts with a custom region");
        using (var mask = CompositionWindowMask.TryCreate(corner.Handle, recovery) ?? throw new Exception("Could not create mask"))
        { Check(mask.IsIntact, "Custom-region window is masked"); }
        using (var region = new Region())
        {
            GetWindowRgn(corner.Handle, region.Handle);
            GetRgnBox(region.Handle, out var bounds);
            Check(bounds.Left == 2 && bounds.Top == 3 && bounds.Right == 90 && bounds.Bottom == 22, "Custom region is restored exactly");
        }
        SetWindowRgn(corner.Handle, 0, true);
        using (var mask = CompositionWindowMask.TryCreate(corner.Handle, recovery) ?? throw new Exception("Could not create mask"))
        {
            ShowWindow(corner.Handle, 0);
            mask.TryRestore();
            Check(!IsWindowVisible(corner.Handle), "An owner-hidden popup is not resurrected during restoration");
        }
        ShowWindow(corner.Handle, 8);
        using (var mask = CompositionWindowMask.TryCreate(corner.Handle, recovery) ?? throw new Exception("Could not create mask"))
        {
            SetWindowRgn(corner.Handle, CreateRectRgn(9, 8, 70, 20), true);
            mask.TryRestore();
            using var region = new Region(); GetWindowRgn(corner.Handle, region.Handle); GetRgnBox(region.Handle, out var bounds);
            Check(bounds.Left == 9 && bounds.Top == 8, "Owner region changes take precedence over old saved state");
        }
        SetWindowRgn(corner.Handle, 0, true);
        using (var changing = new CompositionGuard(() => fixture.Handle, recovery))
        {
            changing.Tick();
            SetWindowPos(corner.Handle, 0, 500, 450, 130, 30, 0x14);
            changing.Tick();
            Check(RegionType(corner.Handle) == 0, "Moving composition to its normal position restores it");
        }
        SetWindowPos(corner.Handle, 0, 0, 0, 130, 30, 0x14);
        using (var disposable = new Window(CompositionGuardPolicy.WindowClass, 0, 0))
        using (var mask = CompositionWindowMask.TryCreate(disposable.Handle, recovery) ?? throw new Exception("Could not mask disposable"))
        {
            disposable.Dispose();
            Check(mask.TryRestore(), "Destroyed window needs no stale-handle restoration");
        }

        var start = new ProcessStartInfo(Environment.ProcessPath!)
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = true };
        if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(Tests).Assembly.Location);
        start.ArgumentList.Add("--test-guard-worker"); start.ArgumentList.Add(fixture.Handle.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(recovery.DirectoryPath);
        using var child = Process.Start(start) ?? throw new Exception("Could not start fixture helper");
        try
        {
            Wait(() => RegionType(corner.Handle) == 1 || child.HasExited);
            Check(!child.HasExited && RegionType(corner.Handle) == 1, "Real helper monitors while its parent pipe remains open (cross-process mask)");
            child.StandardInput.Close();
            Wait(() => child.HasExited);
            Check(child.ExitCode == 0 && RegionType(corner.Handle) == 0, "Parent-pipe EOF restores cross-process mask and exits helper");
        }
        finally
        {
            if (!child.HasExited) { child.StandardInput.Close(); Wait(() => child.HasExited); }
        }
        Check(RegionType(candidate.Handle) == 0 && IsWindowVisible(candidate.Handle), "Candidate fixture stays visible through worker shutdown");
        SetWindowRgn(corner.Handle, CreateRectRgn(4, 5, 80, 25), true);
        using (var crashing = Process.Start(start) ?? throw new Exception("Could not start crash fixture helper"))
        {
            try
            {
                Wait(() => RegionType(corner.Handle) == 1 || crashing.HasExited);
                Check(!crashing.HasExited && recovery.HasRecords, "Recovery journal is durable before a helper failure");
                // Only this synthetic helper is terminated; it owns no user windows.
                crashing.Kill(); Wait(() => crashing.HasExited);
                Check(recovery.Recover(), "Journal recovers after abrupt helper termination");
                using var region = new Region(); GetWindowRgn(corner.Handle, region.Handle); GetRgnBox(region.Handle, out var bounds);
                Check(bounds.Left == 4 && bounds.Top == 5 && bounds.Right == 80 && bounds.Bottom == 25,
                    "Recovery reconstructs the original region after its owning process has died");
            }
            finally { if (!crashing.HasExited) { crashing.StandardInput.Close(); Wait(() => crashing.HasExited); } }
        }
        Check(!recovery.HasRecords, "Successful restoration removes recovery records");
        Directory.Delete(recovery.DirectoryPath);
        Console.WriteLine("All isolated native fixture checks passed. Real IME composition remains a separate validation gate.");
    }
    private static void Wait(Func<bool> done)
    {
        var until = Environment.TickCount64 + 5000;
        while (!done())
        {
            while (PeekMessageW(out var message, 0, 0, 0, 1)) { TranslateMessage(ref message); DispatchMessageW(ref message); }
            if (Environment.TickCount64 >= until) throw new TimeoutException("Isolated fixture timed out");
            Thread.Sleep(10);
        }
    }
    private static int RegionType(nint window) { using var region = new Region(); return GetWindowRgn(window, region.Handle); }
    private static void Register(string name, uint style)
    {
        var value = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(), Style = style,
            Procedure = Marshal.GetFunctionPointerForDelegate(Procedure), Instance = GetModuleHandleW(null), Name = name };
        if (RegisterClassExW(ref value) == 0) throw new System.ComponentModel.Win32Exception();
    }
    private sealed class Window : IDisposable
    {
        internal nint Handle;
        internal Window(string name, int x, int y)
        {
            Handle = CreateWindowExW(0x08000080, name, "synthetic fixture", 0x90000000, x, y, 130, 30, 0, 0, GetModuleHandleW(null), 0);
            if (Handle == 0) throw new System.ComponentModel.Win32Exception();
        }
        public void Dispose() { if (Handle != 0) DestroyWindow(Handle); Handle = 0; }
    }
    private sealed class Region : IDisposable
    {
        internal readonly nint Handle = CreateRectRgn(0, 0, 0, 0);
        public void Dispose() => DeleteObject(Handle);
    }
    private delegate nint WindowProc(nint window, uint message, nint wp, nint lp);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass
    { internal uint Size, Style; internal nint Procedure; internal int ClassExtra, WindowExtra; internal nint Instance, Icon, Cursor, Brush; internal string? Menu, Name; internal nint SmallIcon; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Message { internal nint Window; internal uint Id; internal nuint WParam; internal nint LParam; internal uint Time; internal int X, Y; internal uint Private; }
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandleW(string? name);
    [DllImport("user32.dll")] private static extern nint GetThreadDesktop(uint thread);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetUserObjectInformationW(nint value, int index, StringBuilder name, uint size, out uint needed);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassExW(ref WindowClass value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowExW(uint extended, string name, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern nint DefWindowProcW(nint window, uint message, nint wp, nint lp);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(nint window, nint region, bool redraw);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(nint window, nint region);
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern int GetRgnBox(nint region, out Rect rect);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint value);
    [DllImport("user32.dll")] private static extern bool PeekMessageW(out Message message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint DispatchMessageW(ref Message message);
}
