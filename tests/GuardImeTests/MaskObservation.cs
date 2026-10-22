using System.Runtime.InteropServices;

// Test-only timing on the disposable VM. Event time marks Windows announcing
// the popup, not its first rendered pixel. Polling adds observation delay.
internal sealed class MaskObservation : IDisposable
{
    private readonly WinEvent callback;
    private readonly nint hook;
    private nint window;
    private uint shown;
    private bool measured;
    internal uint? Milliseconds { get; private set; }
    internal MaskObservation()
    {
        callback = (_, _, h, objectId, childId, _, time) => {
            if (window != 0 || objectId != 0 || childId != 0) return;
            uint style = unchecked((uint)Native.GetWindowLongW(h, -16));
            if ((style & 0x80000000) == 0 || (style & 0x40000000) != 0 || !CompositionGuard.IsCompositionSurface(h)) return;
            window = h; shown = time;
        };
        hook = SetWinEventHook(0x8002, 0x8002, 0, callback, (uint)Environment.ProcessId, Native.GetCurrentThreadId(), 0);
        if (hook == 0) throw new InvalidOperationException("Fixture could not observe its own popup show event");
    }
    internal void Sample()
    {
        if (window == 0 || measured || !Native.IsWindow(window)) return;
        nint region = Native.CreateRectRgn(0, 0, 0, 0);
        if (region == 0) throw new InvalidOperationException("Fixture could not inspect popup drawing region");
        try
        {
            if (Native.GetWindowRgn(window, region) != 1) return;
            Milliseconds = unchecked((uint)Environment.TickCount - shown);
            measured = true;
        }
        finally { Native.DeleteObject(region); }
    }
    public void Dispose()
    {
        UnhookWinEvent(hook);
        GC.KeepAlive(callback);
    }
    private delegate void WinEvent(nint hook, uint id, nint window, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEvent callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
}
