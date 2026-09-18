using System;
using System.Runtime.InteropServices;
using System.Threading;
using Accessibility;

enum FocusedFieldKind { Unknown, Text, Direct }

// Only structural accessibility metadata is read. Never request accValue,
// accName, accDescription, document text, or key events.
sealed class FocusedInputProbe : IDisposable
{
    private readonly Thread worker;
    private volatile bool stopping;
    private Observation? latest;
    private long focusVersion;
    private readonly WinEventCallback focusChanged;
    private sealed record Observation(IntPtr Foreground, IntPtr Focus, long At, FocusedFieldKind Kind, int ElementId);

    public FocusedInputProbe()
    {
        focusChanged = (_, _, _, _, _, _, _) =>
        {
            Interlocked.Increment(ref focusVersion);
            Volatile.Write(ref latest, null);
        };
        worker = new Thread(Poll) { IsBackground = true, Name = "IME field metadata" };
        worker.SetApartmentState(ApartmentState.MTA);
        worker.Start();
    }

    public (FocusedFieldKind Kind, int ElementId) Read(InputContext context)
    {
        var value = Volatile.Read(ref latest);
        if (value == null || value.Foreground != context.Foreground || value.Focus != context.Focus
            || Environment.TickCount64 - value.At > 350)
            return (FocusedFieldKind.Unknown, 0);
        return (value.Kind, value.ElementId);
    }

    private void Poll()
    {
        IntPtr hook = SetWinEventHook(0x8005, 0x8005, IntPtr.Zero, focusChanged, 0, 0, 0);
        try
        {
            while (!stopping)
            {
                PumpEvents();
                long version = Interlocked.Read(ref focusVersion);
                long started = Environment.TickCount64;
                IntPtr foreground = GetForegroundWindow();
                uint thread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
                var gui = new GUIINFO { Size = Marshal.SizeOf<GUIINFO>() };
                if (thread != 0 && GetGUIThreadInfo(thread, ref gui) && gui.Focus != IntPtr.Zero)
                {
                    var (kind, elementId) = Inspect(gui.Focus);
                    PumpEvents();
                    var after = new GUIINFO { Size = Marshal.SizeOf<GUIINFO>() };
                    if (version == Interlocked.Read(ref focusVersion) && foreground == GetForegroundWindow()
                        && GetGUIThreadInfo(thread, ref after) && after.Focus == gui.Focus && !stopping)
                        Volatile.Write(ref latest, new Observation(foreground, gui.Focus,
                            started, kind, elementId));
                }
                else Volatile.Write(ref latest, null);
                Thread.Sleep(100);
            }
        }
        finally { if (hook != IntPtr.Zero) UnhookWinEvent(hook); }
    }

    private static void PumpEvents()
    {
        while (PeekMessage(out var message, IntPtr.Zero, 0, 0, 1))
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
    }

    private static (FocusedFieldKind, int) Inspect(IntPtr focus)
    {
        IAccessible? accessible = null;
        try
        {
            Guid iid = typeof(IAccessible).GUID;
            if (AccessibleObjectFromWindow(focus, 0xFFFFFFFC, ref iid, out accessible) != 0 || accessible == null)
                return (FocusedFieldKind.Unknown, 0);
            object child = 0;
            // Browser DOM focus need not change the HWND. Follow accFocus to the
            // focused element, bounded to protect against broken providers.
            for (int depth = 0; depth < 20; depth++)
            {
                object? next = accessible.accFocus;
                if (next is IAccessible nested && !ReferenceEquals(nested, accessible))
                {
                    Release(accessible);
                    accessible = nested;
                    continue;
                }
                if (next is int childId) child = childId;
                break;
            }
            int state = Convert.ToInt32(accessible.get_accState(child));
            if ((state & 0x00000004) == 0) return (FocusedFieldKind.Unknown, 0); // FOCUSED
            if ((state & 0x00000001) != 0 || (state & 0x00000040) != 0) // unavailable/read-only
                return (FocusedFieldKind.Unknown, 0);
            if ((state & 0x20000000) != 0) return (FocusedFieldKind.Direct, 0); // PROTECTED
            if (!Equals(child, 0)) return (FocusedFieldKind.Unknown, 0);
            return ReadIa2(accessible);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException
            or InvalidOperationException or NotImplementedException)
        {
            return (FocusedFieldKind.Unknown, 0);
        }
        finally { Release(accessible); }
    }

