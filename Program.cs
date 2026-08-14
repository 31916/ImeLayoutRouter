using System;
using System.Windows.Forms;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
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

        bool showSettingsOnStartup =
            args.Length > 0
            &&
            args[0] == "--first-run";

        Application.Run(
            new TrayApplicationContext(
                showSettingsOnStartup
            )
        );
    }
}