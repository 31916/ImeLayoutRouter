using System.Windows.Forms;
using static UiText;

internal sealed class GuardTrayContext : ApplicationContext
{
    private readonly CompositionGuardClient guard = new();
    private readonly Icon appIcon = new(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
    private readonly NotifyIcon icon;
    private readonly System.Windows.Forms.Timer timer;
    private bool settingsVisible;
    private GuardSettingsForm? activeSettings;
    private bool exiting;
    private bool errorShown;
    private readonly ToolStripMenuItem status;
    internal GuardTrayContext(bool firstRun, EventWaitHandle request, EventWaitHandle stopRequest)
    {
        var menu = new ContextMenuStrip();
        status = new ToolStripMenuItem(T("小窓の非表示: オフ", "Popup suppression: off")) { Enabled = false };
        menu.Items.Add(status);
        menu.Items.Add(T("設定", "Settings"), null, (_, _) => ShowSettings());
        menu.Items.Add(T("終了して表示を戻す", "Exit and restore display"), null, (_, _) => ExitThread());
        icon = new NotifyIcon { Icon = appIcon, Text = "IME Layout Router V3 Preview", ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => ShowSettings();
        var settings = GuardSettings.Load();
        bool showInitialSettings = firstRun || settings == null;
        timer = new System.Windows.Forms.Timer { Interval = 500, Enabled = true };
        timer.Tick += (_, _) =>
        {
            if (stopRequest.WaitOne(0)) { ExitThread(); return; }
            if (showInitialSettings || request.WaitOne(0))
            { showInitialSettings = false; ShowSettings(); }
            if (exiting) return;
            if (guard.CheckError() != null && !errorShown)
            {
                errorShown = true; status.Text = T("停止中: 設定を開いて再開", "Stopped: open Settings to retry");
                icon.ShowBalloonTip(8000, "IME Layout Router V3", T("小窓の監視が停止しました。設定を開くと再開します。", "Popup monitoring stopped. Open Settings to retry."), ToolTipIcon.Warning);
            }
        };
        // Open the initial modal dialog only after Application.Run subscribes
        // to this context's exit event. Otherwise --shutdown during first run
        // can dispose the tray before the application loop even starts.
        if (!showInitialSettings) Apply(settings!.Enabled);
    }
    private void ShowSettings()
    {
        if (settingsVisible || exiting) return;
        settingsVisible = true;
        guard.SetEnabled(false);
        try
        {
            using var form = new GuardSettingsForm(GuardSettings.Load(), GuardStartup.IsEnabled());
            activeSettings = form;
            if (form.ShowDialog() == DialogResult.OK)
            { form.Selection.Save(); GuardStartup.SetEnabled(form.StartWithWindows); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        { MessageBox.Show(T("設定を保存できませんでした。", "Could not save settings.") + "\n" + ex.Message, "IME Layout Router V3"); }
        finally
        {
            activeSettings = null; settingsVisible = false;
            if (!exiting) Apply(GuardSettings.Load()?.Enabled ?? false);
        }
    }
    private void Apply(bool enabled)
    {
        errorShown = false; guard.SetEnabled(enabled);
        status.Text = enabled ? T("小窓の非表示: オン", "Popup suppression: on") : T("小窓の非表示: オフ", "Popup suppression: off");
    }
    protected override void ExitThreadCore()
    {
        if (exiting) return;
        exiting = true;
        activeSettings?.Close();
        timer.Dispose(); guard.Dispose(); icon.Visible = false; icon.Dispose(); appIcon.Dispose();
        base.ExitThreadCore();
    }
}
