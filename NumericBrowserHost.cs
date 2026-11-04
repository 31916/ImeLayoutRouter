using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

// The same installed EXE can run as a Chrome/Edge native messaging host.
// Its browser messages contain only a challenge ID and a field classification.
static class NumericBrowserHost
{
    private const string Origin = "chrome-extension://feaapoghfchepkmogdmdkcedfimopbpl/";

    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || !args[0].StartsWith("chrome-extension://", StringComparison.Ordinal)) return false;
        NumericCiTrace.Mark("host-start");
        exitCode = 1;
        if (args[0] != Origin) return true;
        try
        {
            if (!NumericPeer.Installed(Environment.ProcessPath)) { NumericCiTrace.Mark("host-marker-missing"); return true; }
            if (NumericPeer.BrowserAncestor(Environment.ProcessId) is not { } browser) { NumericCiTrace.Mark("host-browser-parent-rejected"); return true; }
            NumericCiTrace.Mark("host-browser-parent-confirmed");
            nint parentWindow = 0;
            foreach (string argument in args.Skip(1))
            {
                const string prefix = "--parent-window=";
                if (!argument.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (!ulong.TryParse(argument.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var handle))
                    return true;
                parentWindow = unchecked((nint)handle);
            }
            new Session(browser, parentWindow).RunAsync().GetAwaiter().GetResult();
            exitCode = 0;
        }
        catch (Exception ex) when (NumericPeer.ExpectedFailure(ex)) { NumericCiTrace.Mark("host-stop", ex); }
        return true;
    }

    private sealed class Session(NumericPeer.ProcessIdentity browser, nint parentWindow)
    {
        private readonly object gate = new();
        private readonly SemaphoreSlim send = new(1);
        private TaskCompletionSource<bool>? waiting;
        private long serial;

        public async Task RunAsync()
        {
            using var stop = new CancellationTokenSource();
            using var input = Console.OpenStandardInput();
            using var output = Console.OpenStandardOutput();
            using var first = new NativeChannel();
            using var second = new NativeChannel();
            Task v1 = ConnectAsync("V1", first, output, stop.Token);
            Task v2 = ConnectAsync("V2", second, output, stop.Token);
            try
            {
                var header = new byte[4];
                while (await input.ReadAsync(header.AsMemory(0, 1)) != 0)
                {
                    await input.ReadExactlyAsync(header.AsMemory(1));
                    int length = BinaryPrimitives.ReadInt32LittleEndian(header);
                    if (length <= 0 || length > 256) break;
                    var body = new byte[length];
                    await input.ReadExactlyAsync(body);
                    using var json = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 2 });
                    var root = json.RootElement;
                    if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2
                        || !root.TryGetProperty("id", out var id) || !id.TryGetInt64(out var replyId)
                        || !root.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String) break;
                    string? classification = kind.GetString();
                    if (classification is not ("numeric" or "unknown")) break;
                    NumericCiTrace.Mark(classification == "numeric" ? "browser-replied-numeric" : "browser-replied-unknown");
                    lock (gate)
                        if (waiting != null && replyId == serial) waiting.TrySetResult(classification == "numeric");
                }
            }
            finally
            {
                stop.Cancel();
                first.Dispose();
                second.Dispose();
                await Task.WhenAll(v1, v2);
            }
        }

        private async Task ConnectAsync(string edition, NativeChannel channel, Stream output, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeClientStream(".", NumericPeer.PipeName(edition), PipeDirection.InOut,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, TokenImpersonationLevel.Identification);
                    channel.Set(pipe);
                    await pipe.ConnectAsync(1000, token);
                    if (NumericPeer.TrustedServer(pipe, edition))
                    {
                        NumericCiTrace.Mark("host-connected-" + edition);
                        await ServeAsync(pipe, output, token);
                    }
                    else NumericCiTrace.Mark("host-rejected-router-" + edition);
                }
                catch (Exception ex) when (NumericPeer.ExpectedFailure(ex)) { NumericCiTrace.Mark("host-pipe-" + edition, ex); }
                if (!token.IsCancellationRequested)
                {
                    try { await Task.Delay(1500, token); }
                    catch (OperationCanceledException) { return; }
                }
            }
        }

        private async Task ServeAsync(NamedPipeClientStream pipe, Stream output, CancellationToken token)
        {
            var request = new byte[8];
            var answer = new byte[1];
            while (!token.IsCancellationRequested)
            {
                await pipe.ReadExactlyAsync(request, token);
                var window = (nint)BinaryPrimitives.ReadInt64LittleEndian(request);
                bool current = (parentWindow == 0 || NumericPeer.TopLevel(parentWindow) == window) && NumericPeer.IsForeground(window, browser);
                NumericCiTrace.Mark(current ? "host-window-matches" : "host-window-rejected");
                bool numeric = current && await AskBrowserAsync(window, output, token) && NumericPeer.IsForeground(window, browser);
                answer[0] = numeric ? (byte)1 : (byte)0;
                await pipe.WriteAsync(answer, token);
            }
        }

        private async Task<bool> AskBrowserAsync(nint window, Stream output, CancellationToken token)
        {
            await send.WaitAsync(token);
            try
            {
                if (!NumericPeer.IsForeground(window, browser)) return false;
                var reply = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                long id;
                lock (gate) { id = ++serial; waiting = reply; }
                byte[] body = JsonSerializer.SerializeToUtf8Bytes(new { op = "classify", id });
                var packet = new byte[4 + body.Length];
                BinaryPrimitives.WriteInt32LittleEndian(packet, body.Length);
                body.CopyTo(packet, 4);
                await output.WriteAsync(packet, token);
                await output.FlushAsync(token);
                NumericCiTrace.Mark("host-sent-challenge");
                try { return await reply.Task.WaitAsync(TimeSpan.FromMilliseconds(240), token); }
                catch (TimeoutException) { NumericCiTrace.Mark("host-challenge-timeout"); return false; }
            }
            finally
            {
                lock (gate) waiting = null;
                send.Release();
            }
        }
    }

    // Close pending pipe reads/connects promptly when the browser closes stdin.
    private sealed class NativeChannel : IDisposable
    {
        private NamedPipeClientStream? stream;
        private bool closed;
        public void Set(NamedPipeClientStream current)
        {
            lock (this)
            {
                if (closed) throw new ObjectDisposedException(nameof(NativeChannel));
                stream = current;
            }
        }
        public void Dispose() { lock (this) { closed = true; stream?.Dispose(); } }
    }
}

