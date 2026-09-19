#if !SIMPLE_EDITION
using System.Windows.Forms;

static class HotkeyProbe
{
    public static void Run()
    {
        using var form = new Form { Text = "IME Layout Router shortcut test", Width = 660, Height = 230 };
        var label = new Label { Left = 20, Top = 20, Width = 610, Height = 150 };
        form.Controls.Add(label);
        int delivered = 0;
        using var shortcuts = new GlobalHotkeys(id =>
        {
            delivered |= 1 << id;
            Console.WriteLine($"PASS real WM_HOTKEY delivery: command {id}");
            label.Text = delivered == 14 ? "PASS all three Windows global shortcuts. Close this test window."
                : $"Received command {id}. Test Ctrl+Alt+F8, Ctrl+Alt+F9 and Ctrl+Alt+F10.";
        });
        form.Activated += (_, _) =>
        {
            string? error = shortcuts.Apply(new RoutingPreferences { HotkeysEnabled = true });
            label.Text = error == null ? "Test Ctrl+Alt+F8, Ctrl+Alt+F9 and Ctrl+Alt+F10.\nRegistration is removed whenever this test loses focus."
                : "FAIL cannot register shortcuts: " + error;
            if (error != null) Console.Error.WriteLine(label.Text);
        };
        form.Deactivate += (_, _) => shortcuts.Apply(new RoutingPreferences());
        Application.Run(form);
        if (delivered != 14) Environment.ExitCode = 1;
    }
}
#endif
