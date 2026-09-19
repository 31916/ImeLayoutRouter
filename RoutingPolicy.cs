using System;

enum ImeInputMode { Unknown, Direct, Native, Other }
enum RoutingAction { None, SwitchToTarget, RestoreNative }

readonly record struct InputContext(IntPtr Foreground, IntPtr Focus, uint ThreadId, int ElementId = 0,
    long FocusVersion = 0);
readonly record struct InputSnapshot(InputContext Context, IntPtr KeyboardLayout, ImeInputMode Mode,
    bool RequiresDirectInput = false);

// Pure decisions: replayable without changing Windows input state.
sealed class RoutingPolicy
{
    private readonly RoutingConfiguration configuration;
    private InputSnapshot? previous;
    private long nextRequestAt;
    private long? restoreUntil;
    private const int RetryMilliseconds = 500;
    private const int RestoreGraceMilliseconds = 1000;

    public RoutingPolicy(RoutingConfiguration configuration) => this.configuration = configuration;

    public RoutingAction Evaluate(InputSnapshot current, long now)
    {
        bool sameContext = previous?.Context == current.Context;
        bool returningFromTarget = sameContext
            && previous?.KeyboardLayout == configuration.Target.Hkl
            && current.KeyboardLayout != configuration.Target.Hkl;
        if (!sameContext)
        {
            nextRequestAt = 0;
            restoreUntil = null;
        }
        previous = current;
        bool sourceIsActive = configuration.IsSourceLayout(current.KeyboardLayout);
        if (!sourceIsActive || (!current.RequiresDirectInput && current.Mode is ImeInputMode.Native or ImeInputMode.Other))
        {
            restoreUntil = null;
            nextRequestAt = 0;
            return RoutingAction.None;
        }
        // InputScope can force A inside the IME while IMM/TSF still reports native.
        // A known email/URL/password field takes priority over restoration.
        if (current.RequiresDirectInput)
        {
            restoreUntil = null;
            if (now < nextRequestAt) return RoutingAction.None;
            nextRequestAt = now + RetryMilliseconds;
            return RoutingAction.SwitchToTarget;
        }
        // Changing apps/controls is not evidence of explicitly returning to the IME.
        if (returningFromTarget)
        {
            restoreUntil = now + RestoreGraceMilliseconds;
            nextRequestAt = 0;
        }
        // A failed/timed-out read must never be interpreted as direct input.
        if (current.Mode == ImeInputMode.Unknown || now < nextRequestAt)
            return RoutingAction.None;

        nextRequestAt = now + RetryMilliseconds;
        if (restoreUntil.HasValue && now < restoreUntil.Value)
            return RoutingAction.RestoreNative;
        restoreUntil = null;
        // Reconcile persistent A too: transitions and asynchronous requests can be missed.
        return RoutingAction.SwitchToTarget;
    }

    public static ImeInputMode Classify(bool? open, int? conversion)
    {
        if (open == false) return ImeInputMode.Direct;
        if (open != true || !conversion.HasValue) return ImeInputMode.Unknown;
        if ((conversion.Value & 0x0001) != 0) return ImeInputMode.Native;
        // Full-width Latin (Ａ) is intentional conversion, not direct A.
        return (conversion.Value & 0x0008) != 0 ? ImeInputMode.Other : ImeInputMode.Direct;
    }
}