// Per-process, opt-in diagnostics for the repository's disposable CI runner.
// Only distinct structural milestones and error codes are recorded.
static class NumericCiTrace
{
    private static readonly bool Enabled =
        Environment.GetEnvironmentVariable("IME_NUMERIC_CI_TRACE") == "1" &&
        Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" &&
        Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") == "github-hosted" &&
        Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") == "31916/ImeLayoutRouter";
    private static readonly HashSet<string> Seen = new(StringComparer.Ordinal);
    internal static void Mark(string stage, Exception? error = null)
    {
        if (!Enabled || Environment.GetEnvironmentVariable("RUNNER_TEMP") is not string directory) return;
        if (error != null) stage += $" {error.GetType().Name} 0x{error.HResult:X8}";
        lock (Seen)
        {
            if (Seen.Count >= 64 || !Seen.Add(stage)) return;
            try
            {
                File.AppendAllText(Path.Combine(directory, $"imelayout-numeric-ci-{Environment.ProcessId}.txt"),
                    $"{DateTime.UtcNow:HH:mm:ss.fff} {stage}{Environment.NewLine}");
            }
            catch (Exception ex) when (NumericPeer.ExpectedFailure(ex)) { }
        }
    }
}

static class NumericPeer
{
    internal sealed record ProcessIdentity(int Pid, long CreationTime, string Path);
    private static readonly Lazy<(int Session, string? Sid)> UserSession = new(() =>
    {
        using var process = Process.GetCurrentProcess();
        using var identity = WindowsIdentity.GetCurrent();
        return (process.SessionId, identity.User?.Value);
    });
    internal static string PipeName(string edition) =>
        $"ImeLayoutRouter.Numeric.{UserSession.Value.Sid}.{UserSession.Value.Session}.{edition}";

    internal static bool ExpectedFailure(Exception ex) => ex is IOException or OperationCanceledException
        or UnauthorizedAccessException or System.Security.SecurityException or TimeoutException
        or InvalidOperationException or ArgumentException or Win32Exception or JsonException;

