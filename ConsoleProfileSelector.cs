using System;

static class ConsoleProfileSelector
{
    public static RoutingConfiguration? Select()
    {
        var candidates =
            TsfProfileEnumerator.GetSelectableProfiles();

        if (candidates.Sources.Count == 0)
        {
            Console.WriteLine(
                "No selectable Japanese IME was found."
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

        int sourceIndex =
            ReadSelection(
                candidates.Sources.Count
            );

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

        int targetIndex =
            ReadSelection(
                candidates.Targets.Count
            );

        return new RoutingConfiguration(
            candidates.Sources[sourceIndex],
            candidates.Targets[targetIndex]
        );
    }

    private static int ReadSelection(
        int count
    )
    {
        while (true)
        {
            string? input =
                Console.ReadLine();

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

            Console.Write(
                $"Enter a number from 1 to {count}: "
            );
        }
    }
}