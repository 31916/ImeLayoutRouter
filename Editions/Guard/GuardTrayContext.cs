using System.Windows.Forms;
using static UiText;

internal sealed class GuardTrayContext : ApplicationContext
{
    private readonly CompositionGuardClient guard = new();
    private readonly Icon appIcon = new(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
    private readonly NotifyIcon icon;
    private readonly System.Windows.Forms.Timer timer;
    private bool settingsVisible;
    private bool errorShown;
    private readonly ToolStripMenuItem status;
    internal GuardTrayContext(bool firstRun, EventWaitHandle request)
    {
        var menu = new ContextMenuStrip();
        status = new ToolStripMenuItem(T("小窓の非表示: オフ", "Popup suppression: off")) { Enabled = false };
        menu.Items.Add(status);
        menu.Items.Add(T("設定", "Settings"), null, (_, _) => ShowSettings());
        menu.Items.Add(T("終了して表示を戻す", "Exit and restore display"), null, (_, _) => ExitThread());
        icon = new NotifyIcon { Icon = appIcon, Text = "IME Layout Router V3 Preview", ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => ShowSettings();
        timer = new System.Windows.Forms.Timer { Interval = 500, Enabled = true };
        timer.Tick += (_, _) =>
        {
            if (request.WaitOne(0)) ShowSettings();
            if (guard.CheckError() != null && !errorShown)
            {
                errorShown = true; status.Text = T("停止中: 設定を開いて再開", "Stopped: open Settings to retry");
                icon.ShowBalloonTip(8000, "IME Layout Router V3", T("小窓の監視が停止しました。設定を開くと再開します。", "Popup monitoring stopped. Open Settings to retry."), ToolTipIcon.Warning);
            }
        };
        if (firstRun || GuardSettings.Load() is not { } settings) ShowSettings();
        else Apply(settings.Enabled);
    }
    private void ShowSettings()
    {
        if (settingsVisible) return;
        settingsVisible = true;
        guard.SetEnabled(false);
        try
        {
            using var form = new GuardSettingsForm(GuardSettings.Load(), GuardStartup.IsEnabled());
            if (form.ShowDialog() == DialogResult.OK)
            { form.Selection.Save(); GuardStartup.SetEnabled(form.StartWithWindows); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        { MessageBox.Show(T("設定を保存できませんでした。", "Could not save settings.") + "\n" + ex.Message, "IME Layout Router V3"); }
        finally { settingsVisible = false; Apply(GuardSettings.Load()?.Enabled ?? false); }
    }
    private void Apply(bool enabled)
    {
        errorShown = false; guard.SetEnabled(enabled);
        status.Text = enabled ? T("小窓の非表示: オン", "Popup suppression: on") : T("小窓の非表示: オフ", "Popup suppression: off");
    }
    protected override void ExitThreadCore()
    {
        timer.Dispose(); guard.Dispose(); icon.Visible = false; icon.Dispose(); appIcon.Dispose();
        base.ExitThreadCore();
    }
}
