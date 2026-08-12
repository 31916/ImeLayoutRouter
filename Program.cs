using System;
using System.Runtime.InteropServices;
using System.Threading;

class Program
{
    // ============================================================
    // Windows API
    // ============================================================

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        IntPtr processId
    );

    [DllImport("user32.dll")]
    static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    static extern int GetKeyboardLayoutList(
        int nBuff,
        [Out] IntPtr[]? lpList
    );

    [DllImport("user32.dll")]
    static extern bool GetGUIThreadInfo(
        uint idThread,
        ref GUITHREADINFO guiThreadInfo
    );

    [DllImport("imm32.dll")]
    static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam
    );

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool PostMessage(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam
    );

    [DllImport("user32.dll")]
    static extern IntPtr GetAncestor(
        IntPtr hwnd,
        uint gaFlags
    );

    // ============================================================
    // Windows定数
    // ============================================================

    const uint WM_IME_CONTROL = 0x0283;

    const int IMC_GETOPENSTATUS = 0x0005;

    // 「IMEを開く / 閉じる」を設定する命令
    const int IMC_SETOPENSTATUS = 0x0006;

    // 入力言語変更要求
    const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

    // 一番上の親ウィンドウを取得
    const uint GA_ROOT = 2;

    // ============================================================
    // Windows構造体
    // ============================================================

    [StructLayout(LayoutKind.Sequential)]
    struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;

        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;

        public RECT rcCaret;
    }

    // ============================================================
    // Main
    // ============================================================

        [STAThread]
    static void Main(string[] args)
    {
        if (
            args.Length > 0
            && args[0] == "--list-profiles"
        )
        {
            TsfProfileEnumerator.PrintAllProfiles();
            return;
        }

        if (
            args.Length > 0
            && args[0] == "--active-profile"
        )
        {
            TsfProfileEnumerator.PrintActiveProfile();
            return;
        }
        if (
            args.Length > 0
            && args[0] == "--list-candidates"
        )
        {
            TsfProfileEnumerator.PrintSelectableProfiles();
            return;
        }
        if (
            args.Length > 0
            && args[0] == "--selection-test"
        )
        {
            TsfProfileEnumerator.PrintAutomaticSelection();
            return;
        }
        if (
            args.Length > 0
            && args[0] == "--routing-match-test"
        )
        {
            TsfProfileEnumerator.PrintRoutingMatchTest();
            return;
        }

                RoutingConfiguration? configuration =
            TsfProfileEnumerator.CreateAutomaticSelection();

        if (configuration == null)
        {
            Console.WriteLine(
                "Routing configuration could not be created."
            );

            Console.WriteLine(
                "Source and Target must be selected."
            );

            return;
        }

        Console.WriteLine(
            "IME Layout Router - Automatic Switch Test"
        );

        Console.WriteLine(
            $"Source: {configuration.Source.DisplayName}"
        );

        Console.WriteLine(
            $"Target: {configuration.Target.DisplayName}"
        );

        Console.WriteLine();

        Console.WriteLine(
            "Ctrl + C で終了します。"
        );

        Console.WriteLine();

        int previousLanguageId =
            -1;

        IntPtr previousKeyboardLayout =
            IntPtr.Zero;

        bool? previousImeOpen =
            null;

        while (true)
        {
            IntPtr foregroundWindow =
                GetForegroundWindow();

            if (foregroundWindow == IntPtr.Zero)
            {
                Thread.Sleep(100);
                continue;
            }

            uint threadId =
                GetWindowThreadProcessId(
                    foregroundWindow,
                    IntPtr.Zero
                );

            IntPtr focusedWindow =
                GetFocusedWindow(
                    threadId,
                    foregroundWindow
                );

            // 実際にフォーカスされている
            // アプリのKeyboard Layoutを取得する
            IntPtr keyboardLayout =
                GetKeyboardLayout(
                    threadId
                );

            int languageId =
                (int)(
                    keyboardLayout.ToInt64()
                    & 0xFFFF
                );

            bool sourceIsActive =
                languageId
                == configuration.Source.LanguageId;

            bool targetIsActive =
                configuration.Target.Type
                    == InputProfileType.KeyboardLayout
                &&
                keyboardLayout
                    == configuration.Target.Hkl;

            bool? imeOpen =
                null;

            if (sourceIsActive)
            {
                imeOpen =
                    GetImeOpenStatus(
                        focusedWindow
                    );
            }

            // ====================================================
            // Source:
            // IME input mode → Direct input
            // ====================================================

            bool switchedToDirectInput =
                previousLanguageId
                    == configuration.Source.LanguageId
                &&
                previousImeOpen == true
                &&
                sourceIsActive
                &&
                imeOpen == false;

            if (switchedToDirectInput)
            {
                Console.WriteLine(
                    $"[Detected] {configuration.Source.DisplayName}"
                    + " → Direct input"
                );

                bool success =
                    SwitchToTargetLayout(
                        configuration.Target,
                        focusedWindow,
                        foregroundWindow
                    );

                if (success)
                {
                    Console.WriteLine(
                        $"[Switch] {configuration.Target.DisplayName}"
                        + " requested"
                    );
                }
                else
                {
                    Console.WriteLine(
                        "[Error] Target keyboard layout "
                        + "could not be activated."
                    );
                }

                Console.WriteLine();
            }

            // ====================================================
            // Target → Source Direct input
            // ====================================================

            bool switchedFromTargetToSourceDirect =
                previousKeyboardLayout
                    == configuration.Target.Hkl
                &&
                sourceIsActive
                &&
                imeOpen == false;

            if (switchedFromTargetToSourceDirect)
            {
                Console.WriteLine(
                    $"[Detected] {configuration.Target.DisplayName}"
                    + $" → {configuration.Source.DisplayName}"
                    + " direct input"
                );

                bool success =
                    SetImeOpenStatus(
                        focusedWindow,
                        true
                    );

                if (success)
                {
                    Console.WriteLine(
                        "[Switch] IME input mode requested"
                    );

                    imeOpen =
                        GetImeOpenStatus(
                            focusedWindow
                        );
                }
                else
                {
                    Console.WriteLine(
                        "[Error] Could not open source IME."
                    );
                }

                Console.WriteLine();
            }

            // ====================================================
            // 状態表示
            // ====================================================

            if (
                keyboardLayout
                    != previousKeyboardLayout
                ||
                imeOpen
                    != previousImeOpen
            )
            {
                PrintState(
                    languageId,
                    keyboardLayout,
                    imeOpen,
                    configuration
                );
            }

            previousLanguageId =
                languageId;

            previousKeyboardLayout =
                keyboardLayout;

            previousImeOpen =
                imeOpen;

            Thread.Sleep(100);
        }
    }
   

    // ============================================================
    // フォーカス中のウィンドウを取得
    // ============================================================ 

    static IntPtr GetFocusedWindow(
        uint threadId,
        IntPtr fallbackWindow
    )
    {
        GUITHREADINFO info =
            new GUITHREADINFO();

        info.cbSize =
            Marshal.SizeOf<GUITHREADINFO>();

        bool success =
            GetGUIThreadInfo(
                threadId,
                ref info
            );

        if (
            success
            &&
            info.hwndFocus != IntPtr.Zero
        )
        {
            return info.hwndFocus;
        }

        return fallbackWindow;
    }

    // ============================================================
    // IME Open / Close取得
    // ============================================================

    static bool? GetImeOpenStatus(
        IntPtr window
    )
    {
        IntPtr imeWindow =
            ImmGetDefaultIMEWnd(window);

        if (imeWindow == IntPtr.Zero)
        {
            return null;
        }

        IntPtr result =
            SendMessage(
                imeWindow,
                WM_IME_CONTROL,
                (IntPtr)IMC_GETOPENSTATUS,
                IntPtr.Zero
            );

        return result != IntPtr.Zero;
    }

    // ============================================================
    // IME Open / Closeを変更
    // ============================================================

    static bool SetImeOpenStatus(
        IntPtr window,
        bool open
    )
    {
        IntPtr imeWindow =
            ImmGetDefaultIMEWnd(window);

        if (imeWindow == IntPtr.Zero)
        {
            return false;
        }

        SendMessage(
            imeWindow,
            WM_IME_CONTROL,
            (IntPtr)IMC_SETOPENSTATUS,
            open
                ? (IntPtr)1
                : IntPtr.Zero
        );

        return true;
    }

    // ============================================================
    // 設定されたTarget Keyboard Layoutへ変更
    // ============================================================

    static bool SwitchToTargetLayout(
        InputProfile target,
        IntPtr focusedWindow,
        IntPtr foregroundWindow
    )
    {
        if (
            target.Type
            != InputProfileType.KeyboardLayout
            ||
            target.Hkl == IntPtr.Zero
        )
        {
            return false;
        }

        IntPtr rootWindow =
            GetAncestor(
                focusedWindow,
                GA_ROOT
            );

        if (rootWindow == IntPtr.Zero)
        {
            rootWindow =
                foregroundWindow;
        }

        return PostMessage(
            rootWindow,
            WM_INPUTLANGCHANGEREQUEST,
            IntPtr.Zero,
            target.Hkl
        );
    }

    // ============================================================
    // インストール済みKeyboard Layoutを検索
    // ============================================================

    static IntPtr FindKeyboardLayout(
        int languageId
    )
    {
        int count =
            GetKeyboardLayoutList(
                0,
                null
            );

        if (count <= 0)
        {
            return IntPtr.Zero;
        }

        IntPtr[] layouts =
            new IntPtr[count];

        GetKeyboardLayoutList(
            layouts.Length,
            layouts
        );

        foreach (
            IntPtr layout in layouts
        )
        {
            int currentLanguageId =
                (int)(
                    layout.ToInt64()
                    & 0xFFFF
                );

            if (
                currentLanguageId
                == languageId
            )
            {
                return layout;
            }
        }

        return IntPtr.Zero;
    }

    // ============================================================
    // 状態表示
    // ============================================================

        static void PrintState(
        int languageId,
        IntPtr keyboardLayout,
        bool? imeOpen,
        RoutingConfiguration configuration
    )
    {
        Console.WriteLine(
            $"Language ID: 0x{languageId:X4}"
        );

        Console.WriteLine(
            $"Keyboard Layout: 0x{keyboardLayout.ToInt64():X}"
        );

        if (
            languageId
            == configuration.Source.LanguageId
        )
        {
            Console.WriteLine(
                $"Input: {configuration.Source.DisplayName}"
            );

            if (imeOpen == true)
            {
                Console.WriteLine(
                    "IME Open: True → IME input mode"
                );
            }
            else if (imeOpen == false)
            {
                Console.WriteLine(
                    "IME Open: False → Direct input mode"
                );
            }
            else
            {
                Console.WriteLine(
                    "IME Open: Unknown"
                );
            }
        }
        else if (
            keyboardLayout
            == configuration.Target.Hkl
        )
        {
            Console.WriteLine(
                $"Input: {configuration.Target.DisplayName}"
            );

            Console.WriteLine(
                "IME Open: N/A"
            );
        }
        else
        {
            Console.WriteLine(
                "Input: Other"
            );

            Console.WriteLine(
                "IME Open: N/A"
            );
        }

        Console.WriteLine();
    }
}
   