    private static (FocusedFieldKind, int) ReadIa2(IAccessible accessible)
    {
        if (accessible is not IServiceProvider provider) return (FocusedFieldKind.Unknown, 0);
        Guid service = typeof(IAccessible).GUID;
        Guid iid = new("E89F726E-C4F4-4C19-BB19-B647D7FA8478");
        if (provider.QueryService(ref service, ref iid, out IntPtr ia2) != 0 || ia2 == IntPtr.Zero)
            return (FocusedFieldKind.Unknown, 0);
        try
        {
            // IAccessible2 extends IAccessible (28 slots including IUnknown and
            // IDispatch). uniqueID is slot 41; attributes is slot 45.
            // Linux Foundation IAccessible2.idl defines this stable COM ABI.
            IntPtr table = Marshal.ReadIntPtr(ia2);
            var getId = Marshal.GetDelegateForFunctionPointer<GetUniqueId>(Marshal.ReadIntPtr(table, 41 * IntPtr.Size));
            var getAttributes = Marshal.GetDelegateForFunctionPointer<GetAttributes>(Marshal.ReadIntPtr(table, 45 * IntPtr.Size));
            if (getId(ia2, out int id) != 0) id = 0;
            if (getAttributes(ia2, out string attributes) != 0) return (FocusedFieldKind.Unknown, id);
            return (ClassifyAttributes(attributes), id);
        }
        finally { Marshal.Release(ia2); }
    }

    internal static FocusedFieldKind ClassifyAttributes(string? attributes)
    {
        // Match complete attributes, never labels or user-entered values.
        foreach (string attribute in SplitAttributes(attributes ?? ""))
        {
            const string prefix = "text-input-type:";
            if (attribute.Contains('\\') || !attribute.StartsWith(prefix, StringComparison.Ordinal)) continue;
            return attribute[prefix.Length..] switch
            {
                "email" or "url" or "tel" or "number" or "password" => FocusedFieldKind.Direct,
                "text" or "search" => FocusedFieldKind.Text,
                _ => FocusedFieldKind.Unknown
            };
        }
        return FocusedFieldKind.Unknown;
    }

    private static System.Collections.Generic.IEnumerable<string> SplitAttributes(string text)
    {
        int start = 0;
        bool escaped = false;
        for (int i = 0; i < text.Length; i++)
        {
            if (escaped) { escaped = false; continue; }
            if (text[i] == '\\') { escaped = true; continue; }
            if (text[i] == ';') { yield return text[start..i]; start = i + 1; }
        }
        yield return text[start..];
    }

    private static void Release(object? value)
    {
        if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }

    public void Dispose() => stopping = true; // A stuck provider must not block tray shutdown.

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IServiceProvider
    {
        [PreserveSig] int QueryService(ref Guid service, ref Guid iid, out IntPtr result);
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetUniqueId(IntPtr self, out int id);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetAttributes(IntPtr self, [MarshalAs(UnmanagedType.BStr)] out string attributes);
    [DllImport("oleacc.dll")] private static extern int AccessibleObjectFromWindow(
        IntPtr window, uint objectId, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IAccessible? accessible);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint thread, ref GUIINFO info);
    [StructLayout(LayoutKind.Sequential)]
    private struct GUIINFO
    {
        public int Size;
        public uint Flags;
        public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public int Left, Top, Right, Bottom;
    }
    private delegate void WinEventCallback(IntPtr hook, uint eventId, IntPtr window,
        int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min, uint max,
        IntPtr module, WinEventCallback callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
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
