using System.Windows.Forms;

static class Program
{
    [STAThread] static void Main(string[] args)
    {
        if (CompositionGuardClient.TryRunWorker(args)) return;
        if (args.Length == 1 && args[0] == "--shutdown")
        {
            if (EventWaitHandle.TryOpenExisting(@"Local\ImeLayoutRouter.V3.Stop", out var stop))
                using (stop) stop.Set();
            using var running = new Mutex(false, @"Local\ImeLayoutRouter.V3.Instance");
            try
            {
                if (!running.WaitOne(5000)) { Environment.ExitCode = 3; return; }
            }
            catch (AbandonedMutexException) { }
            running.ReleaseMutex();
            CompositionGuardClient.TryRunWorker(["--composition-guard-recover"]);
            return;
        }
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // V3 can run alongside the existing V1/V2 routing application.
        using var request = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ImeLayoutRouter.V3.ShowSettings");
        using var stopRequest = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ImeLayoutRouter.V3.Stop");
        using var instance = new Mutex(true, @"Local\ImeLayoutRouter.V3.Instance", out bool first);
        if (!first) { request.Set(); return; }
        Application.Run(new GuardTrayContext(args.Contains("--first-run"), request, stopRequest));
    }
}
