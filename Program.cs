using System;
using System.Windows.Forms;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--diagnose")
        {
            var configuration = SettingsService.Load();
            if (configuration == null || args.Length != 3
                || !int.TryParse(args[1], out int seconds) || seconds < 1 || seconds > 3600)
            {
                Console.Error.WriteLine("Save settings first. Usage: --diagnose <seconds 1..3600> <output-path>");
                Environment.ExitCode = 1;
                return;
            }
            using var output = new System.IO.StreamWriter(args[2]);
            RoutingMonitor.Diagnose(configuration, seconds, output);
            return;
        }

        if (
            args.Length > 0
            && args[0] == "--list-profiles"
        )
        {
            TsfProfileEnumerator.PrintAllProfiles();
            return;
        }

        if (
            args.Length > 0
            && args[0] == "--active-profile"
        )
        {
            TsfProfileEnumerator.PrintActiveProfile();
            return;
        }

        if (
            args.Length > 0
            && args[0] == "--list-candidates"
        )
        {
            TsfProfileEnumerator.PrintSelectableProfiles();
            return;
        }

        if (
            args.Length > 0
            && args[0] == "--selection-test"
        )
        {
            TsfProfileEnumerator.PrintAutomaticSelection();
            return;
        }

        if (
            args.Length > 0
            && args[0] == "--routing-match-test"
        )
        {
            TsfProfileEnumerator.PrintRoutingMatchTest();
            return;
        }

        if (
            args.Length > 0
            && args[0] == "--configure"
        )
        {
            RoutingConfiguration? selected =
                ConsoleProfileSelector.Select();

            if (selected == null)
            {
                return;
            }

            SettingsService.Save(selected);

            Console.WriteLine(
                "Settings saved:"
            );

            Console.WriteLine(
                SettingsService.GetSettingsPath()
            );

            return;
        }

        if (
            args.Length > 0
            && args[0] == "--load-settings-test"
        )
        {
            RoutingConfiguration? loaded =
                SettingsService.Load();

            if (loaded == null)
            {
                Console.WriteLine(
                    "Saved routing configuration could not be loaded."
                );

                return;
            }

            Console.WriteLine(
                $"Source: {loaded.Source.DisplayName}"
            );

            Console.WriteLine(
                $"Target: {loaded.Target.DisplayName}"
            );

            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var settingsRequest = new System.Threading.EventWaitHandle(false,
            System.Threading.EventResetMode.AutoReset, @"Local\ImeLayoutRouter.ShowSettings");
        using var instance = new System.Threading.Mutex(true, @"Local\ImeLayoutRouter.Instance", out bool firstInstance);
        if (!firstInstance)
        {
            settingsRequest.Set();
            return;
        }

        bool showSettingsOnStartup =
            args.Length > 0
            &&
            args[0] == "--first-run";

        Application.Run(
            new TrayApplicationContext(
                showSettingsOnStartup,
                settingsRequest
            )
        );
    }
}
