using System.Runtime.InteropServices;
using System.Windows.Forms;

// Opt-in manual fixture. Keyboard input is supplied from the real desktop, not
// synthesized by this test. Only this fixture's disposable text is inspected.
static class CompositionProbe
{
    public static void Run()
    {
        var candidates = TsfProfileEnumerator.GetSelectableProfiles();
        using var form = new Form { Text = "IME Layout Router composition test", Width = 760, Height = 400 };
        var sources = new ComboBox { Left = 20, Top = 20, Width = 700, DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = nameof(InputProfile.DisplayName), DataSource = candidates.Sources };
        var normal = new TextBox { Left = 20, Top = 100, Width = 700, ImeMode = ImeMode.NoControl };
        var native = new Button { Text = "1. Start native input", Left = 20, Top = 60, Width = 215 };
        var direct = new Button { Text = "2. Switch to direct input", Left = 245, Top = 60, Width = 230 };
        var verify = new Button { Text = "3. Verify typed text", Left = 485, Top = 60, Width = 235 };
        var status = new Label { Left = 20, Top = 145, Width = 700, Height = 155, Text = "Choose an IME, start native input, and type a native character.\nThen verify, switch to direct input and type @ using the target layout." };
        form.Controls.AddRange([sources, native, direct, verify, normal, status]);
        var target = candidates.Targets.FirstOrDefault() ?? throw new Exception("No target layout");
        CancellationTokenSource? stop = null;
        Task? monitor = null;
        RoutingConfiguration? config = null;
        bool testingDirect = false;
        bool typedNative = false;
        bool typedTarget = false;
        var originalLayout = GetKeyboardLayout(0);
        using var timer = new System.Windows.Forms.Timer { Interval = 250 };
        string result = "";
        timer.Tick += (_, _) =>
        {
            if (config == null) return;
            var snapshot = RoutingMonitor.ReadSnapshot(config);
            // Buttons do not retain an edit control's IME context. Capture the
            // layout while the actual text box has focus, before Verify is clicked.
            if (normal.Focused)
            {
                typedNative |= !testingDirect && normal.Text.Any(c => c >= 0x3000 && c <= 0xD7AF)
                    && config.IsSourceLayout(GetKeyboardLayout(0));
                typedTarget |= testingDirect && normal.Text == "@" && GetKeyboardLayout(0) == target.Hkl;
            }
            status.Text = $"{EditionPolicy.Label} / {config.Source.DisplayName}\nTarget: {target.DisplayName}\nLayout: {GetKeyboardLayout(0):X}; mode: {snapshot?.Mode}\n{result}";
        };
        native.Click += async (_, _) =>
        {
            try
            {
                if (stop != null) { stop.Cancel(); if (monitor != null) await monitor; stop.Dispose(); }
                if (sources.SelectedItem is not InputProfile source) throw new Exception("Select an enabled IME");
                config = new RoutingConfiguration(source, target);
                normal.Clear(); normal.Focus();
                // Change only our UI thread; do not enable profiles or alter defaults.
                ActivateKeyboardLayout((IntPtr)(source.LanguageId | ((uint)source.LanguageId << 16)), 0);
                object managerObject = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("33C53A50-F456-4884-B049-85FD643ECFED"))!)!;
                try
                {
                    var manager = (ITfInputProcessorProfileMgr)managerObject;
                    var clsid = source.Clsid; var profile = source.ProfileGuid;
                    int hr = manager.ActivateProfile(1, source.LanguageId, ref clsid, ref profile, IntPtr.Zero, 0);
                    if (hr != 0) throw new Exception($"IME activation failed: {hr:X8}");
                }
                finally { Marshal.ReleaseComObject(managerObject); }
                SetMode(normal.Handle, true);
                testingDirect = false; result = "Type a native character, commit it, then click Verify.";
                typedNative = typedTarget = false;
                stop = new CancellationTokenSource();
                var token = stop.Token; var window = form.Handle; var selected = config;
                monitor = Task.Run(() => RoutingMonitor.Run(selected, token, allowedForeground: window));
                timer.Start();
            }
            catch (Exception ex) { result = "FAIL " + ex.Message; Console.Error.WriteLine(result); }
        };
        direct.Click += (_, _) =>
        {
            normal.Clear(); normal.Focus(); SetMode(normal.Handle, false);
            testingDirect = true; result = "Type @ using the target layout, then click Verify.";
        };
        verify.Click += (_, _) =>
        {
            if (config == null) return;
            bool pass = testingDirect ? typedTarget && normal.Text == "@"
                : typedNative && normal.Text.Any(c => c >= 0x3000 && c <= 0xD7AF);
            result = (pass ? "PASS " : "FAIL ") + (testingDirect ? "target-layout @ keystroke" : "native IME composition") + ": " + config.Source.DisplayName;
            Console.WriteLine(result);
            normal.Focus();
        };
        try { Application.Run(form); }
        finally
        {
            stop?.Cancel(); monitor?.GetAwaiter().GetResult(); stop?.Dispose();
            ActivateKeyboardLayout(originalLayout, 0);
        }
    }
    static void SetMode(IntPtr window, bool native)
    {
        var context = ImmGetContext(window);
        if (context == IntPtr.Zero) throw new Exception("Fixture has no IMM context");
        try { ImmSetOpenStatus(context, native); if (native) ImmSetConversionStatus(context, 0x19, 0); }
        finally { ImmReleaseContext(window, context); }
    }
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);
    [DllImport("user32.dll")] static extern IntPtr ActivateKeyboardLayout(IntPtr layout, uint flags);
    [DllImport("imm32.dll")] static extern IntPtr ImmGetContext(IntPtr window);
    [DllImport("imm32.dll")] static extern bool ImmReleaseContext(IntPtr window, IntPtr context);
    [DllImport("imm32.dll")] static extern bool ImmSetOpenStatus(IntPtr context, bool open);
    [DllImport("imm32.dll")] static extern bool ImmSetConversionStatus(IntPtr context, int conversion, int sentence);
}
