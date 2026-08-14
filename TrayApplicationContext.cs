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

    public TrayApplicationContext(
        bool showSettingsOnStartup = false
    )
    {
        ContextMenuStrip menu =
            new ContextMenuStrip();

        menu.Items.Add(
            "Settings",
            null,
            (_, _) => ShowSettings()
        );

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

        SettingsService.Save(
            form.SelectedConfiguration
        );

        StartMonitor(
            form.SelectedConfiguration
        );
    }

    private void StartMonitor(
        RoutingConfiguration configuration
    )
    {
        StopMonitor();

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
        notifyIcon.Visible =
            false;

        StopMonitor();

        notifyIcon.Dispose();

        base.ExitThreadCore();
    }
}