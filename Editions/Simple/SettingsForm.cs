using System.Drawing;
using System.Windows.Forms;

sealed class SettingsForm : Form
{
    private readonly ComboBox sources = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 520 };
    private readonly ComboBox targets = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 520 };
    private readonly CheckBox startup = new() { Text = "Windows起動時に開始する", AutoSize = true };
    public RoutingConfiguration? SelectedConfiguration { get; private set; }
    public bool StartWithWindows => startup.Checked;

    public SettingsForm(RoutingConfiguration? current, bool startWithWindows,
        (List<InputProfile> Sources, List<InputProfile> Targets)? availableProfiles = null)
    {
        Text = "IME Layout Router V1 — 日本語簡易版";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(580, 345);
        MinimumSize = new Size(600, 385);
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = MaximizeBox = false;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16),
            FlowDirection = FlowDirection.TopDown, WrapContents = false };
        Controls.Add(panel);
        void Note(string text) => panel.Controls.Add(new Label { Text = text, AutoSize = true,
            MaximumSize = new Size(520, 0), Margin = new Padding(3, 8, 3, 8) });
        Note("日本語入力はIME、英数入力は選んだ配列に自動で切り替えます。");
        Note("使用する日本語IME");
        panel.Controls.Add(sources);
        Note("英数入力に使うキーボード配列");
        panel.Controls.Add(targets);
        startup.Checked = current == null || startWithWindows;
        panel.Controls.Add(startup);
        var buttons = new FlowLayoutPanel { Width = 520, Height = 42 };
        var save = new Button { Text = "保存", AutoSize = true };
        var cancel = new Button { Text = "キャンセル", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange([save, cancel]);
        panel.Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
        var candidates = availableProfiles ?? TsfProfileEnumerator.GetSelectableProfiles();
        var japanese = candidates.Sources.Where(p => EditionPolicy.SupportsSource(p.LanguageId)).ToList();
        sources.DisplayMember = targets.DisplayMember = nameof(InputProfile.DisplayName);
        sources.DataSource = japanese;
        targets.DataSource = candidates.Targets;
        sources.SelectedIndex = current == null ? (japanese.Count == 1 ? 0 : -1) : japanese.FindIndex(p => p.HasSameIdentityAs(current.Source));
        targets.SelectedIndex = current == null ? -1 : candidates.Targets.FindIndex(p => p.HasSameIdentityAs(current.Target));
        save.Enabled = japanese.Count > 0 && candidates.Targets.Count > 0;
        if (!save.Enabled) Note("Windowsの言語設定に日本語IMEと英数用の配列を追加してから、開き直してください。");
        save.Click += (_, _) =>
        {
            SelectedConfiguration = ReadConfiguration();
            if (SelectedConfiguration == null) { MessageBox.Show(this, "日本語IMEと英数配列を選択してください。", Text); return; }
            DialogResult = DialogResult.OK;
            Close();
        };
    }
    internal RoutingConfiguration? ReadConfiguration() => sources.SelectedItem is InputProfile source
        && targets.SelectedItem is InputProfile target ? new RoutingConfiguration(source, target) : null;
}
