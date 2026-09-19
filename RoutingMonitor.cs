using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

static class RoutingMonitor
{
    public static void Run(RoutingConfiguration configuration) => Run(configuration, CancellationToken.None);

#if SIMPLE_EDITION
    public static void Run(RoutingConfiguration configuration, CancellationToken cancellationToken,
        IntPtr allowedForeground = default)
    {
        var policy = new RoutingPolicy(configuration);
        using var wake = new AutoResetEvent(false);
        using var fields = new FocusedInputProbe(() => wake.Set());
        var waits = new[] { cancellationToken.WaitHandle, wake };
        while (!cancellationToken.IsCancellationRequested)
        {
            if (ReadSnapshot(configuration, fields) is { } current
                && (allowedForeground == IntPtr.Zero || current.Context.Foreground == allowedForeground))
            {
                var action = policy.Evaluate(current, Environment.TickCount64);
                if (action != RoutingAction.None && !cancellationToken.IsCancellationRequested && IsCurrent(current, fields))
                {
                    if (action == RoutingAction.SwitchToTarget) SwitchLayout(configuration.Target.Hkl, current);
                    else RestoreNative(current, fields);
                }
            }
            else policy = new RoutingPolicy(configuration);
            WaitHandle.WaitAny(waits, 50);
        }
    }
#else
    public static void Run(RoutingConfiguration configuration, CancellationToken cancellationToken,
        RoutingSession? session = null, IntPtr allowedForeground = default)
    {
        using var ownedSession = session == null ? new RoutingSession() : null;
        session ??= ownedSession!;
        var policy = new RoutingPolicy(configuration);
        using var fields = new FocusedInputProbe(() => session.Wake.Set());
        var applications = new ForegroundApplication();
        var manual = new ManualRoutingState();
        InputSnapshot? previous = null;
        (InputContext Context, IntPtr Layout, bool Native, long At)? pending = null;
        (InputContext Context, string Text, long Until)? notice = null;
        bool wasPaused = session.Paused;
        var waitHandles = new[] { cancellationToken.WaitHandle, session.Wake };
        while (!cancellationToken.IsCancellationRequested)
        {
            InputSnapshot? observed = ReadSnapshot(configuration, fields);
            Interlocked.Exchange(ref session.FocusVersion, fields.FocusVersion);
            if (observed is InputSnapshot current
                && (allowedForeground == IntPtr.Zero || current.Context.Foreground == allowedForeground))
            {
                manual.Observe(current);
                if (current != previous) Console.WriteLine(Describe(current));
                string reason = current.RequiresDirectInput ? UiText.T("英数専用の入力欄", "Direct-input field")
                    : current.Mode == ImeInputMode.Direct ? UiText.T("IMEが英数入力", "IME direct input")
                    : UiText.T("入力状態を監視中", "Watching input state");
                bool paused = session.Paused;
                if (paused != wasPaused)
                {
                    policy = new RoutingPolicy(configuration);
                    pending = null;
                    if (!paused) manual.Resume();
                    wasPaused = paused;
                }
                if (pending is { } request)
                {
                    if (request.Context != current.Context)
                        pending = null;
                    else if (current.KeyboardLayout == request.Layout && (!request.Native || current.Mode == ImeInputMode.Native))
                    {
                        reason = UiText.T("切替完了を確認", "Switch confirmed");
                        notice = (current.Context, reason, Environment.TickCount64 + 2000);
                        pending = null;
                    }
                    else reason = Environment.TickCount64 - request.At >= 1000
                        ? UiText.T("切替未確認（アプリが要求を無視した可能性）", "Switch unconfirmed; application may have ignored it")
                        : UiText.T("切替要求済み・完了待ち", "Switch requested; awaiting confirmation");
                }

                bool handledManual = false;
                while (session.TryTake(out var command))
                {
                    // Never apply a queued shortcut to a different field/window.
                    if (!ManualRoutingState.Matches(command, current) || !IsCurrent(current, fields))
                    {
                        notice = (current.Context, UiText.T("入力先が変わったため手動切替を取り消しました", "Manual switch cancelled because focus changed"), Environment.TickCount64 + 2000);
                        continue;
                    }
                    handledManual = true;
                    IntPtr layout = command.Action == ManualRoutingAction.Target ? configuration.Target.Hkl
                        : manual.RestoreLayout(current, configuration.Target.Hkl);
                    if (layout == IntPtr.Zero)
                    {
                        reason = UiText.T("このウィンドウには戻せる配列がありません", "No previous layout for this window");
                        notice = (current.Context, reason, Environment.TickCount64 + 2000);
                        continue;
                    }
                    bool sent = !cancellationToken.IsCancellationRequested && SwitchLayout(layout, current);
                    if (sent)
                    {
                        if (command.Action == ManualRoutingAction.Target) manual.Remember(current, configuration.Target.Hkl);
                        manual.Hold();
                        policy = new RoutingPolicy(configuration);
                        pending = (current.Context, layout, false, Environment.TickCount64);
                        reason = UiText.T("手動切替を要求・完了待ち", "Manual switch requested; awaiting confirmation");
                    }
                    else
                    {
                        reason = UiText.T("手動切替に失敗しました", "Manual switch request failed");
                        notice = (current.Context, reason, Environment.TickCount64 + 2000);
                    }
                    break;
                }

                bool excluded = configuration.Preferences.Resolve(applications.Read(current.Context.Focus)) == ApplicationRoutingMode.Disabled;
                if (paused || excluded || manual.Holding || handledManual)
                {
                    policy = new RoutingPolicy(configuration);
                    if (!handledManual && pending == null) reason = paused ? UiText.T("自動切替を一時停止中", "Automatic routing paused")
                        : manual.Holding ? UiText.T("手動選択を優先中（別のウィンドウへ移るまで）", "Manual choice held until leaving this window")
                        : UiText.T("アプリ別ルールにより自動切替を停止", "Automatic routing disabled by application rule");
                }
                else
                {
                    RoutingAction action = policy.Evaluate(current, Environment.TickCount64);
                    if (action != RoutingAction.None && !session.Paused && !cancellationToken.IsCancellationRequested && IsCurrent(current, fields))
                    {
                        bool sent = action == RoutingAction.SwitchToTarget
                            ? SwitchLayout(configuration.Target.Hkl, current) : RestoreNative(current, fields);
                        if (sent)
                        {
                            if (action == RoutingAction.SwitchToTarget) manual.Remember(current, configuration.Target.Hkl);
                            IntPtr target = action == RoutingAction.SwitchToTarget ? configuration.Target.Hkl : current.KeyboardLayout;
                            long started = pending?.At ?? Environment.TickCount64;
                            pending = (current.Context, target, action == RoutingAction.RestoreNative, started);
                            reason = Environment.TickCount64 - started >= 1000
                                ? UiText.T("切替未確認・再試行中", "Switch unconfirmed; retrying")
                                : UiText.T("切替要求済み・完了待ち", "Switch requested; awaiting confirmation");
                        }
                        else reason = UiText.T("切替要求に失敗・再試行予定", "Switch request failed; will retry");
                    }
                }
                // Tray updates are slower than observations: keep completion and
                // command errors visible instead of losing them on the next tick.
                if (pending == null && notice is { } feedback && feedback.Context == current.Context
                    && Environment.TickCount64 < feedback.Until) reason = feedback.Text;
                session.Publish(new RoutingStatus(current, reason, paused));
                previous = current;
            }
            else
            {
                policy = new RoutingPolicy(configuration);
                previous = null;
                pending = null;
                notice = null;
                manual.RetainOnlyWindow(GetForegroundWindow());
                while (session.TryTake(out _)) { }
                session.Publish(new RoutingStatus(null, UiText.T("対象の入力欄を待っています", "Waiting for an input field"), session.Paused));
            }
            // Foreground/focus and new metadata wake immediately; the fallback
            // also detects IME-mode changes which do not emit focus events.
            WaitHandle.WaitAny(waitHandles, 50);
        }
    }

#endif
    // Read-only diagnostics; never collect text, titles, URLs or keystrokes.
#if !SIMPLE_EDITION
    public static void Diagnose(RoutingConfiguration configuration, int seconds, TextWriter output)
    {
        long until = Environment.TickCount64 + seconds * 1000L;
        using var fields = new FocusedInputProbe();
        InputSnapshot? previous = null;
        output.WriteLine($"Source: {configuration.Source.DisplayName}; Target: {configuration.Target.DisplayName}");
        while (Environment.TickCount64 < until)
        {
            var current = ReadSnapshot(configuration, fields);
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
        + $"thread={s.Context.ThreadId} element={s.Context.ElementId} hkl=0x{s.KeyboardLayout:X} mode={s.Mode} directField={s.RequiresDirectInput}";
#endif

    internal static InputSnapshot? ReadSnapshot(RoutingConfiguration configuration, FocusedInputProbe? fields = null)
    {
        long focusVersion = fields?.FocusVersion ?? 0;
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
        if (configuration.IsSourceLayout(layout))
        {
            IntPtr ime = ImmGetDefaultIMEWnd(info.hwndFocus);
            bool? open = ReadIme(ime, IMC_GETOPENSTATUS) is int value ? value != 0 : null;
            int? conversion = open == true ? ReadIme(ime, IMC_GETCONVERSIONMODE) : null;
            mode = RoutingPolicy.Classify(open, conversion);
        }
        var context = new InputContext(foreground, info.hwndFocus, focusedThread,
            FocusVersion: focusVersion);
        var hint = fields?.Read(context) ?? (FocusedFieldKind.Unknown, 0);
        context = context with { ElementId = hint.Item2 };
        var snapshot = new InputSnapshot(context, layout, mode, hint.Item1 == FocusedFieldKind.Direct);
        return IsStillFocused(snapshot) && (fields == null || fields.FocusVersion == context.FocusVersion) ? snapshot : null;
    }

    // Shortcuts capture the actual foreground, not the last tray observation.
    public static InputSnapshot? CaptureManualFocus(long focusVersion)
    {
        IntPtr foreground = GetForegroundWindow();
        uint thread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (thread == 0 || !GetGUIThreadInfo(thread, ref info) || info.hwndFocus == IntPtr.Zero) return null;
        thread = GetWindowThreadProcessId(info.hwndFocus, IntPtr.Zero);
        IntPtr layout = GetKeyboardLayout(thread);
        var snapshot = new InputSnapshot(new InputContext(foreground, info.hwndFocus, thread,
            FocusVersion: focusVersion), layout, ImeInputMode.Unknown);
        return thread != 0 && layout != IntPtr.Zero && IsStillFocused(snapshot) ? snapshot : null;
    }

    private static bool IsCurrent(InputSnapshot current, FocusedInputProbe fields) =>
        IsStillFocused(current) && fields.FocusVersion == current.Context.FocusVersion
        && (!current.RequiresDirectInput || fields.Read(current.Context) == (FocusedFieldKind.Direct, current.Context.ElementId));

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

    private static bool RestoreNative(InputSnapshot snapshot, FocusedInputProbe fields)
    {
        IntPtr ime = ImmGetDefaultIMEWnd(snapshot.Context.Focus);
        if (!SendIme(ime, IMC_SETOPENSTATUS, (IntPtr)1, out var result) || result != UIntPtr.Zero)
            return false;
        int? conversion = ReadIme(ime, IMC_GETCONVERSIONMODE);
        if (!conversion.HasValue || !IsCurrent(snapshot, fields)) return false;
        int native = ImeLanguage.NativeConversion((ushort)(snapshot.KeyboardLayout.ToInt64() & 0xFFFF), conversion.Value);
        return SendIme(ime, IMC_SETCONVERSIONMODE, (IntPtr)native, out result) && result == UIntPtr.Zero;
    }

    private static bool SwitchLayout(IntPtr layout, InputSnapshot snapshot)
    {
        if (layout == IntPtr.Zero) return false;
        // Use the focused control's queue when its thread differs from the root.
        IntPtr receiver = GetWindowThreadProcessId(snapshot.Context.Foreground, IntPtr.Zero) == snapshot.Context.ThreadId
            ? snapshot.Context.Foreground : snapshot.Context.Focus;
        return PostMessage(receiver, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layout);
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
