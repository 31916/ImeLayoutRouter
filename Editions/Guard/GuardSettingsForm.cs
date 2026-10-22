using System.Windows.Forms;
using static UiText;

internal sealed class GuardSettingsForm : Form
{
    private readonly Icon appIcon = new(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
    private readonly CheckBox enabled = new() { AutoSize = true };
    private readonly CheckBox startup = new() { AutoSize = true };
    internal GuardSettings Selection => new() { Enabled = enabled.Checked };
    internal bool StartWithWindows => startup.Checked;
    internal GuardSettingsForm(GuardSettings? current, bool startWithWindows)
    {
        Text = T("IME Layout Router V3 — 小窓非表示・試験版", "IME Layout Router V3 — Composition Guard Preview");
        Icon = appIcon;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(570, 340);
        MinimumSize = new Size(590, 380);
        StartPosition = FormStartPosition.CenterScreen;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16),
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        Controls.Add(panel);
        panel.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(520, 0),
            Text = T("画面左上へ出るWindows標準の未確定文字ウィンドウを隠す単体版です。IMEやキーボード配列は切り替えません。",
                "Hides the Windows fallback composition popup when it appears at a screen's top-left corner. Does not switch IMEs or keyboard layouts.") });
        enabled.Text = T("左上の未確定文字の小窓を隠す（試験機能）", "Hide the corner composition popup (experimental)");
        enabled.Checked = current?.Enabled ?? true;
        panel.Controls.Add(enabled);
        panel.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(3, 12, 3, 12),
            Text = T("小窓内の入力途中の文字は見えなくなります。入力先や表示位置の不具合を直す機能ではありません。独自の描画方式を使うIMEには適用されません。オフにすると元の表示に戻ります。",
                "Uncommitted text in that popup becomes invisible. This does not repair focus or positioning bugs. IMEs with custom rendering are not supported. Turn this off to restore normal display.") });
        startup.Text = T("Windowsと同時に起動する", "Start with Windows");
        startup.Checked = startWithWindows;
        panel.Controls.Add(startup);
        var buttons = new FlowLayoutPanel { Width = 520, Height = 42 };
        var save = new Button { Text = T("保存", "Save"), AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = T("キャンセル", "Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange([save, cancel]); panel.Controls.Add(buttons);
        AcceptButton = save; CancelButton = cancel;
    }
    protected override void Dispose(bool disposing)
    { base.Dispose(disposing); if (disposing) appIcon.Dispose(); }
}
