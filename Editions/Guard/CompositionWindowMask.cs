using System.Runtime.InteropServices;

// Preserve WS_VISIBLE and the IME buffer. When the IME hides its own window,
// restoring the region cannot accidentally show a stale composition popup.
internal sealed class CompositionWindowMask : IDisposable
{
    private const string Property = "ImeLayoutRouter.CompositionMask.Preview";
    private readonly nint window;
    private readonly nint token;
    private nint original;
    private readonly string journal;
    private bool restored;
    private CompositionWindowMask(nint window, nint token, nint original, string journal)
    { this.window = window; this.token = token; this.original = original; this.journal = journal; }

    internal static CompositionWindowMask? TryCreate(nint window, CompositionRecovery recovery)
    {
        if (GetPropW(window, Property) != 0 || !IsWindow(window)) return null;
        // Do not queue work to an already unresponsive target.
        if (SendMessageTimeoutW(window, 0, 0, 0, 2, 40, out _) == 0) return null;
        nint original = CreateRectRgn(0, 0, 0, 0);
        if (original == 0) return null;
        int kind = GetWindowRgn(window, original);
        Trace($"create hwnd={window:X} kind={kind}");
        if (kind == 1) { DeleteObject(original); return null; } // already invisible
        if (kind == 0) { DeleteObject(original); original = 0; } // no explicit region
        byte[]? regionData = null;
        if (original != 0)
        {
            uint size = GetRegionData(original, 0, null);
            if (size == 0 || size > 65536) { DeleteObject(original); return null; }
            regionData = new byte[size];
            if (GetRegionData(original, size, regionData) != size) { DeleteObject(original); return null; }
        }
        uint thread = CompositionGuard.Native.GetWindowThreadProcessId(window, out uint pid);
        if (thread == 0 || pid == 0) { if (original != 0) DeleteObject(original); return null; }
        nint token = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue);
        string journal;
        try { journal = recovery.Save(new(1, window.ToInt64(), token.ToInt64(), pid, thread, regionData)); }
        catch { if (original != 0) DeleteObject(original); throw; }
        if (!SetPropW(window, Property, token))
        { if (original != 0) DeleteObject(original); File.Delete(journal); return null; }
        nint empty = CreateRectRgn(0, 0, 0, 0);
        Trace($"mask hwnd={window:X} token={token} original={original:X}");
        if (empty == 0 || SetWindowRgn(window, empty, true) == 0)
        {
            if (empty != 0) DeleteObject(empty);
            if (original != 0) DeleteObject(original);
            RemovePropW(window, Property);
            File.Delete(journal);
            return null;
        }
        Trace($"masked hwnd={window:X} token={GetPropW(window, Property)} empty={IsEmpty(window)}");
        return new CompositionWindowMask(window, token, original, journal);
    }
    internal static bool Recover(CompositionMaskRecord record, string journal)
    {
        nint window = (nint)record.Window;
        uint thread = CompositionGuard.Native.GetWindowThreadProcessId(window, out uint pid);
        if (thread != record.ThreadId || pid != record.ProcessId || GetPropW(window, Property) != (nint)record.Token)
        { File.Delete(journal); return true; }
        var name = new System.Text.StringBuilder(128);
        CompositionGuard.Native.GetClassNameW(window, name, name.Capacity);
        // The exact PID, TID and random property token above bind this record
        // to our original window. Its children may already have disappeared.
        if (!CompositionGuardPolicy.IsSurfaceClass(name.ToString())) return false;
        nint original = record.Region == null ? 0 : ExtCreateRegion(0, (uint)record.Region.Length, record.Region);
        if (record.Region != null && original == 0) return false;
        var mask = new CompositionWindowMask(window, (nint)record.Token, original, journal);
        bool result = mask.TryRestore();
        mask.ReleaseOriginal(); // journal remains available if this attempt failed
        return result;
    }
    internal bool IsIntact => !restored && GetPropW(window, Property) == token && IsEmpty(window);
    internal bool TryRestore()
    {
        if (restored) return true;
        Trace($"restore hwnd={window:X} token={GetPropW(window, Property)} expected={token} original={original:X}");
        if (GetPropW(window, Property) != token) { ReleaseOriginal(); File.Delete(journal); restored = true; return true; }
        // If the owner replaced the region, preserve its more recent decision.
        if (IsEmpty(window))
        {
            if (SendMessageTimeoutW(window, 0, 0, 0, 2, 40, out _) == 0 && IsWindow(window)) return false;
            if (SetWindowRgn(window, original, true) == 0)
            {
                if (IsWindow(window)) return false;
            }
            else original = 0; // ownership transferred to Windows
            Trace($"restored-region hwnd={window:X} empty={IsEmpty(window)}");
        }
        RemovePropW(window, Property);
        ReleaseOriginal();
        File.Delete(journal);
        restored = true;
        Trace($"restored hwnd={window:X} empty={IsEmpty(window)}");
        return true;
    }
    private static bool IsEmpty(nint window)
    {
        nint region = CreateRectRgn(0, 0, 0, 0);
        if (region == 0) return false;
        try { int kind = GetWindowRgn(window, region); Trace($"region hwnd={window:X} kind={kind}"); return kind == 1; }
        finally { DeleteObject(region); }
    }
    private void ReleaseOriginal() { if (original != 0) DeleteObject(original); original = 0; }
    // Compiled out of the shipped application and real-IME fixture. Only the
    // synthetic native fixture retains bounded, geometry-only failure traces.
    [System.Diagnostics.Conditional("GUARD_NATIVE_TRACE")]
    private static void Trace(string message) => Console.WriteLine($"{Environment.TickCount64} {message}");
    public void Dispose()
    {
        // A failed restoration keeps its disk record for the next helper start.
        for (int attempt = 0; attempt < 3 && !TryRestore(); attempt++) Thread.Sleep(30);
        ReleaseOriginal();
    }
    [DllImport("gdi32.dll")] static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] static extern uint GetRegionData(nint region, uint size, [Out] byte[]? data);
    [DllImport("gdi32.dll")] static extern nint ExtCreateRegion(nint transform, uint size, byte[] data);
    [DllImport("user32.dll")] static extern int GetWindowRgn(nint window, nint region);
    [DllImport("user32.dll")] static extern int SetWindowRgn(nint window, nint region, bool redraw);
    [DllImport("user32.dll")] static extern bool IsWindow(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern nint GetPropW(nint window, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SetPropW(nint window, string name, nint value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern nint RemovePropW(nint window, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern nint SendMessageTimeoutW(nint window, uint message, nint wp, nint lp, uint flags, uint timeout, out nuint result);
}
