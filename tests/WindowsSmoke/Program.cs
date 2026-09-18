using System.Runtime.InteropServices;
using System.Windows.Forms;

// Opt-in desktop integration fixture. Changes input only on its own UI thread.
// No user text, network access, installed settings or registry changes.
static class Smoke
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        using var form = new Form { Text = "IME Layout Router integration test", Width = 660, Height = 240 };
        var normal = new TextBox { Left = 20, Top = 50, Width = 580, ImeMode = ImeMode.NoControl };
        var password = new TextBox { Left = 20, Top = 110, Width = 580, UseSystemPasswordChar = true };
        form.Controls.Add(new Label { Text = "Testing empty normal and password fields; closes automatically.", Left = 20, Top = 15, Width = 600 });
        form.Controls.Add(normal);
        form.Controls.Add(password);
        form.Shown += async (_, _) =>
        {
            IntPtr original = GetKeyboardLayout(0);
            using var cancellation = new CancellationTokenSource();
            Task? monitor = null;
            try
            {
                var candidates = TsfProfileEnumerator.GetSelectableProfiles();
                var source = candidates.Sources.First(p => p.LanguageId == 0x0411);
                var target = candidates.Targets.First();
                var config = new RoutingConfiguration(source, target);
                ActivateKeyboardLayout((IntPtr)0x04110411, 0);
                normal.Focus();
                await Task.Delay(500);
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
                if (native?.Mode != ImeInputMode.Native) throw new Exception($"Expected native baseline, got {native}");
                Console.WriteLine("PASS live native IMM state");
                monitor = Task.Run(() => RoutingMonitor.Run(config, cancellation.Token));
                context = ImmGetContext(normal.Handle);
                try { ImmSetOpenStatus(context, false); }
                finally { ImmReleaseContext(normal.Handle, context); }
                await Until(() => GetKeyboardLayout(0) == target.Hkl, "native -> direct -> target layout");

                cancellation.Cancel();
                await monitor;
                ActivateKeyboardLayout((IntPtr)0x04110411, 0);
                password.Focus();
                await Until(() => RoutingMonitor.ReadSnapshot(config, fields)?.RequiresDirectInput == true,
                    "live password structural metadata");
                Console.WriteLine("3 Windows integration checks passed.");
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
    [DllImport("user32.dll")] static extern IntPtr ActivateKeyboardLayout(IntPtr layout, uint flags);
    [DllImport("imm32.dll")] static extern IntPtr ImmGetContext(IntPtr window);
    [DllImport("imm32.dll")] static extern bool ImmReleaseContext(IntPtr window, IntPtr context);
    [DllImport("imm32.dll")] static extern bool ImmSetOpenStatus(IntPtr context, bool open);
    [DllImport("imm32.dll")] static extern bool ImmSetConversionStatus(IntPtr context, int conversion, int sentence);
}
