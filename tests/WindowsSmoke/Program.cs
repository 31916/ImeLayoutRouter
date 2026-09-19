using System.Runtime.InteropServices;
using System.Windows.Forms;

// Opt-in desktop integration fixture. Changes input only on its own UI thread.
// No user text, network access, installed settings or registry changes.
static class Smoke
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--browser") { BrowserProbe.Run(args); return; }
        Application.EnableVisualStyles();
        if (args.Length == 1 && args[0] == "--composition") { CompositionProbe.Run(); return; }
        using var form = new Form { Text = "IME Layout Router integration test", Width = 660, Height = 290 };
        var normal = new TextBox { Left = 20, Top = 50, Width = 580, ImeMode = ImeMode.NoControl };
        var password = new TextBox { Left = 20, Top = 110, Width = 580, UseSystemPasswordChar = true };
        form.Controls.Add(new Label { Text = "Testing empty normal and password fields; closes automatically.", Left = 20, Top = 15, Width = 600 });
        form.Controls.Add(normal);
        form.Controls.Add(password);
        var start = new Button { Text = "Start desktop checks", Left = 20, Top = 160, Width = 240 };
        form.Controls.Add(start);
        start.Click += async (_, _) =>
        {
            start.Enabled = false;
            IntPtr original = GetKeyboardLayout(0);
            using var cancellation = new CancellationTokenSource();
            using var lifetime = new CancellationTokenSource();
            Task? monitor = null;
            form.Deactivate += (_, _) => { lifetime.Cancel(); cancellation.Cancel(); };
            try
            {
                var candidates = TsfProfileEnumerator.GetSelectableProfiles();
                var source = candidates.Sources.FirstOrDefault(p => p.LanguageId == 0x0411)
                    ?? throw new Exception("Desktop test requires an enabled Japanese IME in this Windows user session.");
                var target = candidates.Targets.FirstOrDefault()
                    ?? throw new Exception("Desktop test requires an enabled non-CJK keyboard layout.");
                var config = new RoutingConfiguration(source, target);
                // Shown can run before activation has finished. Verify the actual
                // foreground control so another window cannot satisfy the baseline.
                await Task.Delay(500);
                form.Activate();
                ActivateKeyboardLayout((IntPtr)0x04110411, 0);
                form.ActiveControl = normal;
                normal.Focus();
                await Until(() => GetForegroundWindow() == form.Handle && normal.Focused,
                    "normal test control owns foreground focus");
                IntPtr context = ImmGetContext(normal.Handle);
                if (context == IntPtr.Zero) throw new Exception("Normal field has no IMM context");
                try
                {
                    ImmSetOpenStatus(context, true);
                    ImmSetConversionStatus(context, 0x19, 0);
                }
                finally { ImmReleaseContext(normal.Handle, context); }
                await Task.Delay(500);
                using var fields = new FocusedInputProbe();
                var native = RoutingMonitor.ReadSnapshot(config);
                if (native?.Mode != ImeInputMode.Native || native?.Context.Focus != normal.Handle)
                    throw new Exception($"Expected native baseline in the test control, got {native}");
                cancellation.Token.ThrowIfCancellationRequested();
                Console.WriteLine("PASS live native IMM state");
                IntPtr testWindow = form.Handle;
                monitor = Task.Run(() => RoutingMonitor.Run(config, cancellation.Token, allowedForeground: testWindow));
                context = ImmGetContext(normal.Handle);
                try { ImmSetOpenStatus(context, false); }
                finally { ImmReleaseContext(normal.Handle, context); }
                await Until(() => GetKeyboardLayout(0) == target.Hkl, "native -> direct -> target layout");

                await Task.Delay(200, lifetime.Token);
                ActivateKeyboardLayout((IntPtr)0x04110411, 0);
                await Until(() => RoutingMonitor.ReadSnapshot(config) is { Mode: ImeInputMode.Native } s
                    && s.Context.Focus == normal.Handle, "explicit return to IME restores native input");

                cancellation.Cancel();
                await monitor;
                ActivateKeyboardLayout((IntPtr)0x04110411, 0);
                form.ActiveControl = password;
                password.Focus();
                await Until(() => RoutingMonitor.ReadSnapshot(config, fields) is { RequiresDirectInput: true } snapshot
                    && snapshot.Context.Focus == password.Handle,
                    "live password structural metadata");

#if !SIMPLE_EDITION
                form.ActiveControl = normal;
                normal.Focus();
                ActivateKeyboardLayout((IntPtr)0x04110411, 0);
                context = ImmGetContext(normal.Handle);
                try { ImmSetOpenStatus(context, false); }
                finally { ImmReleaseContext(normal.Handle, context); }
                await Until(() => RoutingMonitor.ReadSnapshot(config) is { Mode: ImeInputMode.Direct } s
                    && s.Context.Focus == normal.Handle, "direct-input baseline for routing controls");
                lifetime.Token.ThrowIfCancellationRequested();
                string executable = Environment.ProcessPath ?? throw new Exception("Test executable path unavailable");
                var excluded = new RoutingConfiguration(source, target, preferences: new RoutingPreferences
                {
                    ApplicationRules = [new(executable, ApplicationRoutingMode.Disabled)]
                });
                using (var exclusionSession = new RoutingSession())
                using (var exclusionStop = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                {
                    monitor = Task.Run(() => RoutingMonitor.Run(excluded, exclusionStop.Token, exclusionSession, testWindow));
                    try
                    {
                        await Until(() => exclusionSession.Status.Reason == UiText.T("アプリ別ルールにより自動切替を停止", "Automatic routing disabled by application rule"), "live executable exclusion is applied");
                        await Task.Delay(200, lifetime.Token);
                        if (GetKeyboardLayout(0) == target.Hkl) throw new Exception("Excluded window was routed");
                    }
                    finally { exclusionStop.Cancel(); await monitor; }
                }

                using (var controlSession = new RoutingSession())
                using (var controlStop = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                {
                    controlSession.SetPaused(true);
                    monitor = Task.Run(() => RoutingMonitor.Run(config, controlStop.Token, controlSession, testWindow));
                    try
                    {
                        await Until(() => controlSession.Status is { Paused: true, Snapshot.Mode: ImeInputMode.Direct }, "live pause observes state without routing");
                        await Task.Delay(200, lifetime.Token);
                        if (GetKeyboardLayout(0) == target.Hkl) throw new Exception("Paused window was routed");
                        var captured = RoutingMonitor.CaptureManualFocus(Interlocked.Read(ref controlSession.FocusVersion))
                            ?? throw new Exception("Manual target capture failed");
                        controlSession.Request(new ManualRoutingRequest(ManualRoutingAction.Target, captured));
                        await Until(() => GetKeyboardLayout(0) == target.Hkl
                            && controlSession.Status.Snapshot?.KeyboardLayout == target.Hkl, "manual target works while paused");
                        captured = RoutingMonitor.CaptureManualFocus(Interlocked.Read(ref controlSession.FocusVersion))
                            ?? throw new Exception("Manual restore capture failed");
                        controlSession.Request(new ManualRoutingRequest(ManualRoutingAction.Restore, captured));
                        await Until(() => GetKeyboardLayout(0) == (IntPtr)0x04110411, "manual previous-layout restoration");
                        context = ImmGetContext(normal.Handle);
                        try { ImmSetOpenStatus(context, false); }
                        finally { ImmReleaseContext(normal.Handle, context); }
                        await Until(() => controlSession.Status.Snapshot?.Mode == ImeInputMode.Direct, "direct-input baseline before resuming");
                        controlSession.SetPaused(false);
                        await Until(() => GetKeyboardLayout(0) == target.Hkl
                            && controlSession.Status.Snapshot?.KeyboardLayout == target.Hkl, "resuming restores automatic routing");
                        captured = RoutingMonitor.CaptureManualFocus(Interlocked.Read(ref controlSession.FocusVersion))
                            ?? throw new Exception("Manual hold capture failed");
                        controlSession.Request(new ManualRoutingRequest(ManualRoutingAction.Restore, captured));
                        await Until(() => GetKeyboardLayout(0) == (IntPtr)0x04110411, "manual choice restored with automatic routing enabled");
                        context = ImmGetContext(normal.Handle);
                        try { ImmSetOpenStatus(context, false); }
                        finally { ImmReleaseContext(normal.Handle, context); }
                        await Task.Delay(700, lifetime.Token);
                        if (GetKeyboardLayout(0) != (IntPtr)0x04110411) throw new Exception("Automatic routing overwrote manual choice");
                        Console.WriteLine("PASS manual choice survives the automatic retry interval");
                    }
                    finally { controlStop.Cancel(); await monitor; }
                }
#endif
                Console.WriteLine("Windows integration checks passed for " + EditionPolicy.Label);
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally
            {
                cancellation.Cancel();
                if (monitor != null) await monitor;
                ActivateKeyboardLayout(original, 0);
                form.Close();
            }
        };
        Application.Run(form);
    }

    static async Task Until(Func<bool> predicate, string name)
    {
        for (int i = 0; i < 30; i++)
        {
            if (predicate()) { Console.WriteLine("PASS " + name); return; }
            await Task.Delay(100);
        }
        throw new Exception("Timed out: " + name);
    }
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr ActivateKeyboardLayout(IntPtr layout, uint flags);
    [DllImport("imm32.dll")] static extern IntPtr ImmGetContext(IntPtr window);
    [DllImport("imm32.dll")] static extern bool ImmReleaseContext(IntPtr window, IntPtr context);
    [DllImport("imm32.dll")] static extern bool ImmSetOpenStatus(IntPtr context, bool open);
    [DllImport("imm32.dll")] static extern bool ImmSetConversionStatus(IntPtr context, int conversion, int sentence);
}
