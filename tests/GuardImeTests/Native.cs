using System.Runtime.InteropServices;
using System.Text;

internal static class Native
{
    internal delegate nint WindowProc(nint window, uint message, nint wp, nint lp);
    internal delegate bool EnumProc(nint window, nint data);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct WindowClass
    { public uint Style; public WindowProc Proc; public int ClassExtra, WindowExtra; public nint Instance, Icon, Cursor, Background; public string? Menu, Name; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct CompositionForm { public uint Style; public Point Current; public Rect Area; }
    [StructLayout(LayoutKind.Sequential)] internal struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion { [FieldOffset(0)] public Keyboard Keyboard; [FieldOffset(0)] public Mouse Mouse; }
    [StructLayout(LayoutKind.Sequential)] internal struct Keyboard { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct Mouse { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern ushort RegisterClassW(ref WindowClass cls);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint data);
    [DllImport("user32.dll")] internal static extern bool DestroyWindow(nint h);
    [DllImport("user32.dll")] internal static extern nint DefWindowProcW(nint h, uint msg, nint wp, nint lp);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandleW(string? name);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint h);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern nint SetFocus(nint h);
    [DllImport("user32.dll")] internal static extern nint GetFocus();
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint h, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool CreateCaret(nint h, nint bitmap, int width, int height);
    [DllImport("user32.dll")] internal static extern bool SetCaretPos(int x, int y);
    [DllImport("user32.dll")] internal static extern bool ShowCaret(nint h);
    [DllImport("user32.dll")] internal static extern bool DestroyCaret();
    [DllImport("user32.dll")] internal static extern uint SendInput(uint count, Input[] input, int size);
    [DllImport("user32.dll")] internal static extern bool ScreenToClient(nint h, ref Point point);
    [DllImport("user32.dll")] internal static extern nint GetKeyboardLayout(uint thread);
    [DllImport("user32.dll")] internal static extern bool EnumThreadWindows(uint thread, EnumProc proc, nint data);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc proc, nint data);
    [DllImport("user32.dll")] internal static extern bool EnumChildWindows(nint parent, EnumProc proc, nint data);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint h);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassNameW(nint h, StringBuilder name, int count);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint h, out Rect rect);
    [DllImport("user32.dll")] internal static extern nint GetWindow(nint h, uint command);
    [DllImport("user32.dll")] internal static extern int GetWindowLongW(nint h, int index);
    [DllImport("user32.dll")] internal static extern uint GetClassLongW(nint h, int index);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint h, out uint pid);
    [DllImport("gdi32.dll")] internal static extern nint CreateRectRgn(int l, int t, int r, int b);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint obj);
    [DllImport("user32.dll")] internal static extern int GetWindowRgn(nint h, nint region);
    [DllImport("imm32.dll")] internal static extern nint ImmGetContext(nint h);
    [DllImport("imm32.dll")] internal static extern bool ImmReleaseContext(nint h, nint context);
    [DllImport("imm32.dll")] internal static extern bool ImmSetOpenStatus(nint context, bool open);
    [DllImport("imm32.dll")] internal static extern bool ImmGetOpenStatus(nint context);
    [DllImport("imm32.dll")] internal static extern bool ImmIsIME(nint hkl);
    [DllImport("imm32.dll")] internal static extern bool ImmSetConversionStatus(nint context, uint conversion, uint sentence);
    [DllImport("imm32.dll")] internal static extern bool ImmSetCompositionWindow(nint context, ref CompositionForm form);
    [DllImport("imm32.dll")] internal static extern int ImmGetCompositionStringW(nint context, uint index, [Out] byte[]? data, uint size);
    [DllImport("imm32.dll")] internal static extern uint ImmGetCandidateListW(nint context, uint index, [Out] byte[]? data, uint size);
}
