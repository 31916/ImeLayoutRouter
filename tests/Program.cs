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
        Console.WriteLine($"{passed} regression scenarios passed.");
    }
}
