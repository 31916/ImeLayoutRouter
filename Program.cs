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

    // 日本語
    const int JAPANESE_LANGUAGE_ID = 0x0411;

    // German (Switzerland)
    const int SWISS_GERMAN_LANGUAGE_ID = 0x0807;

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
        Console.WriteLine(
            "IME Layout Router - Automatic Switch Test"
        );

        Console.WriteLine(
            "Japanese IME: Japanese → Direct Input"
        );

        Console.WriteLine(
            "Target: German (Switzerland)"
        );

        Console.WriteLine();

        Console.WriteLine(
            "Ctrl + C で終了します。"
        );

        Console.WriteLine();

        int previousLanguageId = -1;

        bool? previousImeOpen = null;

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

            IntPtr keyboardLayout =
                GetKeyboardLayout(threadId);

            int languageId =
                (int)(
                    keyboardLayout.ToInt64()
                    & 0xFFFF
                );

            bool? imeOpen = null;

            if (
                languageId
                == JAPANESE_LANGUAGE_ID
            )
            {
                imeOpen =
                    GetImeOpenStatus(
                        focusedWindow
                    );
            }

            // ====================================================
            // 「日本語入力 → Direct Input」を検出
            // ====================================================

            bool switchedToDirectInput =
                previousLanguageId
                    == JAPANESE_LANGUAGE_ID
                &&
                previousImeOpen == true
                &&
                languageId
                    == JAPANESE_LANGUAGE_ID
                &&
                imeOpen == false;

            if (switchedToDirectInput)
            {
                Console.WriteLine(
                    "[Detected] Japanese input → Direct input"
                );

                bool success =
                    SwitchToSwissGerman(
                        focusedWindow,
                        foregroundWindow
                    );

                if (success)
                {
                    Console.WriteLine(
                        "[Switch] German (Switzerland) requested"
                    );
                }
                else
                {
                    Console.WriteLine(
                        "[Error] German (Switzerland) layout was not found."
                    );
                }

                Console.WriteLine();
            }

            // ====================================================
            // 「German (Switzerland) → 日本語IME Direct Input」を検出
            // ====================================================

            bool switchedFromSwissGermanToJapaneseDirect =
                previousLanguageId
                    == SWISS_GERMAN_LANGUAGE_ID
                &&
                languageId
                    == JAPANESE_LANGUAGE_ID
                &&
                imeOpen == false;

            if (switchedFromSwissGermanToJapaneseDirect)
            {
                Console.WriteLine(
                    "[Detected] German (Switzerland) → Japanese direct input"
                );

                bool success =
                    SetImeOpenStatus(
                        focusedWindow,
                        true
                    );

                if (success)
                {
                    Console.WriteLine(
                        "[Switch] Japanese input mode requested"
                    );

                    // 設定後の状態をもう一度取得
                    imeOpen =
                        GetImeOpenStatus(
                            focusedWindow
                        );
                }
                else
                {
                    Console.WriteLine(
                        "[Error] Could not open Japanese IME."
                    );
                }

                Console.WriteLine();
            }

            // ====================================================
            // 状態表示
            // ====================================================

            if (
                languageId != previousLanguageId
                ||
                imeOpen != previousImeOpen
            )
            {
                PrintState(
                    languageId,
                    keyboardLayout,
                    imeOpen
                );
            }

            previousLanguageId =
                languageId;

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
    // Swiss Germanへ変更
    // ============================================================

    static bool SwitchToSwissGerman(
        IntPtr focusedWindow,
        IntPtr foregroundWindow
    )
    {
        IntPtr targetLayout =
            FindKeyboardLayout(
                SWISS_GERMAN_LANGUAGE_ID
            );

        if (targetLayout == IntPtr.Zero)
        {
            return false;
        }

        // 入力欄そのものではなく、
        // アプリの一番上のウィンドウへ送る
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
            targetLayout
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
        bool? imeOpen
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
            == JAPANESE_LANGUAGE_ID
        )
        {
            Console.WriteLine(
                "Input: Japanese"
            );

            if (imeOpen == true)
            {
                Console.WriteLine(
                    "IME Open: True → Japanese input mode"
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
            languageId
            == SWISS_GERMAN_LANGUAGE_ID
        )
        {
            Console.WriteLine(
                "Input: German (Switzerland)"
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