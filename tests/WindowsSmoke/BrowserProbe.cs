using System.Globalization;

static class BrowserProbe
{
    // Observes and routes only the explicitly selected browser window.
    // Never reads page text, passwords, titles or keyboard events.
    public static void Run(string[] args)
    {
        var window = new IntPtr(long.Parse(args[1], CultureInfo.InvariantCulture));
        var candidates = TsfProfileEnumerator.GetSelectableProfiles();
        foreach (var p in candidates.Sources) Console.WriteLine($"SOURCE {p.LanguageId:X4} {p.DisplayName}");
        foreach (var p in candidates.Targets) Console.WriteLine($"TARGET {p.LanguageId:X4} {p.DisplayName} {p.Hkl:X}");
        var source = candidates.Sources.First(p => p.LanguageId == 0x0411);
        var target = candidates.Targets.First();
        var config = new RoutingConfiguration(source, target);
        using var stop = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        using var fields = new FocusedInputProbe();
        var worker = Task.Run(() => RoutingMonitor.Run(config, stop.Token, allowedForeground: window));
        InputSnapshot? previous = null;
        while (!stop.IsCancellationRequested)
        {
            var snapshot = RoutingMonitor.ReadSnapshot(config, fields);
            if (snapshot is { } current && current.Context.Foreground == window && current != previous)
            {
                Console.WriteLine($"{DateTimeOffset.Now:O} layout={current.KeyboardLayout:X} mode={current.Mode} direct={current.RequiresDirectInput} element={current.Context.ElementId} generation={current.Context.FocusVersion}");
                previous = current;
            }
            Thread.Sleep(50);
        }
        worker.GetAwaiter().GetResult();
    }
}
