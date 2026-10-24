using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

// Test-only ACPI sleep handshake. The external hypervisor must independently
// observe S3 and wake this disposable guest; simulated messages never pass.
internal static class VmLifecycle
{
    private static readonly List<int> powerEvents = [];
    internal static void Observe(uint message, nint parameter)
    {
        if (message == 0x218 && (parameter == 4 || parameter == 7 || parameter == 18))
            powerEvents.Add((int)parameter);
    }
    internal static void Suspend(Action<int> pump, int cycle)
    {
        RequireDisposable();
        powerEvents.Clear();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.Add("Authorization", Environment.GetEnvironmentVariable("GUARD_TEST_VM_TOKEN"));
        using var body = new StringContent(JsonSerializer.Serialize(new { phase = $"suspend-ready-{cycle}" }), Encoding.UTF8, "application/json");
        http.PostAsync("http://10.0.2.2:8000/status", body).GetAwaiter().GetResult().EnsureSuccessStatusCode();
        SuspendCore(pump);
    }
    private static void RequireDisposable()
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted"
            || Environment.MachineName != "V3-TEST"
            || Environment.GetEnvironmentVariable("GUARD_TEST_VM_ENDPOINT") != "http://10.0.2.2:8000")
            throw new InvalidOperationException("Sleep is restricted to the disposable nested V3 VM");
    }
    private static void SuspendCore(Action<int> pump)
    {
        var request = Task.Run(() => {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x28, out nint token))
                throw new InvalidOperationException("Sleep privilege token unavailable");
            try
            {
                if (!LookupPrivilegeValueW(null, "SeShutdownPrivilege", out var luid))
                    throw new InvalidOperationException("Sleep privilege identifier unavailable");
                var privileges = new Privileges { Count = 1, Id = luid, Attributes = 2 };
                if (!AdjustTokenPrivileges(token, false, ref privileges, 0, 0, 0) || Marshal.GetLastWin32Error() != 0)
                    throw new InvalidOperationException("Sleep privilege unavailable in the disposable guest");
                if (!SetSuspendState(false, false, false))
                    throw new InvalidOperationException("Actual guest suspend failed: " + Marshal.GetLastWin32Error());
            }
            finally { CloseHandle(token); }
        });
        long deadline = Environment.TickCount64 + 90000;
        while (!request.IsCompleted && Environment.TickCount64 < deadline) pump(50);
        if (!request.IsCompleted) throw new TimeoutException("Disposable guest did not resume");
        request.GetAwaiter().GetResult();
        pump(1000);
        Console.WriteLine("Actual WM_POWERBROADCAST events: " + string.Join(",", powerEvents));
        if (!powerEvents.Contains(4) || !(powerEvents.Contains(7) || powerEvents.Contains(18)))
            throw new InvalidOperationException("Actual suspend/resume notifications were not observed");
    }
    [StructLayout(LayoutKind.Sequential)] private struct Luid { internal uint Low; internal int High; }
    [StructLayout(LayoutKind.Sequential)] private struct Privileges { internal uint Count; internal Luid Id; internal uint Attributes; }
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool LookupPrivilegeValueW(string? system, string name, out Luid id);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool AdjustTokenPrivileges(nint token, bool disable, ref Privileges state, uint size, nint previous, nint returned);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)] private static extern bool SetSuspendState([MarshalAs(UnmanagedType.U1)] bool hibernate, [MarshalAs(UnmanagedType.U1)] bool force, [MarshalAs(UnmanagedType.U1)] bool disableWake);
}
