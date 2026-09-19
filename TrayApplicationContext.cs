using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using static UiText;

sealed class TrayApplicationContext :
    ApplicationContext
{
    private readonly NotifyIcon notifyIcon;

    private CancellationTokenSource?
        monitorCancellation;

    private Task? monitorTask;
    private Task stoppedMonitor = Task.CompletedTask;
    private readonly ToolStripMenuItem pauseItem;
    private readonly System.Windows.Forms.Timer statusTimer;
    private bool settingsVisible;
    private bool errorShown;
    private RoutingSession? session;
    private RoutingConfiguration? activeConfiguration;
    private readonly GlobalHotkeys hotkeys;
    private readonly ToolStripMenuItem layoutItem;
    private readonly ToolStripMenuItem reasonItem;
    private readonly ToolStripMenuItem shortcutItem;

    public TrayApplicationContext(
        bool showSettingsOnStartup = false,
        EventWaitHandle? settingsRequest = null
    )
    {
        ContextMenuStrip menu =
            new ContextMenuStrip();
        layoutItem = new ToolStripMenuItem(T("入力状態を取得中", "Reading input state")) { Enabled = false };
        reasonItem = new ToolStripMenuItem("") { Enabled = false };
        shortcutItem = new ToolStripMenuItem("") { Enabled = false };
        menu.Items.AddRange([layoutItem, reasonItem, shortcutItem, new ToolStripSeparator()]);

        menu.Items.Add(
            T("設定・入力確認", "Settings / typing check"),
            null,
            (_, _) => ShowSettings()
        );

        pauseItem = new ToolStripMenuItem(T("自動切替を一時停止", "Pause automatic routing")) { CheckOnClick = true };
        menu.Items.Add(pauseItem);
        menu.Items.Add(T("診断ログを保存（30秒）", "Save diagnostic log (30 seconds)"), null, async (_, _) => await SaveDiagnosticLog());

        menu.Items.Add(
            new ToolStripSeparator()
        );

        menu.Items.Add(
            T("終了", "Exit"),
            null,
            (_, _) => ExitThread()
        );

        notifyIcon =
            new NotifyIcon
            {
                Icon =  new Icon(
                            Path.Combine(
                                AppContext.BaseDirectory,
                                "Assets",
                                "app.ico"
                            )
                        ),

                Text =
                    "IME Layout Router",

                ContextMenuStrip =
                    menu,

                Visible =
                    true
            };

        notifyIcon.DoubleClick +=
            (_, _) => ShowSettings();
        pauseItem.CheckedChanged += (_, _) =>
        {
            session?.SetPaused(pauseItem.Checked);
        };
        hotkeys = new GlobalHotkeys(id =>
        {
            if (settingsVisible || session == null) return;
            if (id == 1) { pauseItem.Checked = !pauseItem.Checked; return; }
            if (RoutingMonitor.CaptureManualFocus(Interlocked.Read(ref session.FocusVersion)) is { } focus)
                session.Request(new ManualRoutingRequest(id == 2 ? ManualRoutingAction.Target : ManualRoutingAction.Restore, focus));
        });

        statusTimer = new System.Windows.Forms.Timer { Interval = 500, Enabled = true };
        statusTimer.Tick += (_, _) =>
        {
            if (settingsRequest?.WaitOne(0) == true) ShowSettings();
            if (session is { } running && monitorTask?.IsFaulted != true)
            {
                var state = running.Status;
                string layout = state.Snapshot is { } snapshot ? LayoutName(snapshot) : T("取得不可", "Unavailable");
                layoutItem.Text = T("現在の配列: ", "Current layout: ") + layout;
                reasonItem.Text = state.Reason;
                string tooltip = $"IME Layout Router\n{layout}\n{state.Reason}";
                notifyIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
            }
            if (monitorTask?.IsFaulted == true && !errorShown)
            {
                errorShown = true;
                notifyIcon.Text = "IME Layout Router - Stopped";
                reasonItem.Text = T("監視停止。設定を開くと再起動します。", "Monitor stopped. Open Settings to restart.");
                notifyIcon.ShowBalloonTip(8000, "IME Layout Router",
                    "Routing stopped. Open Settings to restart. " + monitorTask.Exception?.GetBaseException().Message,
                    ToolTipIcon.Error);
            }
        };

        RoutingConfiguration? configuration =
            SettingsService.Load();

        if (configuration != null)
        {
            StartMonitor(
                configuration
            );
        }

        if (
            showSettingsOnStartup
            ||
            configuration == null
        )
        {
            ShowSettings();
        }
    }

    private void ShowSettings()
    {
        if (settingsVisible) return;
        settingsVisible = true;
        StopMonitor(); // Only the explicitly started, window-scoped preview may run in Settings.
        hotkeys.Apply(new RoutingPreferences());
        try { ShowSettingsCore(); }
        finally
        {
            settingsVisible = false;
            if (SettingsService.Load() is { } saved) StartMonitor(saved);
        }
    }

    private void ShowSettingsCore()
    {
        RoutingConfiguration? current =
            SettingsService.Load();

        using SettingsForm form =
            new SettingsForm(
                current,
                StartupManager.IsEnabled()
            );

        if (
            form.ShowDialog()
                != DialogResult.OK
            ||
            form.SelectedConfiguration
                == null
        )
        {
            return;
        }

        try
        {
            StartupManager.SetEnabled(
                form.StartWithWindows
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "IME Layout Router",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            return;
        }

        try { SettingsService.Save(form.SelectedConfiguration); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, "IME Layout Router", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

    }

    private void StartMonitor(
        RoutingConfiguration configuration
    )
    {
        StopMonitor();
        Task previousTask = stoppedMonitor;
        errorShown = false;
        notifyIcon.Text = "IME Layout Router";

        monitorCancellation =
            new CancellationTokenSource();

        CancellationToken token =
            monitorCancellation.Token;
        activeConfiguration = configuration;
        session = new RoutingSession();
        session.SetPaused(pauseItem.Checked);
        var runningSession = session;
        string? shortcutError = hotkeys.Apply(configuration.Preferences);
        shortcutItem.Text = shortcutError != null ? T("ショートカット: 登録失敗", "Shortcuts: registration failed")
            : configuration.Preferences.HotkeysEnabled ? T("ショートカット: 有効", "Shortcuts: enabled") : T("ショートカット: 無効", "Shortcuts: disabled");
        if (shortcutError != null) notifyIcon.ShowBalloonTip(8000, "IME Layout Router",
            T("ショートカットを登録できませんでした。設定でキーを変更してください。\n", "Shortcuts could not be registered. Change the keys in Settings.\n") + shortcutError, ToolTipIcon.Warning);

        monitorTask =
            Task.Run(
                async () =>
                {
                    // Never overlap an old monitor whose shutdown is still finishing.
                    try { await previousTask.ConfigureAwait(false); } catch (Exception) { }
                    token.ThrowIfCancellationRequested();
                    RoutingMonitor.Run(
                        configuration,
                        token,
                        runningSession
                    );
                },
                token
            );
    }

    private void StopMonitor()
    {
        if (
            monitorCancellation
            == null
        )
        {
            return;
        }

        monitorCancellation.Cancel();

        try
        {
            monitorTask?.Wait(500);
        }
        catch (AggregateException)
        {
        }

        var cancelled = monitorCancellation;
        var stoppedSession = session;
        if (monitorTask == null || monitorTask.IsCompleted) { cancelled.Dispose(); stoppedSession?.Dispose(); }
        else _ = monitorTask.ContinueWith(_ => { cancelled.Dispose(); stoppedSession?.Dispose(); }, TaskScheduler.Default);
        if (monitorTask != null) stoppedMonitor = stoppedMonitor.IsCompleted ? monitorTask : Task.WhenAll(stoppedMonitor, monitorTask);

        monitorCancellation = null;
        monitorTask = null;
        session = null;
    }

    protected override void ExitThreadCore()
    {
        statusTimer.Stop();
        statusTimer.Dispose();
        notifyIcon.Visible =
            false;

        StopMonitor();
        hotkeys.Dispose();

        notifyIcon.Dispose();

        base.ExitThreadCore();
    }

    private string LayoutName(InputSnapshot snapshot)
    {
        string name = snapshot.KeyboardLayout == activeConfiguration?.Target.Hkl
            ? activeConfiguration.Target.DisplayName : $"HKL {snapshot.KeyboardLayout.ToInt64():X8}";
        if (activeConfiguration?.IsSourceLayout(snapshot.KeyboardLayout) == true)
        {
            try { name = System.Globalization.CultureInfo.GetCultureInfo((int)(snapshot.KeyboardLayout.ToInt64() & 0xFFFF)).NativeName + " IME"; }
            catch (System.Globalization.CultureNotFoundException) { }
        }
        return name + (snapshot.Mode switch
        {
            ImeInputMode.Native => T(" / 母語入力", " / native input"),
            ImeInputMode.Direct => T(" / 英数", " / direct input"),
            ImeInputMode.Other => T(" / 変換モード", " / conversion mode"),
            _ => ""
        });
    }

    private async Task SaveDiagnosticLog()
    {
        var configuration = SettingsService.Load();
        if (configuration == null) { ShowSettings(); return; }
        using var dialog = new SaveFileDialog
        {
            Filter = "Log files (*.log)|*.log", FileName = "ImeLayoutRouter-diagnostic.log",
            Title = "Save input-state diagnostic log (no typed text)"
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        try
        {
            await Task.Run(() =>
            {
                using var output = new StreamWriter(dialog.FileName);
                RoutingMonitor.Diagnose(configuration, 30, output);
            });
            if (notifyIcon.Visible) notifyIcon.ShowBalloonTip(5000, "IME Layout Router",
                "Diagnostic log saved.", ToolTipIcon.Info);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, "IME Layout Router", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
