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
#if SIMPLE_EDITION
        var chinese = new InputProfile { Type = InputProfileType.InputProcessor, LanguageId = 0x0804, DisplayName = "Chinese test IME" };
        var expanded = new RoutingConfiguration(source, target, [source, chinese], preferences);
        if (expanded.RouteAllSupportedImes || expanded.Sources.Count != 1
            || expanded.Preferences.HotkeysEnabled || expanded.Preferences.ApplicationRules.Length != 0
            || expanded.IsSourceLayout((IntPtr)0x08040804))
            throw new Exception("Simple edition must discard advanced routing and non-Japanese sources");
        foreach (string language in new[] { "ja-JP", "en-US" })
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            using var form = new SettingsForm(config, false, ([source, chinese], [target]));
            var restored = form.ReadConfiguration() ?? throw new Exception("Simple settings lost");
            if (!form.Text.Contains("日本語簡易版") || !restored.Source.HasSameIdentityAs(source)
                || !restored.Target.HasSameIdentityAs(target) || form.StartWithWindows
                || Descendants(form).OfType<TabControl>().Any()) throw new Exception("Simple UI changed edition behavior");
            using var empty = new SettingsForm(null, false, ([chinese], [target]));
            if (Descendants(empty).OfType<Button>().Single(b => b.Text == "保存").Enabled)
                throw new Exception("Simple edition must reject non-Japanese source selection");
        }
        if (EditionPolicy.SettingsFile != "settings-v1.json") throw new Exception("V1 settings must be separate");
        Console.WriteLine("PASS V1 Japanese-only settings, source restriction and removal of advanced preferences");
#else
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
#endif
        // Real registry operations in a disposable test key, never the user's Run key.
        string testKey = @"Software\ImeLayoutRouter.Tests\" + Guid.NewGuid().ToString("N");
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(testKey);
            const string v1 = "\"C:\\Apps\\V1\\ImeLayoutRouter.exe\"";
            const string v2 = "\"C:\\Apps\\V2\\ImeLayoutRouter.exe\"";
            StartupManager.SetEnabled(key, true, v1);
            StartupManager.SetEnabled(key, false, v2);
            if (key.GetValue("ImeLayoutRouter") as string != v1) throw new Exception("Disabling V2 removed V1 startup");
            StartupManager.SetEnabled(key, true, v2);
            if (key.GetValue("ImeLayoutRouter") as string != v2) throw new Exception("Startup ownership did not transfer");
            StartupManager.SetEnabled(key, false, v2);
            if (key.GetValue("ImeLayoutRouter") != null) throw new Exception("Own startup was not removed");
        }
        finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testKey, false); }
        Console.WriteLine("PASS startup ownership transfer and isolation between editions");

        foreach (int schema in new[] { 1, 2, 3 })
        {
            string json = "{\"Version\":" + schema + ",\"Source\":{\"LanguageId\":1041},\"Target\":{\"LanguageId\":1033,\"Hkl\":67699721}}";
            var migrated = SettingsService.FromJson(json, [source], [target]);
            if (migrated == null || !migrated.Target.HasSameIdentityAs(target)) throw new Exception("Legacy settings migration failed");
        }
        Console.WriteLine("PASS settings schema 1/2/3 migration");
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
