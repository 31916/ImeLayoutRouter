using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

sealed class TrayApplicationContext :
    ApplicationContext
{
    private readonly NotifyIcon notifyIcon;

    private CancellationTokenSource?
        monitorCancellation;

    private Task? monitorTask;
    private readonly ToolStripMenuItem pauseItem;
    private readonly System.Windows.Forms.Timer statusTimer;
    private bool settingsVisible;
    private bool errorShown;

    public TrayApplicationContext(
        bool showSettingsOnStartup = false,
        EventWaitHandle? settingsRequest = null
    )
    {
        ContextMenuStrip menu =
            new ContextMenuStrip();

        menu.Items.Add(
            "Settings",
            null,
            (_, _) => ShowSettings()
        );

        pauseItem = new ToolStripMenuItem("Pause routing") { CheckOnClick = true };
        menu.Items.Add(pauseItem);
        menu.Items.Add("Save diagnostic log (30 seconds)", null, async (_, _) => await SaveDiagnosticLog());

        menu.Items.Add(
            new ToolStripSeparator()
        );

        menu.Items.Add(
            "Exit",
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
            if (pauseItem.Checked) StopMonitor();
            else if (SettingsService.Load() is { } saved) StartMonitor(saved);
            notifyIcon.Text = pauseItem.Checked ? "IME Layout Router - Paused" : "IME Layout Router";
        };

        statusTimer = new System.Windows.Forms.Timer { Interval = 500, Enabled = true };
        statusTimer.Tick += (_, _) =>
        {
            if (settingsRequest?.WaitOne(0) == true) ShowSettings();
            if (monitorTask?.IsFaulted == true && !errorShown)
            {
                errorShown = true;
                notifyIcon.Text = "IME Layout Router - Stopped";
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
        try { ShowSettingsCore(); }
        finally { settingsVisible = false; }
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

        if (!pauseItem.Checked) StartMonitor(
            form.SelectedConfiguration
        );
    }

    private void StartMonitor(
        RoutingConfiguration configuration
    )
    {
        StopMonitor();
        errorShown = false;

        monitorCancellation =
            new CancellationTokenSource();

        CancellationToken token =
            monitorCancellation.Token;

        monitorTask =
            Task.Run(
                () =>
                    RoutingMonitor.Run(
                        configuration,
                        token
                    ),
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

        monitorCancellation.Dispose();

        monitorCancellation = null;
        monitorTask = null;
    }

    protected override void ExitThreadCore()
    {
        statusTimer.Stop();
        statusTimer.Dispose();
        notifyIcon.Visible =
            false;

        StopMonitor();

        notifyIcon.Dispose();

        base.ExitThreadCore();
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
