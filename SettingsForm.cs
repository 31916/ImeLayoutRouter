using System.Drawing;
using System.Windows.Forms;
using static UiText;

sealed class SettingsForm : Form
{
    private readonly ComboBox sources = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 620 };
    private readonly ComboBox targets = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 620 };
    private readonly CheckBox allImes = new() { AutoSize = true };
    private readonly CheckBox startup = new() { AutoSize = true };
    private readonly CheckBox hotkeys = new() { AutoSize = true };
    private readonly ComboBox defaultMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 620 };
    private readonly DataGridView rules = new() { Width = 620, Height = 240, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    private readonly ComboBox pauseKey = KeyPicker(), targetKey = KeyPicker(), restoreKey = KeyPicker();
    private readonly Label previewStatus = new() { AutoSize = true, MaximumSize = new Size(620, 0) };
    private readonly Button previewButton = new() { AutoSize = true };
    private readonly System.Windows.Forms.Timer previewTimer = new() { Interval = 200 };
    private CancellationTokenSource? previewCancellation;
    private Task? previewTask;
    private RoutingSession? previewSession;
    private readonly List<InputProfile> sourceProfiles;
    public RoutingConfiguration? SelectedConfiguration { get; private set; }
    public bool StartWithWindows => startup.Checked;

    public SettingsForm(RoutingConfiguration? currentConfiguration, bool startWithWindows,
        (List<InputProfile> Sources, List<InputProfile> Targets)? availableProfiles = null)
    {
        Text = currentConfiguration == null ? T("IME Layout Router — 初回設定", "IME Layout Router — First-time setup")
            : T("IME Layout Router — 設定", "IME Layout Router — Settings");
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(690, 620);
        MinimumSize = new Size(710, 650);
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };
        var back = new Button { Text = T("戻る", "Back"), AutoSize = true };
        var next = new Button { Text = T("次へ", "Next"), AutoSize = true };
        var save = new Button { Text = T("保存", "Save"), AutoSize = true };
        var cancel = new Button { Text = T("キャンセル", "Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        navigation.Controls.AddRange([back, next, save, cancel]);
        Controls.Add(tabs);
        Controls.Add(navigation);
        CancelButton = cancel;
        back.Click += (_, _) => tabs.SelectedIndex = Math.Max(0, tabs.SelectedIndex - 1);
        next.Click += (_, _) => tabs.SelectedIndex = Math.Min(tabs.TabCount - 1, tabs.SelectedIndex + 1);
        tabs.SelectedIndexChanged += (_, _) => { back.Enabled = tabs.SelectedIndex > 0; next.Enabled = tabs.SelectedIndex < tabs.TabCount - 1; StopPreview(); };
        back.Enabled = false;

        var profiles = Page(tabs, T("1. IME・配列", "1. IME and layout"));
        Note(profiles, T("日本語などを入力するIMEと、英数入力で使う配列を選びます。最後の画面で試してから保存できます。",
            "Choose your IME and the layout to use for Latin input. Try them on the last page before saving."));
        Note(profiles, T("切替元のIME", "Source IME"));
        profiles.Controls.Add(sources);
        Note(profiles, T("英数入力のキーボード配列", "Keyboard layout for Latin input"));
        profiles.Controls.Add(targets);
        allImes.Text = T("有効な日本語・中国語・韓国語IMEをすべて対象にする", "Route all enabled Japanese, Chinese and Korean IMEs");
        allImes.Checked = currentConfiguration?.RouteAllSupportedImes ?? false;
        profiles.Controls.Add(allImes);
        Note(profiles, T("同じ言語のIMEに共通で適用されます。選択肢がない場合は、Windowsの言語設定でIMEと配列を追加してから開き直してください。",
            "Routing applies to IMEs of the selected language. If choices are missing, add an IME and layout in Windows language settings, then reopen this dialog."));
        startup.Text = T("Windowsと同時に起動する", "Start with Windows");
        startup.Checked = currentConfiguration == null || startWithWindows;
        profiles.Controls.Add(startup);

        var preferences = currentConfiguration?.Preferences ?? new RoutingPreferences();
        var applicationPage = Page(tabs, T("2. アプリ別設定", "2. Applications"));
        Note(applicationPage, T("通常の動作（下のルールが優先されます）", "Default behavior (rules below take priority)"));
        defaultMode.Items.AddRange([T("自動切替", "Automatic"), T("自動切替しない", "Disabled")]);
        defaultMode.SelectedIndex = (int)preferences.DefaultMode;
        applicationPage.Controls.Add(defaultMode);
        var pathColumn = new DataGridViewTextBoxColumn { Name = "Executable", HeaderText = T("実行ファイル", "Executable"), ReadOnly = true, FillWeight = 72 };
        var modeColumn = new DataGridViewComboBoxColumn { Name = "Mode", HeaderText = T("動作", "Behavior"), FillWeight = 28 };
        modeColumn.Items.AddRange([T("自動切替", "Automatic"), T("自動切替しない", "Disabled")]);
        rules.Columns.AddRange(pathColumn, modeColumn);
        foreach (var rule in preferences.ApplicationRules) rules.Rows.Add(rule.ExecutablePath, modeColumn.Items[(int)rule.Mode]);
        applicationPage.Controls.Add(rules);
        var ruleButtons = new FlowLayoutPanel { Width = 620, Height = 42 };
        var add = new Button { Text = T("実行ファイルを追加", "Add executable"), AutoSize = true };
        var remove = new Button { Text = T("選択したルールを削除", "Remove selected rule"), AutoSize = true };
        ruleButtons.Controls.AddRange([add, remove]);
        applicationPage.Controls.Add(ruleButtons);
        Note(applicationPage, T("ファイルのフルパスで判別します。タイトル・URL・入力文は使いません。アプリを識別できない場合、ルールがあるときは自動切替を控えます。",
            "Rules match the full executable path, not titles, URLs or typed text. With rules configured, automatic routing stops when the application cannot be identified."));
        add.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Windows executable (*.exe)|*.exe", CheckFileExists = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string path = Path.GetFullPath(dialog.FileName);
            if (rules.Rows.Cast<DataGridViewRow>().Any(r => string.Equals(r.Cells[0].Value as string, path, StringComparison.OrdinalIgnoreCase))) return;
            rules.Rows.Add(path, modeColumn.Items[1]);
        };
        remove.Click += (_, _) => { if (rules.SelectedRows.Count > 0) rules.Rows.Remove(rules.SelectedRows[0]); };

        var shortcutsPage = Page(tabs, T("3. ショートカット", "3. Shortcuts"));
        hotkeys.Text = T("グローバルショートカットを有効にする", "Enable global shortcuts");
        hotkeys.Checked = preferences.HotkeysEnabled;
        shortcutsPage.Controls.Add(hotkeys);
        Note(shortcutsPage, T("Ctrl + Alt と組み合わせます。他のアプリと競合した場合は登録せず、お知らせします。", "Keys use Ctrl + Alt. If registration conflicts with another application, shortcuts stay disabled and a warning is shown."));
        AddKey(shortcutsPage, T("自動切替の一時停止／再開", "Pause / resume automatic routing"), pauseKey, preferences.PauseKey);
        AddKey(shortcutsPage, T("設定した英数配列に切替", "Switch to target layout"), targetKey, preferences.TargetKey);
        AddKey(shortcutsPage, T("このウィンドウでの直前の配列に戻す", "Restore the previous layout in this window"), restoreKey, preferences.RestoreKey);
        Note(shortcutsPage, T("手動選択は別のウィンドウへ移るまで優先されます。一時停止中・除外アプリでも手動操作はできます。「戻す」は配列を戻す操作で、IMEの入力モードは変更しません。",
            "Manual choices are held until you leave the window. Manual switching also works while paused or excluded. Restore changes the layout; it does not force an IME conversion mode."));
        hotkeys.CheckedChanged += (_, _) => { pauseKey.Enabled = targetKey.Enabled = restoreKey.Enabled = hotkeys.Checked; };
        pauseKey.Enabled = targetKey.Enabled = restoreKey.Enabled = hotkeys.Checked;

        var testPage = Page(tabs, T("4. 入力確認", "4. Try typing"));
        Note(testPage, T("「この設定を試す」で、この画面だけに未保存のIME・配列設定を適用します。アプリ別ルールとショートカットは保存後に有効になります。",
            "Try the unsaved IME/layout selection in this window only. Application rules and shortcuts take effect after saving."));
        previewButton.Text = T("この設定を試す", "Try these settings");
        testPage.Controls.Add(previewButton);
        Note(testPage, T("IME入力（日本語などを入力して確認）", "IME input (try composing native text)"));
        testPage.Controls.Add(new TextBox { Width = 620, ImeMode = ImeMode.NoControl, AccessibleName = "IME input test" });
        Note(testPage, T("英数入力（例：abc+tag@example.invalid、@、記号）", "Latin input (try abc+tag@example.invalid, @ and punctuation)"));
        testPage.Controls.Add(new TextBox { Width = 620, ImeMode = ImeMode.Disable, AccessibleName = "Latin input test" });
        Note(testPage, T("Windowsの言語切替で選んだIMEを有効にし、各欄を移動して試してください。",
            "Use the Windows language switcher to activate your chosen IME, then move between the fields."));
        Note(testPage, T("入力は保存・送信されません。見た目と配列を自分で確認してください。この確認は、ブラウザーなど他アプリの動作を保証するものではありません。",
            "Test text is not saved or sent. Check the characters and layout yourself. This does not validate behavior in browsers or other applications."));
        testPage.Controls.Add(previewStatus);
        previewButton.Click += (_, _) => StartPreview();
        previewTimer.Tick += (_, _) =>
        {
            if (previewTask?.IsFaulted == true) { previewStatus.Text = T("確認処理が停止しました。設定を見直してください。", "Preview stopped. Check your settings."); StopPreview(); }
            else if (previewSession != null) previewStatus.Text = previewSession.Status.LayoutText + "\n" + previewSession.Status.Reason;
        };

        var candidates = availableProfiles ?? TsfProfileEnumerator.GetSelectableProfiles();
        sourceProfiles = candidates.Sources;
        sources.DisplayMember = targets.DisplayMember = nameof(InputProfile.DisplayName);
        sources.DataSource = sourceProfiles;
        targets.DataSource = candidates.Targets;
        sources.SelectedIndex = currentConfiguration == null ? -1 : sourceProfiles.FindIndex(p => p.HasSameIdentityAs(currentConfiguration.Source));
        targets.SelectedIndex = currentConfiguration == null ? -1 : candidates.Targets.FindIndex(p => p.HasSameIdentityAs(currentConfiguration.Target));
        sources.Enabled = !allImes.Checked;
        allImes.CheckedChanged += (_, _) => { sources.Enabled = !allImes.Checked; StopPreview(); if (allImes.Checked && sources.SelectedIndex < 0 && sources.Items.Count > 0) sources.SelectedIndex = 0; };
        sources.SelectedIndexChanged += (_, _) => StopPreview();
        targets.SelectedIndexChanged += (_, _) => StopPreview();
        save.Enabled = previewButton.Enabled = candidates.Sources.Count > 0 && candidates.Targets.Count > 0;
        save.Click += (_, _) =>
        {
            var selected = ReadConfiguration();
            if (selected == null) return;
            StopPreview();
            SelectedConfiguration = selected;
            DialogResult = DialogResult.OK;
            Close();
        };
        FormClosing += (_, _) => StopPreview();
    }

    internal RoutingConfiguration? ReadConfiguration()
    {
        if (sources.SelectedItem is not InputProfile source || targets.SelectedItem is not InputProfile target)
        {
            MessageBox.Show(this, T("IMEと配列を選択してください。", "Select both an IME and a layout."), Text);
            return null;
        }
        rules.EndEdit();
        var preferences = new RoutingPreferences
        {
            DefaultMode = (ApplicationRoutingMode)defaultMode.SelectedIndex,
            ApplicationRules = rules.Rows.Cast<DataGridViewRow>().Select(r => new ApplicationRule((string)r.Cells[0].Value,
                Equals(r.Cells[1].Value, T("自動切替", "Automatic")) ? ApplicationRoutingMode.Automatic : ApplicationRoutingMode.Disabled)).ToArray(),
            HotkeysEnabled = hotkeys.Checked,
            PauseKey = pauseKey.SelectedIndex + 0x75, TargetKey = targetKey.SelectedIndex + 0x75, RestoreKey = restoreKey.SelectedIndex + 0x75
        };
        if (!preferences.IsValid())
        {
            MessageBox.Show(this, T("ショートカットには異なるキーを選び、アプリのルールを確認してください（最大256件）。", "Choose distinct shortcut keys and check the application rules (maximum 256)."), Text);
            return null;
        }
        return new RoutingConfiguration(source, target, allImes.Checked ? sourceProfiles : null, preferences);
    }
    private void StartPreview()
    {
        var selected = ReadConfiguration();
        if (selected == null) return;
        StopPreview();
        if (previewTask is { IsCompleted: false }) return;
        previewCancellation = new CancellationTokenSource();
        previewSession = new RoutingSession();
        var cancellation = previewCancellation;
        var session = previewSession;
        IntPtr window = Handle;
        // Rules must not hide the routing behavior this local exercise demonstrates.
        var config = new RoutingConfiguration(selected.Source, selected.Target, selected.RouteAllSupportedImes ? selected.Sources : null);
        previewTask = Task.Run(() => RoutingMonitor.Run(config, cancellation.Token, session, window));
        previewTimer.Start();
        previewStatus.Text = T("入力欄を選んで試してください。", "Focus a test field and try typing.");
    }
    private void StopPreview()
    {
        previewTimer.Stop();
        var cancellation = previewCancellation;
        var session = previewSession;
        if (cancellation == null) return;
        cancellation.Cancel();
        try { previewTask?.Wait(750); } catch (AggregateException) { }
        if (previewTask == null || previewTask.IsCompleted) { cancellation.Dispose(); session?.Dispose(); }
        else _ = previewTask.ContinueWith(_ => { cancellation.Dispose(); session?.Dispose(); }, TaskScheduler.Default);
        previewCancellation = null;
        previewSession = null;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { StopPreview(); previewTimer.Dispose(); }
        base.Dispose(disposing);
    }
    private static FlowLayoutPanel Page(TabControl tabs, string name)
    {
        var page = new TabPage(name);
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        page.Controls.Add(flow);
        tabs.TabPages.Add(page);
        return flow;
    }
    private static void Note(Control parent, string text) => parent.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(620, 0), Margin = new Padding(3, 8, 3, 8) });
    private static ComboBox KeyPicker()
    {
        var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        box.Items.AddRange(Enumerable.Range(6, 6).Select(n => (object)$"Ctrl + Alt + F{n}").ToArray());
        return box;
    }
    private static void AddKey(Control parent, string caption, ComboBox picker, int key)
    {
        Note(parent, caption);
        picker.SelectedIndex = key - 0x75;
        parent.Controls.Add(picker);
    }
}
