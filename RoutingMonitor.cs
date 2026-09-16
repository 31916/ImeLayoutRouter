using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

static class RoutingMonitor
{
    public static void Run(RoutingConfiguration configuration) => Run(configuration, CancellationToken.None);

    public static void Run(RoutingConfiguration configuration, CancellationToken cancellationToken)
    {
        var policy = new RoutingPolicy(configuration);
        InputSnapshot? previous = null;
        (InputContext Context, RoutingAction Action)? pending = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            InputSnapshot? observed = ReadSnapshot(configuration);
            if (observed is InputSnapshot current)
            {
                if (current != previous) Console.WriteLine(Describe(current));
                if (pending is { } request)
                {
                    if (request.Context != current.Context)
                        pending = null;
                    else if ((request.Action == RoutingAction.SwitchToTarget
                            && current.KeyboardLayout == configuration.Target.Hkl)
                        || (request.Action == RoutingAction.RestoreNative
                            && current.Mode == ImeInputMode.Native))
                    {
                        Console.WriteLine($"[Confirmed] {request.Action}");
                        pending = null;
                    }
                }
                RoutingAction action = policy.Evaluate(current, Environment.TickCount64);
                if (action != RoutingAction.None && !cancellationToken.IsCancellationRequested
                    && IsStillFocused(current))
                {
                    bool sent = action == RoutingAction.SwitchToTarget
                        ? SwitchToTarget(configuration.Target, current)
                        : RestoreNative(current.Context.Focus);
                    Console.WriteLine($"[Request] {action}: {(sent ? "sent; awaiting observation" : "failed; will retry")}");
                    if (sent) pending = (current.Context, action);
                }
                previous = current;
            }
            else
            {
                policy = new RoutingPolicy(configuration);
                previous = null;
                pending = null;
            }
            cancellationToken.WaitHandle.WaitOne(100);
        }
    }

    // Read-only diagnostics; never collect text, titles, URLs or keystrokes.
    public static void Diagnose(RoutingConfiguration configuration, int seconds, TextWriter output)
    {
        long until = Environment.TickCount64 + seconds * 1000L;
        InputSnapshot? previous = null;
        output.WriteLine($"Source: {configuration.Source.DisplayName}; Target: {configuration.Target.DisplayName}");
        while (Environment.TickCount64 < until)
        {
            var current = ReadSnapshot(configuration);
            if (current != previous)
            {
                output.WriteLine(current is InputSnapshot snapshot ? Describe(snapshot) : "[State] unavailable");
                output.Flush();
            }
            previous = current;
            Thread.Sleep(100);
        }
    }

    private static string Describe(InputSnapshot s) =>
        $"{DateTimeOffset.Now:O} hwnd=0x{s.Context.Foreground:X} focus=0x{s.Context.Focus:X} "
        + $"thread={s.Context.ThreadId} hkl=0x{s.KeyboardLayout:X} mode={s.Mode}";

    internal static InputSnapshot? ReadSnapshot(RoutingConfiguration configuration)
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return null;
        uint foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
        if (foregroundThread == 0) return null;
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (!GetGUIThreadInfo(foregroundThread, ref info) || info.hwndFocus == IntPtr.Zero)
            return null;
        // Read the control's thread, which can differ from the top-level window's.
        uint focusedThread = GetWindowThreadProcessId(info.hwndFocus, IntPtr.Zero);
        if (focusedThread == 0) return null;
        IntPtr layout = GetKeyboardLayout(focusedThread);
        if (layout == IntPtr.Zero) return null;
        var mode = ImeInputMode.Unknown;
        if ((layout.ToInt64() & 0xFFFF) == configuration.Source.LanguageId)
        {
            IntPtr ime = ImmGetDefaultIMEWnd(info.hwndFocus);
            bool? open = ReadIme(ime, IMC_GETOPENSTATUS) is int value ? value != 0 : null;
            int? conversion = open == true ? ReadIme(ime, IMC_GETCONVERSIONMODE) : null;
            mode = RoutingPolicy.Classify(open, conversion);
        }
        var snapshot = new InputSnapshot(new InputContext(foreground, info.hwndFocus, focusedThread), layout, mode);
        return IsStillFocused(snapshot) ? snapshot : null;
    }

    private static bool IsStillFocused(InputSnapshot snapshot)
    {
        if (GetForegroundWindow() != snapshot.Context.Foreground) return false;
        uint thread = GetWindowThreadProcessId(snapshot.Context.Foreground, IntPtr.Zero);
        if (thread == 0) return false;
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        return GetGUIThreadInfo(thread, ref info) && info.hwndFocus == snapshot.Context.Focus
            && GetKeyboardLayout(snapshot.Context.ThreadId) == snapshot.KeyboardLayout;
    }

    private static int? ReadIme(IntPtr ime, int command) =>
        SendIme(ime, command, IntPtr.Zero, out var result) ? unchecked((int)result.ToUInt64()) : null;

    private static bool SendIme(IntPtr ime, int command, IntPtr value, out UIntPtr result)
    {
        result = UIntPtr.Zero;
        return ime != IntPtr.Zero && SendMessageTimeout(ime, WM_IME_CONTROL, (UIntPtr)command,
            value, SMTO_ABORTIFHUNG | SMTO_ERRORONEXIT, 50, out result) != IntPtr.Zero;
    }

    private static bool RestoreNative(IntPtr focus)
    {
        IntPtr ime = ImmGetDefaultIMEWnd(focus);
        if (!SendIme(ime, IMC_SETOPENSTATUS, (IntPtr)1, out var result) || result != UIntPtr.Zero)
            return false;
        int? conversion = ReadIme(ime, IMC_GETCONVERSIONMODE);
        if (!conversion.HasValue) return false;
        // Hiragana, preserving the user's roman/kana typing preference.
        int native = (conversion.Value | 0x0001 | 0x0008) & ~0x0002 & ~0x0100;
        return SendIme(ime, IMC_SETCONVERSIONMODE, (IntPtr)native, out result) && result == UIntPtr.Zero;
    }

    private static bool SwitchToTarget(InputProfile target, InputSnapshot snapshot)
    {
        if (target.Type != InputProfileType.KeyboardLayout || target.Hkl == IntPtr.Zero) return false;
        // Use the focused control's queue when its thread differs from the root.
        IntPtr receiver = GetWindowThreadProcessId(snapshot.Context.Foreground, IntPtr.Zero) == snapshot.Context.ThreadId
            ? snapshot.Context.Foreground : snapshot.Context.Focus;
        return PostMessage(receiver, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, target.Hkl);
    }

    private const uint WM_IME_CONTROL = 0x0283, WM_INPUTLANGCHANGEREQUEST = 0x0050;
    private const int IMC_GETCONVERSIONMODE = 1, IMC_SETCONVERSIONMODE = 2,
        IMC_GETOPENSTATUS = 5, IMC_SETOPENSTATUS = 6;
    private const uint SMTO_ABORTIFHUNG = 0x0002, SMTO_ERRORONEXIT = 0x0020;

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);
    [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);
    [DllImport("imm32.dll")] private static extern IntPtr ImmGetDefaultIMEWnd(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam,
        IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
        public RECT rcCaret;
    }
}
