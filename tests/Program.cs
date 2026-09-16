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
        Console.WriteLine($"{passed} regression scenarios passed.");
    }
}
