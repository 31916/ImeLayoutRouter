using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed class GlobalHotkeys : NativeWindow, IDisposable
{
    private readonly List<int> registered = [];
    private readonly Action<int> pressed;
    private readonly Func<int, int, bool> register;
    private readonly Action<int> unregister;
    private readonly Func<string> lastError;
    public GlobalHotkeys(Action<int> pressed, Func<int, int, bool>? register = null,
        Action<int>? unregister = null, Func<string>? lastError = null)
    {
        this.pressed = pressed;
        this.register = register ?? ((id, key) => RegisterHotKey(Handle, id, 0x4003, (uint)key)); // NOREPEAT | CTRL | ALT
        this.unregister = unregister ?? (id => UnregisterHotKey(Handle, id));
        this.lastError = lastError ?? (() => new Win32Exception(Marshal.GetLastWin32Error()).Message);
        CreateHandle(new CreateParams { Caption = "IME Layout Router shortcuts", Parent = (IntPtr)(-3) });
    }
    public string? Apply(RoutingPreferences preferences)
    {
        Clear();
        if (!preferences.HotkeysEnabled) return null;
        int[] keys = [preferences.PauseKey, preferences.TargetKey, preferences.RestoreKey];
        for (int i = 0; i < keys.Length; i++)
        {
            if (!register(i + 1, keys[i]))
            {
                string error = lastError();
                Clear(); // Do not leave an ambiguous, partially working set.
                return $"Ctrl+Alt+F{keys[i] - 0x6F}: {error}";
            }
            registered.Add(i + 1);
        }
        return null;
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0312 && registered.Contains(message.WParam.ToInt32())) pressed(message.WParam.ToInt32());
        base.WndProc(ref message);
    }
    private void Clear() { foreach (int id in registered) unregister(id); registered.Clear(); }
    public void Dispose() { Clear(); DestroyHandle(); }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
}
