using System.Globalization;
using System.Windows.Forms;

// Constructs our own forms without showing them, routing input, registering
// shortcuts, reading installed settings, or changing startup configuration.
static class UiSmoke
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        var source = new InputProfile { Type = InputProfileType.InputProcessor, LanguageId = 0x0411, DisplayName = "Japanese test IME" };
        var target = new InputProfile { Type = InputProfileType.KeyboardLayout, LanguageId = 0x0409, Hkl = (IntPtr)0x04090409, DisplayName = "US test layout" };
        var preferences = new RoutingPreferences { HotkeysEnabled = true, PauseKey = 0x75,
            ApplicationRules = [new(@"C:\Games\game.exe", ApplicationRoutingMode.Disabled), new(@"C:\Tools\edit.exe", ApplicationRoutingMode.Automatic)] };
        var config = new RoutingConfiguration(source, target, preferences: preferences);
        foreach (string language in new[] { "ja-JP", "en-US" })
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            using var form = new SettingsForm(config, false, ([source], [target]));
            var restored = form.ReadConfiguration() ?? throw new Exception("Configuration lost in settings UI");
            if (!restored.Source.HasSameIdentityAs(source) || !restored.Target.HasSameIdentityAs(target)
                || restored.Preferences.PauseKey != preferences.PauseKey || !restored.Preferences.HotkeysEnabled
                || restored.Preferences.ApplicationRules.Length != 2
                || restored.Preferences.Resolve(@"C:\Games\game.exe") != ApplicationRoutingMode.Disabled
                || restored.Preferences.Resolve(@"C:\Tools\edit.exe") != ApplicationRoutingMode.Automatic
                || form.StartWithWindows) throw new Exception("Settings round-trip changed saved choices");
            using var empty = new SettingsForm(null, false, ([], []));
            var save = Descendants(empty).OfType<Button>().Single(b => b.Text == UiText.T("保存", "Save"));
            if (save.Enabled) throw new Exception("Cannot save without installed profiles");
            Console.WriteLine($"PASS {language}: settings round-trip, exclusions and unavailable-profile guard");
        }
        var registered = new HashSet<int>();
        bool conflict = true;
        using (var bindings = new GlobalHotkeys(_ => { },
            (id, key) => { if (conflict && key == 0x78) return false; return registered.Add(id); },
            id => registered.Remove(id), () => "test conflict"))
        {
            if (bindings.Apply(new RoutingPreferences { HotkeysEnabled = true }) == null || registered.Count != 0)
                throw new Exception("Hotkey conflict must roll back all registrations");
            conflict = false;
            if (bindings.Apply(new RoutingPreferences { HotkeysEnabled = true }) != null || registered.Count != 3)
                throw new Exception("Hotkeys must recover after a conflict is resolved");
            bindings.Apply(new RoutingPreferences());
            if (registered.Count != 0) throw new Exception("Disabling hotkeys must unregister the complete set");
        }
        Console.WriteLine("PASS hotkey conflict rollback and recovery (fake registrar, no global keys reserved)");
    }
    static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control control in root.Controls)
        {
            yield return control;
            foreach (var child in Descendants(control)) yield return child;
        }
    }
}
