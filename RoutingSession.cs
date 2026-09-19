using System.Collections.Concurrent;
using System.Threading;

enum ManualRoutingAction { Target, Restore }
readonly record struct ManualRoutingRequest(ManualRoutingAction Action, InputSnapshot Captured);

sealed record RoutingStatus(InputSnapshot? Snapshot, string Reason, bool Paused = false)
{
    public string LayoutText => Snapshot is { } s
        ? $"HKL {s.KeyboardLayout.ToInt64():X8} / {s.Mode}" : UiText.T("状態を取得できません", "State unavailable");
}

// UI and monitor exchange immutable observations/commands, never controls or text.
sealed class RoutingSession : IDisposable
{
    private readonly ConcurrentQueue<ManualRoutingRequest> requests = new();
    private RoutingStatus status = new(null, UiText.T("監視を開始しています", "Starting monitor"));
    private int paused;
    public AutoResetEvent Wake { get; } = new(false);
    public RoutingStatus Status => Volatile.Read(ref status);
    public bool Paused => Volatile.Read(ref paused) != 0;
    public long FocusVersion = 0;
    public void SetPaused(bool value) { Volatile.Write(ref paused, value ? 1 : 0); Wake.Set(); }
    public void Publish(RoutingStatus value) => Volatile.Write(ref status, value);
    public void Request(ManualRoutingRequest request) { requests.Enqueue(request); Wake.Set(); }
    public bool TryTake(out ManualRoutingRequest request) => requests.TryDequeue(out request);
    public void Dispose() => Wake.Dispose();
}

// Manual choice lasts until leaving this window, or explicitly resuming routing.
// The checkpoint is an actual observed HKL, not an inferred TSF profile.
sealed class ManualRoutingState
{
    private (IntPtr Window, uint Thread)? window;
    private IntPtr previousLayout;
    public bool Holding { get; private set; }
    public void Observe(InputSnapshot snapshot)
    {
        var key = (snapshot.Context.Foreground, snapshot.Context.ThreadId);
        if (window != key) { window = key; previousLayout = IntPtr.Zero; Holding = false; }
    }
    public void Remember(InputSnapshot snapshot, IntPtr target)
    {
        Observe(snapshot);
        if (snapshot.KeyboardLayout != target) previousLayout = snapshot.KeyboardLayout;
    }
    public IntPtr RestoreLayout(InputSnapshot snapshot, IntPtr target)
    {
        Observe(snapshot);
        return snapshot.KeyboardLayout == target ? previousLayout : IntPtr.Zero;
    }
    public void Hold() => Holding = true;
    public void Resume() => Holding = false;
    public void Reset() { window = null; previousLayout = IntPtr.Zero; Holding = false; }
    public void RetainOnlyWindow(IntPtr foreground)
    {
        if (window is { } previous && previous.Window != foreground) Reset();
    }
    public static bool Matches(ManualRoutingRequest request, InputSnapshot current) =>
        request.Captured.Context.Foreground == current.Context.Foreground
        && request.Captured.Context.Focus == current.Context.Focus
        && request.Captured.Context.ThreadId == current.Context.ThreadId
        && request.Captured.Context.FocusVersion == current.Context.FocusVersion
        && request.Captured.KeyboardLayout == current.KeyboardLayout;
}
