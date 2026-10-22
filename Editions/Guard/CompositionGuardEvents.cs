using System.Runtime.InteropServices;

// Out-of-context window notifications only: no injected DLL, keyboard hook,
// accessibility object, window title or composition text. Callbacks merely
// schedule a scan; masking/restoration never runs inside a reentrant callback.
internal sealed class CompositionGuardEvents : IDisposable
{
    private readonly WinEvent callback;
    private GCHandle callbackRoot;
    private readonly List<nint> hooks = [];
    private bool changed = true;
    internal CompositionGuardEvents()
    {
        callback = (_, eventId, window, objectId, childId, _, _) =>
        {
            if (window != 0 && (eventId == 3 || (objectId == 0 && childId == 0))) changed = true;
        };
        callbackRoot = GCHandle.Alloc(callback);
        Add(3, 3); // foreground changed
        Add(0x8001, 0x8003); // window destroyed/shown/hidden
        Add(0x800B, 0x800B); // window location changed
    }
    private void Add(uint first, uint last)
    {
        nint hook = SetWinEventHook(first, last, 0, callback, 0, 0, 2); // out of context, skip this helper
        if (hook != 0) hooks.Add(hook);
        // If notifications are unavailable, the periodic scan still works.
    }
    internal void Run(CompositionGuard guard, Task end)
    {
        long nextScan = 0, nextEventScan = 0;
        while (!end.IsCompleted)
        {
            for (int i = 0; i < 64 && PeekMessageW(out var message, 0, 0, 0, 1); i++)
            {
                if (message.Id == 0x12) return; // WM_QUIT: restore through caller's using scope
                TranslateMessage(ref message); DispatchMessageW(ref message);
            }
            if (end.IsCompleted) break;
            long now = Environment.TickCount64;
            if (now >= nextScan || (changed && now >= nextEventScan))
            {
                changed = false;
                guard.Tick();
                now = Environment.TickCount64;
                nextScan = now + 100;
                nextEventScan = now + 10; // coalesce bursts without a busy scan loop
            }
            long next = changed ? Math.Min(nextScan, nextEventScan) : nextScan;
            uint delay = (uint)Math.Clamp(next - now, 1, 100);
            if (MsgWaitForMultipleObjectsEx(0, 0, delay, 0x04FF, 4) == uint.MaxValue)
                Thread.Sleep((int)delay);
        }
    }
    public void Dispose()
    {
        foreach (nint hook in hooks) UnhookWinEvent(hook);
        hooks.Clear();
        if (callbackRoot.IsAllocated) callbackRoot.Free();
        GC.KeepAlive(callback);
    }
    private delegate void WinEvent(nint hook, uint eventId, nint window, int objectId, int childId, uint thread, uint time);
    [StructLayout(LayoutKind.Sequential)] private struct Message
    { internal nint Window; internal uint Id; internal nuint WParam; internal nint LParam; internal uint Time; internal int X, Y; internal uint Private; }
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEvent callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] private static extern bool PeekMessageW(out Message message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint DispatchMessageW(ref Message message);
    [DllImport("user32.dll")] private static extern uint MsgWaitForMultipleObjectsEx(uint count, nint handles, uint timeout, uint mask, uint flags);
}
