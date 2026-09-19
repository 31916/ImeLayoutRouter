using System.Globalization;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("ja-JP");
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var request = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ImeLayoutRouter.ShowSettings");
        using var instance = new Mutex(true, @"Local\ImeLayoutRouter.Instance", out bool first);
        if (!first) { request.Set(); return; }
        Application.Run(new TrayApplicationContext(args.Contains("--first-run"), request));
    }
}
