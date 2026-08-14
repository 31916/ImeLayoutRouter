using System;

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

            Console.WriteLine();

            Console.WriteLine(
                "=== Selected Routing ==="
            );

            Console.WriteLine(
                $"Source: {selected.Source.DisplayName}"
            );

            Console.WriteLine(
                $"Target: {selected.Target.DisplayName}"
            );

            SettingsService.Save(
                selected
            );

            Console.WriteLine();

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
                "=== Loaded Routing ==="
            );

            Console.WriteLine(
                $"Source: {loaded.Source.DisplayName}"
            );

            Console.WriteLine(
                $"Target: {loaded.Target.DisplayName}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Source Language ID: 0x{loaded.Source.LanguageId:X4}"
            );

            Console.WriteLine(
                $"Target Language ID: 0x{loaded.Target.LanguageId:X4}"
            );

            return;
        }

        RoutingConfiguration? configuration =
            SettingsService.Load();

        if (configuration == null)
        {
            Console.WriteLine(
                "No valid saved routing configuration was found."
            );

            Console.WriteLine(
                "Please select Source and Target."
            );

            Console.WriteLine();

            configuration =
                ConsoleProfileSelector.Select();

            if (configuration == null)
            {
                return;
            }

            SettingsService.Save(
                configuration
            );

            Console.WriteLine();

            Console.WriteLine(
                "Routing configuration saved."
            );

            Console.WriteLine();
        }

        RoutingMonitor.Run(
            configuration
        );
    }
}