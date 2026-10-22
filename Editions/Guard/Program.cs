using System.Windows.Forms;

static class Program
{
    [STAThread] static void Main(string[] args)
    {
        if (CompositionGuardClient.TryRunWorker(args)) return;
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // V3 can run alongside the existing V1/V2 routing application.
        using var request = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ImeLayoutRouter.V3.ShowSettings");
        using var instance = new Mutex(true, @"Local\ImeLayoutRouter.V3.Instance", out bool first);
        if (!first) { request.Set(); return; }
        Application.Run(new GuardTrayContext(args.Contains("--first-run"), request));
    }
}
