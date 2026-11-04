using System.Buffers.Binary;
using System.IO.Pipes;

// One read-only receiver per running, opted-in edition. An idle monitor has
// no browser polling task. The presence of an extension is not itself a hint.
sealed class NumericBrowserBridge : IDisposable
{
#if SIMPLE_EDITION
    private const string Edition = "V1";
#else
    private const string Edition = "V2";
#endif
    private const int FreshMilliseconds = 150;
    private sealed class Client(NamedPipeServerStream pipe, NumericPeer.ProcessIdentity browser)
    {
        public readonly NamedPipeServerStream Pipe = pipe;
        public readonly NumericPeer.ProcessIdentity Browser = browser;
        public InputContext? Positive;
        public InputContext? LastQuery;
        public long ObservedAt, NextQueryAt;
        public bool Busy;
        public TaskCompletionSource<byte>? Pending;
    }

    private readonly object gate = new();
    private readonly List<Client> clients = [];
    private readonly Action? changed;
    private readonly CancellationTokenSource stopping = new();
    private bool disposed;
    private NumericBrowserBridge(Action? changed)
    {
        this.changed = changed;
        _ = Task.Run(ListenAsync);
    }

    internal static NumericBrowserBridge? CreateIfInstalled(Action? changed = null) =>
        NumericPeer.Installed(Environment.ProcessPath, Edition) ? new(changed) : null;

    private async Task ListenAsync()
    {
        CancellationToken token = stopping.Token;
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(NumericPeer.PipeName(Edition), PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(token);
                var browser = NumericPeer.TrustedClient(pipe);
                lock (gate)
                {
                    if (!disposed && browser != null)
                    {
                        var client = new Client(pipe, browser);
                        clients.Add(client);
                        pipe = null; // The client now owns this stream.
                        _ = Task.Run(() => ListenForRepliesAsync(client));
                        changed?.Invoke();
                    }
                }
            }
            catch (Exception ex) when (NumericPeer.ExpectedFailure(ex))
            {
                if (!token.IsCancellationRequested)
                    try { await Task.Delay(1000, token); } catch (OperationCanceledException) { return; }
            }
            finally { pipe?.Dispose(); }
        }
    }

    private async Task ListenForRepliesAsync(Client client)
    {
        try
        {
            byte[] message = new byte[1];
            while (await client.Pipe.ReadAsync(message, stopping.Token) != 0)
            {
                lock (gate)
                {
                    if (disposed || message[0] > 1 || client.Pending == null ||
                        !client.Pending.TrySetResult(message[0])) break;
                }
            }
        }
        catch (Exception ex) when (NumericPeer.ExpectedFailure(ex)) { }
        finally { Disconnect(client); }
    }

    private void Disconnect(Client client)
    {
        lock (gate)
        {
            bool removed = clients.Remove(client);
            client.Pending?.TrySetCanceled();
            if (removed && !disposed && client.Positive != null) changed?.Invoke();
        }
        client.Pipe.Dispose();
    }

    // Called only for a focused, writable browser text field. A result is
    // bound to the exact HWND, focus thread, element and focus generation.
    internal bool Read(InputContext context)
    {
        lock (gate)
        {
            if (disposed || clients.Count == 0 || context.Foreground == 0 ||
                NumericPeer.GetForegroundWindow() != context.Foreground) return false;
            int pid = NumericPeer.WindowProcess(context.Foreground);
            long now = Environment.TickCount64;
            bool numeric = false;
            foreach (var client in clients)
            {
                if (client.Browser.Pid != pid) continue;
                numeric |= client.Positive == context && now - client.ObservedAt <= FreshMilliseconds;
                if (client.Busy || (client.LastQuery == context && now < client.NextQueryAt)) continue;
                client.Busy = true;
                client.LastQuery = context;
                client.NextQueryAt = now + 90;
                _ = Task.Run(() => QueryAsync(client, context, now));
            }
            return numeric;
        }
    }

    internal bool Matches(InputContext context)
    {
        lock (gate)
        {
            if (disposed || !NumericPeer.Installed(Environment.ProcessPath, Edition)) return false;
            long now = Environment.TickCount64;
            try
            {
                return clients.Any(c => c.Positive == context && now - c.ObservedAt <= FreshMilliseconds
                    && NumericPeer.TrustedClient(c.Pipe) == c.Browser && NumericPeer.IsForeground(context.Foreground, c.Browser));
            }
            catch (Exception ex) when (NumericPeer.ExpectedFailure(ex)) { return false; }
        }
    }

    private async Task QueryAsync(Client client, InputContext context, long started)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
            timeout.CancelAfter(600);
            var pending = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate)
            {
                if (disposed || !clients.Contains(client)) return;
                client.Pending = pending;
            }
            var frame = new byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(frame, context.Foreground);
            await client.Pipe.WriteAsync(frame, timeout.Token);
            byte reply = await pending.Task.WaitAsync(timeout.Token);
            long now = Environment.TickCount64;
            bool numeric = reply == 1 && now - started <= 350 && NumericPeer.IsForeground(context.Foreground, client.Browser);
            lock (gate)
            {
                if (disposed) return;
                var before = client.Positive;
                client.Positive = numeric ? context : null;
                client.ObservedAt = now;
                // Stable ordinary text does not need a browser query on every
                // monitor tick. A new focus generation or element bypasses this delay.
                if (!numeric) client.NextQueryAt = now + 750;
                client.Busy = false;
                client.Pending = null;
                if (client.Positive != before) changed?.Invoke();
            }
        }
        catch (Exception ex) when (NumericPeer.ExpectedFailure(ex))
        {
            Disconnect(client);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            stopping.Cancel();
            foreach (var client in clients) client.Pipe.Dispose();
            clients.Clear();
        }
    }
}
