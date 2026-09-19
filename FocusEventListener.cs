using System.Runtime.InteropServices;

// Keep hook delivery off the accessibility worker: an unresponsive COM provider
// must not prevent stale field metadata from being invalidated.
sealed class FocusEventListener : IDisposable
{
    private readonly Thread thread;
    private readonly WinEventCallback callback;
    private volatile bool stopping;
    public FocusEventListener(Action changed)
    {
        callback = (_, _, _, _, _, _, _) => { if (!stopping) changed(); };
        thread = new Thread(Listen) { IsBackground = true, Name = "IME focus events" };
        thread.Start();
    }
    private void Listen()
    {
        IntPtr foreground = SetWinEventHook(3, 3, IntPtr.Zero, callback, 0, 0, 0);
        IntPtr focus = SetWinEventHook(0x8005, 0x8005, IntPtr.Zero, callback, 0, 0, 0);
        try
        {
            while (!stopping)
            {
                while (PeekMessage(out var message, IntPtr.Zero, 0, 0, 1))
                {
                    TranslateMessage(ref message);
                    DispatchMessage(ref message);
                }
                // Polling remains available when hooks cannot be installed.
                MsgWaitForMultipleObjects(0, IntPtr.Zero, false, 50, 0x04FF);
            }
        }
        finally
        {
            if (foreground != IntPtr.Zero) UnhookWinEvent(foreground);
            if (focus != IntPtr.Zero) UnhookWinEvent(focus);
            GC.KeepAlive(callback);
        }
    }
    public void Dispose() { stopping = true; thread.Join(250); }
    private delegate void WinEventCallback(IntPtr hook, uint eventId, IntPtr window, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventCallback callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern uint MsgWaitForMultipleObjects(uint count, IntPtr handles, bool waitAll, uint milliseconds, uint wakeMask);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out MSG message, IntPtr window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG message);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG message);
    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr Window;
        public uint Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X, Y;
        public uint Private;
    }
}
