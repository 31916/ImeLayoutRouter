using System.Drawing;
using System.Windows.Forms;

sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon icon;
    private readonly System.Windows.Forms.Timer timer;
    private CancellationTokenSource? stop;
    private Task monitor = Task.CompletedTask;
    private bool settingsVisible, errorShown;

    public TrayApplicationContext(bool firstRun, EventWaitHandle request)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("設定", null, (_, _) => ShowSettings());
        menu.Items.Add("終了", null, (_, _) => ExitThread());
        icon = new NotifyIcon { Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico")),
            Text = "IME Layout Router V1 日本語簡易版", ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => ShowSettings();
        timer = new System.Windows.Forms.Timer { Interval = 500, Enabled = true };
        timer.Tick += (_, _) =>
        {
            if (request.WaitOne(0)) ShowSettings();
            if (monitor.IsFaulted && !errorShown)
            {
                errorShown = true;
                icon.ShowBalloonTip(8000, "IME Layout Router", "自動切替が停止しました。設定を開くと再開します。", ToolTipIcon.Error);
            }
        };
        var config = SettingsService.Load();
        if (firstRun || config == null) ShowSettings();
        else Start(config);
    }
    private void ShowSettings()
    {
        if (settingsVisible) return;
        settingsVisible = true;
        Stop();
        try
        {
            using var form = new SettingsForm(SettingsService.Load(), StartupManager.IsEnabled());
            if (form.ShowDialog() == DialogResult.OK && form.SelectedConfiguration is { } selected)
            {
                SettingsService.Save(selected);
                StartupManager.SetEnabled(form.StartWithWindows);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        { MessageBox.Show("設定を保存できませんでした。\n" + ex.Message, "IME Layout Router", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally
        {
            settingsVisible = false;
            if (SettingsService.Load() is { } config) Start(config);
        }
    }
    private void Start(RoutingConfiguration config)
    {
        var previous = monitor;
        stop = new CancellationTokenSource();
        var token = stop.Token;
        errorShown = false;
        monitor = Task.Run(async () =>
        {
            try { await previous.ConfigureAwait(false); } catch (Exception) { }
            token.ThrowIfCancellationRequested();
            RoutingMonitor.Run(config, token);
        }, token);
    }
    private void Stop()
    {
        if (stop is not { } cancellation) return;
        cancellation.Cancel();
        stop = null;
        try { monitor.Wait(500); } catch (AggregateException) { }
        if (monitor.IsCompleted) cancellation.Dispose();
        else _ = monitor.ContinueWith(_ => cancellation.Dispose(), TaskScheduler.Default);
    }
    protected override void ExitThreadCore()
    {
        timer.Dispose();
        Stop();
        icon.Visible = false;
        icon.Dispose();
        base.ExitThreadCore();
    }
}
