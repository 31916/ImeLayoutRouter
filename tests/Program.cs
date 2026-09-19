using System;

static class Tests
{
    static readonly IntPtr Japanese = (IntPtr)0x04110411;
    static readonly IntPtr Swiss = (IntPtr)0x08070807;
    static readonly InputContext Editor = new((IntPtr)1, (IntPtr)2, 3);
    static readonly InputContext Email = new((IntPtr)4, (IntPtr)5, 6);
    static readonly InputContext OtherField = new((IntPtr)1, (IntPtr)7, 3);
    static readonly RoutingConfiguration Config = new(
        new InputProfile { LanguageId = 0x0411, Type = InputProfileType.InputProcessor },
        new InputProfile { Hkl = Swiss, LanguageId = 0x0807, Type = InputProfileType.KeyboardLayout });
    static int passed;

    static void Equal<T>(T expected, T actual)
    {
        if (!Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
    }
    static void Test(string name, Action body)
    {
        body();
        Console.WriteLine($"PASS {name}");
        passed++;
    }
    static InputSnapshot State(ImeInputMode mode, InputContext? context = null, IntPtr? layout = null)
        => new(context ?? Editor, layout ?? Japanese, mode);

    static void Main()
    {
        Test("A already active at startup", () => Equal(RoutingAction.SwitchToTarget,
            new RoutingPolicy(Config).Evaluate(State(ImeInputMode.Direct), 0)));
        Test("Email after another app used native IME", () =>
        {
            var p = new RoutingPolicy(Config);
            p.Evaluate(State(ImeInputMode.Native), 0);
            p.Evaluate(State(ImeInputMode.Unknown, layout: (IntPtr)0x04090409), 100);
            Equal(RoutingAction.SwitchToTarget, p.Evaluate(State(ImeInputMode.Direct, Email), 200));
        });
        Test("Persistent A retries rejected or ignored request", () =>
        {
            var p = new RoutingPolicy(Config);
            Equal(RoutingAction.SwitchToTarget, p.Evaluate(State(ImeInputMode.Direct), 0));
            Equal(RoutingAction.None, p.Evaluate(State(ImeInputMode.Direct), 100));
            Equal(RoutingAction.SwitchToTarget, p.Evaluate(State(ImeInputMode.Direct), 500));
            Equal(RoutingAction.SwitchToTarget, p.Evaluate(State(ImeInputMode.Direct), 1000));
            Equal(RoutingAction.None, p.Evaluate(State(ImeInputMode.Unknown, layout: Swiss), 1100));
        });
        Test("Unknown status cannot hide later direct input", () =>
        {
            var p = new RoutingPolicy(Config);
            p.Evaluate(State(ImeInputMode.Native), 0);
            Equal(RoutingAction.None, p.Evaluate(State(ImeInputMode.Unknown), 100));
            Equal(RoutingAction.SwitchToTarget, p.Evaluate(State(ImeInputMode.Direct), 200));
        });
        Test("Focus change from target never opens an email field", () =>
        {
            foreach (var context in new[] { Email, OtherField })
            {
                var p = new RoutingPolicy(Config);
                p.Evaluate(State(ImeInputMode.Unknown, layout: Swiss), 0);
                Equal(RoutingAction.SwitchToTarget, p.Evaluate(State(ImeInputMode.Direct, context), 100));
            }
        });
        Test("Explicit return to Japanese restores native without contradictory routing", () =>
        {
            var p = new RoutingPolicy(Config);
            p.Evaluate(State(ImeInputMode.Unknown, layout: Swiss), 0);
            Equal(RoutingAction.RestoreNative, p.Evaluate(State(ImeInputMode.Direct), 100));
            Equal(RoutingAction.None, p.Evaluate(State(ImeInputMode.Native), 200));
            Equal(RoutingAction.SwitchToTarget, p.Evaluate(State(ImeInputMode.Direct), 300));
        });
        Test("IME-disabled field falls back after bounded restore attempts", () =>
        {
            var p = new RoutingPolicy(Config);
            p.Evaluate(State(ImeInputMode.Unknown, layout: Swiss), 0);
            Equal(RoutingAction.RestoreNative, p.Evaluate(State(ImeInputMode.Direct), 100));
            Equal(RoutingAction.None, p.Evaluate(State(ImeInputMode.Direct), 200));
            Equal(RoutingAction.RestoreNative, p.Evaluate(State(ImeInputMode.Direct), 600));
            Equal(RoutingAction.SwitchToTarget, p.Evaluate(State(ImeInputMode.Direct), 1100));
            Equal(RoutingAction.SwitchToTarget, p.Evaluate(State(ImeInputMode.Direct), 1600));
        });
        Test("Unknown state during return keeps restoration intent", () =>
        {
            var p = new RoutingPolicy(Config);
            p.Evaluate(State(ImeInputMode.Unknown, layout: Swiss), 0);
            Equal(RoutingAction.None, p.Evaluate(State(ImeInputMode.Unknown), 100));
            Equal(RoutingAction.RestoreNative, p.Evaluate(State(ImeInputMode.Direct), 200));
        });
        Test("Different language is untouched", () => Equal(RoutingAction.None,
            new RoutingPolicy(Config).Evaluate(State(ImeInputMode.Direct, layout: (IntPtr)0x04090409), 0)));
        Test("IMM open and conversion mode are independent", () =>
        {
            Equal(ImeInputMode.Direct, RoutingPolicy.Classify(false, null));
            Equal(ImeInputMode.Direct, RoutingPolicy.Classify(true, 0));
            Equal(ImeInputMode.Direct, RoutingPolicy.Classify(true, 0x10));
            Equal(ImeInputMode.Native, RoutingPolicy.Classify(true, 0x19));
            Equal(ImeInputMode.Native, RoutingPolicy.Classify(true, 0x1B));
            Equal(ImeInputMode.Other, RoutingPolicy.Classify(true, 0x18));
            Equal(ImeInputMode.Unknown, RoutingPolicy.Classify(null, 0));
            Equal(ImeInputMode.Unknown, RoutingPolicy.Classify(true, null));
        });
        Test("Native and full-width Latin are untouched", () =>
        {
            foreach (var mode in new[] { ImeInputMode.Native, ImeInputMode.Other, ImeInputMode.Unknown })
                Equal(RoutingAction.None, new RoutingPolicy(Config).Evaluate(State(mode), 0));
        });
        Test("Email InputScope overrides stale native IMM state", () =>
        {
            var p = new RoutingPolicy(Config);
            p.Evaluate(State(ImeInputMode.Native), 0);
            Equal(RoutingAction.SwitchToTarget, p.Evaluate(State(ImeInputMode.Native) with { RequiresDirectInput = true }, 100));
        });
        Test("Direct field never restores an IME after returning from target", () =>
        {
            var p = new RoutingPolicy(Config);
            p.Evaluate(State(ImeInputMode.Unknown, layout: Swiss), 0);
            Equal(RoutingAction.SwitchToTarget, p.Evaluate(State(ImeInputMode.Native) with { RequiresDirectInput = true }, 100));
        });
        Test("Browser element changes invalidate restoration intent", () =>
        {
            var p = new RoutingPolicy(Config);
            p.Evaluate(State(ImeInputMode.Unknown, Editor with { ElementId = 1 }, Swiss), 0);
            Equal(RoutingAction.SwitchToTarget, p.Evaluate(State(ImeInputMode.Direct, Editor with { ElementId = 2 }), 100));
        });
        Test("Only explicit semantic field metadata routes", () =>
        {
            foreach (var type in new[] { "email", "url", "tel", "number", "password" })
                Equal(FocusedFieldKind.Direct, FocusedInputProbe.ClassifyAttributes($"tag:input;text-input-type:{type};"));
            Equal(FocusedFieldKind.Text, FocusedInputProbe.ClassifyAttributes("text-input-type:text;"));
            Equal(FocusedFieldKind.Unknown, FocusedInputProbe.ClassifyAttributes("label:email;"));
            Equal(FocusedFieldKind.Unknown, FocusedInputProbe.ClassifyAttributes("text-input-type:emailish;"));
            Equal(FocusedFieldKind.Unknown, FocusedInputProbe.ClassifyAttributes(null));
            Equal(FocusedFieldKind.Unknown, FocusedInputProbe.ClassifyAttributes(@"placeholder:some\;text-input-type:email;"));
        });
        Test("All Chinese locales and Korean are selectable", () =>
        {
            foreach (ushort id in new ushort[] { 0x0411, 0x0412, 0x0404, 0x0804, 0x0C04, 0x1004, 0x1404 })
                Equal(true, ImeLanguage.IsSupported(id));
            Equal(false, ImeLanguage.IsSupported(0x0807));
            Equal(false, ImeLanguage.IsSupported(0x0409));
        });
        Test("Chinese and Korean retain width and roman preferences", () =>
        {
            Equal(0x19, ImeLanguage.NativeConversion(0x0411, 0x10));
            Equal(0x19, ImeLanguage.NativeConversion(0x0411, 0x11B));
            Equal(0x01, ImeLanguage.NativeConversion(0x0412, 0x00));
            Equal(0x11, ImeLanguage.NativeConversion(0x0804, 0x10));
            Equal(0x19, ImeLanguage.NativeConversion(0x0404, 0x18));
        });
        Test("CJK sources route direct and restore native", () =>
        {
            foreach (ushort id in new ushort[] { 0x0411, 0x0412, 0x0404, 0x0804 })
            {
                var config = new RoutingConfiguration(new InputProfile { LanguageId = id }, Config.Target);
                var p = new RoutingPolicy(config);
                var direct = State(ImeInputMode.Direct, layout: (IntPtr)id);
                Equal(RoutingAction.SwitchToTarget, p.Evaluate(direct, 0));
                p.Evaluate(State(ImeInputMode.Unknown, layout: Swiss), 100);
                Equal(RoutingAction.RestoreNative, p.Evaluate(direct, 200));
                Equal(RoutingAction.None, p.Evaluate(direct with { Mode = ImeInputMode.Native }, 300));
            }
        });
        Test("All enabled CJK mode routes each configured language", () =>
        {
            var sources = new[] { new InputProfile { LanguageId = 0x0411 },
                new InputProfile { LanguageId = 0x0412 }, new InputProfile { LanguageId = 0x0804 } };
            var config = new RoutingConfiguration(sources[0], Config.Target, sources);
            Equal(true, config.RouteAllSupportedImes);
            foreach (var source in sources)
                Equal(RoutingAction.SwitchToTarget, new RoutingPolicy(config).Evaluate(
                    State(ImeInputMode.Direct, layout: (IntPtr)source.LanguageId), 0));
            Equal(false, config.IsSourceLayout((IntPtr)0x04090409));
            Equal(false, Config.IsSourceLayout((IntPtr)0x04120412));
        });
        Test("Focus event within one HWND invalidates restoration intent", () =>
        {
            var policy = new RoutingPolicy(Config);
            policy.Evaluate(State(ImeInputMode.Unknown, Editor with { FocusVersion = 1 }, Swiss), 0);
            Equal(RoutingAction.SwitchToTarget, policy.Evaluate(State(ImeInputMode.Direct,
                Editor with { FocusVersion = 2 }), 1));
        });
        Test("Fast observations do not flood asynchronous requests", () =>
        {
            var policy = new RoutingPolicy(Config);
            Equal(RoutingAction.SwitchToTarget, policy.Evaluate(State(ImeInputMode.Direct), 0));
            for (int ms = 1; ms < 500; ms++) Equal(RoutingAction.None, policy.Evaluate(State(ImeInputMode.Direct), ms));
            Equal(RoutingAction.SwitchToTarget, policy.Evaluate(State(ImeInputMode.Direct), 500));
        });
        Test("Exclusions match exact executable paths, ignoring case", () =>
        {
            var preferences = new RoutingPreferences { ApplicationRules = [new(@"C:\Games\game.exe", ApplicationRoutingMode.Disabled)] };
            Equal(true, preferences.IsValid());
            Equal(ApplicationRoutingMode.Disabled, preferences.Resolve(@"c:\games\GAME.EXE"));
            Equal(ApplicationRoutingMode.Automatic, preferences.Resolve(@"D:\Other\game.exe"));
            Equal(ApplicationRoutingMode.Disabled, preferences.Resolve(null));
        });
        Test("Allowlist and unknown-process behavior are explicit", () =>
        {
            var preferences = new RoutingPreferences { DefaultMode = ApplicationRoutingMode.Disabled,
                ApplicationRules = [new(@"C:\Tools\editor.exe", ApplicationRoutingMode.Automatic)] };
            Equal(ApplicationRoutingMode.Automatic, preferences.Resolve(@"C:\Tools\editor.exe"));
            Equal(ApplicationRoutingMode.Disabled, preferences.Resolve(@"C:\Other\editor.exe"));
            Equal(ApplicationRoutingMode.Disabled, preferences.Resolve(null));
            Equal(ApplicationRoutingMode.Automatic, new RoutingPreferences().Resolve(null));
        });
        Test("Invalid and ambiguous preferences are rejected", () =>
        {
            foreach (var path in new[] { "game.exe", @"C:game.exe", @"C:\Games\..\game.exe", @"C:\Games\*.exe", "" })
                Equal(false, new RoutingPreferences { ApplicationRules = [new(path, ApplicationRoutingMode.Disabled)] }.IsValid());
            Equal(false, new RoutingPreferences { ApplicationRules = [new(@"C:\a.exe", ApplicationRoutingMode.Automatic), new(@"c:\A.exe", ApplicationRoutingMode.Disabled)] }.IsValid());
            Equal(false, new RoutingPreferences { PauseKey = 0x7B }.IsValid()); // Windows reserves F12
            Equal(false, new RoutingPreferences { PauseKey = 0x78 }.IsValid());
            Equal(false, new RoutingPreferences { DefaultMode = (ApplicationRoutingMode)99 }.IsValid());
        });
        Test("Manual restore uses only the current window's observed layout", () =>
        {
            var manual = new ManualRoutingState();
            var source = State(ImeInputMode.Direct);
            manual.Remember(source, Swiss);
            manual.Hold();
            var target = source with { KeyboardLayout = Swiss };
            Equal(Japanese, manual.RestoreLayout(target, Swiss));
            Equal(true, manual.Holding);
            // A second target shortcut must not replace the checkpoint with target.
            manual.Remember(target, Swiss);
            Equal(Japanese, manual.RestoreLayout(target, Swiss));
            manual.RetainOnlyWindow(Editor.Foreground); // A transient read failure must not discard the checkpoint.
            Equal(Japanese, manual.RestoreLayout(target, Swiss));
            manual.RetainOnlyWindow(Email.Foreground);
            Equal(false, manual.Holding);
            Equal(IntPtr.Zero, manual.RestoreLayout(State(ImeInputMode.Unknown, Email, Swiss), Swiss));
            Equal(false, manual.Holding);
        });
        Test("Manual choice persists across fields in one window and can resume", () =>
        {
            var manual = new ManualRoutingState();
            manual.Observe(State(ImeInputMode.Native));
            manual.Hold();
            manual.Observe(State(ImeInputMode.Direct, OtherField));
            Equal(true, manual.Holding);
            manual.Resume();
            Equal(false, manual.Holding);
        });
        Test("Queued manual switch cannot affect a new focus or layout", () =>
        {
            var initial = State(ImeInputMode.Native, Editor with { FocusVersion = 3 });
            var request = new ManualRoutingRequest(ManualRoutingAction.Target, initial);
            Equal(true, ManualRoutingState.Matches(request, initial));
            Equal(false, ManualRoutingState.Matches(request, initial with { Context = Email }));
            Equal(false, ManualRoutingState.Matches(request, initial with { Context = initial.Context with { FocusVersion = 4 } }));
            Equal(false, ManualRoutingState.Matches(request, initial with { KeyboardLayout = Swiss }));
        });
        Test("Old settings retain routing without enabling shortcuts", () =>
        {
            foreach (int version in new[] { 1, 2 })
            {
                string json = System.Text.Json.JsonSerializer.Serialize(new { Version = version,
                    Source = new { LanguageId = Config.Source.LanguageId, Config.Source.Clsid, Config.Source.ProfileGuid },
                    Target = new { LanguageId = Config.Target.LanguageId, Hkl = Swiss.ToInt64() } });
                var restored = SettingsService.FromJson(json, [Config.Source], [Config.Target])!;
                Equal(false, restored.Preferences.HotkeysEnabled);
                Equal(ApplicationRoutingMode.Automatic, restored.Preferences.DefaultMode);
                Equal(0, restored.Preferences.ApplicationRules.Length);
                Equal(Swiss, restored.Target.Hkl);
            }
        });
        Test("New preferences survive serialization and unavailable profiles fail closed", () =>
        {
            var configuration = new RoutingConfiguration(Config.Source, Config.Target, preferences: new RoutingPreferences {
                HotkeysEnabled = true, PauseKey = 0x75, DefaultMode = ApplicationRoutingMode.Disabled,
                ApplicationRules = [new(@"C:\Apps\editor.exe", ApplicationRoutingMode.Automatic)] });
            string json = System.Text.Json.JsonSerializer.Serialize(SettingsService.ToSettings(configuration));
            var restored = SettingsService.FromJson(json, [Config.Source], [Config.Target])!;
            Equal(true, restored.Preferences.HotkeysEnabled);
            Equal(0x75, restored.Preferences.PauseKey);
            Equal(ApplicationRoutingMode.Automatic, restored.Preferences.Resolve(@"C:\Apps\editor.exe"));
            Equal(ApplicationRoutingMode.Disabled, restored.Preferences.Resolve(@"C:\Games\game.exe"));
            Equal(null, SettingsService.FromJson(json, [], [Config.Target]));
            Equal(null, SettingsService.FromJson(json, [Config.Source], []));
            Equal(null, SettingsService.FromJson(json.Replace("\"Version\":3", "\"Version\":99"), [Config.Source], [Config.Target]));
            Equal(null, SettingsService.FromJson(json.Replace("\"ApplicationRules\":[", "\"ApplicationRules\":[null,"), [Config.Source], [Config.Target]));
            Equal(null, SettingsService.FromJson("{broken", [Config.Source], [Config.Target]));
        });
        Test("UI commands wake monitoring without waiting for the fallback", () =>
        {
            using var session = new RoutingSession();
            session.SetPaused(true);
            Equal(true, session.Paused);
            Equal(true, session.Wake.WaitOne(0));
            var command = new ManualRoutingRequest(ManualRoutingAction.Target, State(ImeInputMode.Native));
            session.Request(command);
            Equal(true, session.Wake.WaitOne(0));
            Equal(true, session.TryTake(out var received));
            Equal(command, received);
            Equal(false, session.TryTake(out _));
        });
        Console.WriteLine($"{passed} regression scenarios passed.");
    }
}