    internal static bool Installed(string? path, string? edition = null)
    {
        if (string.IsNullOrEmpty(path)) return false;
        try
        {
            using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using var key = user.OpenSubKey(@"Software\31916\IMELayoutRouter\NumericHost");
            foreach (string id in edition == null ? new[] { "V1", "V2" } : new[] { edition })
            {
                if (key?.GetValue(id) is not string manifest || !Path.IsPathFullyQualified(manifest)
                    || !Path.GetFileName(manifest).Equals("numeric-host.json", StringComparison.OrdinalIgnoreCase)) continue;
                string expected = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifest))!, "ImeLayoutRouter.exe");
                if (expected.Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase) && File.Exists(manifest)) return true;
            }
        }
        catch (Exception ex) when (ExpectedFailure(ex)) { }
        return false;
    }

    internal static bool TrustedServer(NamedPipeClientStream pipe, string edition) =>
        GetNamedPipeServerProcessId(pipe.SafePipeHandle, out int pid) && Installed(Identity(pid)?.Path, edition);

    internal static ProcessIdentity? TrustedClient(NamedPipeServerStream pipe)
    {
        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out int pid) || !Installed(Identity(pid)?.Path)) return null;
        return BrowserAncestor(pid);
    }

    internal static ProcessIdentity? BrowserAncestor(int hostPid)
    {
        // Some Chromium versions launch the host via cmd.exe. Never infer
        // the browser from its name alone: the same process must own the
        // actual foreground HWND at both ends of each classification request.
        long childTime = long.MaxValue;
        int pid = hostPid;
        for (int i = 0; i < 3 && pid > 0; i++)
        {
            using var handle = OpenProcess(0x1000, false, pid);
            var process = ReadProcess(handle, pid);
            if (process == null || process.CreationTime > childTime) break;
            string name = Path.GetFileName(process.Path);
            if (i != 0 && (name.Equals("chrome.exe", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("msedge.exe", StringComparison.OrdinalIgnoreCase)))
                return process; // The launching browser, never an older same-name relaunch wrapper.
            if (i != 0 && !name.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase)) break;
            childTime = process.CreationTime;
            if (NtQueryInformationProcess(handle, 0, out var info, Marshal.SizeOf<ProcessBasicInformation>(), out _) < 0) break;
            int parent = (int)info.Parent;
            if (parent == pid) break;
            pid = parent;
        }
        return null;
    }

    internal static int WindowProcess(nint hwnd)
    {
        GetWindowThreadProcessId(hwnd, out int pid);
        return pid;
    }
    internal static bool IsForeground(nint hwnd, ProcessIdentity browser) =>
        hwnd != 0 && GetForegroundWindow() == hwnd && WindowProcess(hwnd) == browser.Pid &&
        Identity(browser.Pid) == browser;
    internal static nint TopLevel(nint window) => GetAncestor(window, 2);

    private static ProcessIdentity? Identity(int pid)
    {
        using var handle = OpenProcess(0x1000, false, pid);
        return ReadProcess(handle, pid);
    }
    private static ProcessIdentity? ReadProcess(SafeProcessHandle handle, int pid)
    {
        if (handle.IsInvalid || !ProcessIdToSessionId(pid, out int session) || session != UserSession.Value.Session ||
            !GetProcessTimes(handle, out long created, out _, out _, out _)) return null;
        var path = new StringBuilder(1024);
        int size = path.Capacity;
        return QueryFullProcessImageNameW(handle, 0, path, ref size) ? new(pid, created, path.ToString()) : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        internal nint ExitStatus, Peb, Affinity, BasePriority, UniqueProcessId, Parent;
    }
    [DllImport("kernel32.dll")] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out int pid);
    [DllImport("kernel32.dll")] private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out int pid);
    [DllImport("kernel32.dll")] private static extern SafeProcessHandle OpenProcess(int access, bool inherit, int pid);
    [DllImport("kernel32.dll")] private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exit, out long kernel, out long user);
    [DllImport("kernel32.dll")] private static extern bool ProcessIdToSessionId(int pid, out int session);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool QueryFullProcessImageNameW(SafeProcessHandle process, int flags, StringBuilder name, ref int size);
    [DllImport("ntdll.dll")] private static extern int NtQueryInformationProcess(SafeProcessHandle process, int infoClass, out ProcessBasicInformation information, int size, out int used);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out int pid);
}
