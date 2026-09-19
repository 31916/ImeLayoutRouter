using System.Runtime.InteropServices;
using System.Text;

sealed class ForegroundApplication
{
    private IntPtr lastWindow;
    private uint lastProcess;
    private long expires;
    private string? path;
    public string? Read(IntPtr window)
    {
        GetWindowThreadProcessId(window, out uint process);
        if (window == lastWindow && process == lastProcess && Environment.TickCount64 < expires) return path;
        lastWindow = window;
        lastProcess = process;
        expires = Environment.TickCount64 + 500;
        path = null;
        IntPtr handle = OpenProcess(0x1000, false, process); // QUERY_LIMITED_INFORMATION only
        if (handle == IntPtr.Zero) return null;
        try
        {
            var buffer = new StringBuilder(32768);
            uint size = (uint)buffer.Capacity;
            if (QueryFullProcessImageName(handle, 0, buffer, ref size)) path = buffer.ToString();
        }
        finally { CloseHandle(handle); }
        return path;
    }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint process);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref uint size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
