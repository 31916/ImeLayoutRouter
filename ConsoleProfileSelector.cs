using System;
using System.IO;

static class ConsoleProfileSelector
{
    public static RoutingConfiguration? Select()
    {
        var candidates =
            TsfProfileEnumerator.GetSelectableProfiles();

        if (candidates.Sources.Count == 0)
        {
            Console.WriteLine(
                "No enabled Japanese, Chinese or Korean IME was found."
            );

            return null;
        }

        if (candidates.Targets.Count == 0)
        {
            Console.WriteLine(
                "No selectable target keyboard layout was found."
            );

            return null;
        }

        Console.WriteLine(
            "=== Source IME ==="
        );

        for (
            int i = 0;
            i < candidates.Sources.Count;
            i++
        )
        {
            Console.WriteLine(
                $"[{i + 1}] {candidates.Sources[i].DisplayName}"
            );
        }

        Console.Write(
            "Select Source: "
        );

        int? sourceIndex =
            ReadSelection(
                candidates.Sources.Count, Console.In, Console.Out
            );
        if (!sourceIndex.HasValue) return null;

        Console.WriteLine();

        Console.WriteLine(
            "=== Target Keyboard Layout ==="
        );

        for (
            int i = 0;
            i < candidates.Targets.Count;
            i++
        )
        {
            Console.WriteLine(
                $"[{i + 1}] {candidates.Targets[i].DisplayName}"
            );
        }

        Console.Write(
            "Select Target: "
        );

        int? targetIndex =
            ReadSelection(
                candidates.Targets.Count, Console.In, Console.Out
            );
        if (!targetIndex.HasValue) return null;

        return new RoutingConfiguration(
            candidates.Sources[sourceIndex.Value],
            candidates.Targets[targetIndex.Value]
        );
    }

    internal static int? ReadSelection(
        int count, TextReader inputReader, TextWriter output
    )
    {
        while (true)
        {
            string? input =
                inputReader.ReadLine();
            // A GUI executable or a closed pipe may not provide standard input.
            // Treat EOF as cancellation instead of retrying forever.
            if (input == null) return null;

            if (
                int.TryParse(
                    input,
                    out int number
                )
                &&
                number >= 1
                &&
                number <= count
            )
            {
                return number - 1;
            }

            output.Write(
                $"Enter a number from 1 to {count}: "
            );
        }
    }
}
