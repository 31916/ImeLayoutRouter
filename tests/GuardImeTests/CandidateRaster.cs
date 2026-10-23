// Microsoft Windows 11's candidate host can expose neither IMM/TSF candidates
// nor UIA descendants. Compare its actual rendered panel on this fixed VM
// fixture instead. Full screenshots are retained for human review as well.
// This file is excluded from the product and never runs on a user's desktop.
internal static class CandidateRaster
{
    private static Bitmap? baseline;
    internal static Bitmap Capture()
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted"
            || Screen.PrimaryScreen!.Bounds != new Rectangle(0, 0, 1024, 768))
            throw new InvalidOperationException("Raster check requires the fixed disposable VM fixture");
        // Observed candidate panel: x=23..195, y=46..369. Exclude the preedit
        // popup (y=0..31) and the fixture window (x>=350). No other UI is read.
        var bitmap = new Bitmap(300, 400);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(new Point(0, 40), Point.Empty, bitmap.Size);
        return bitmap;
    }
    internal static bool Verify(Bitmap before, string label, bool compareBaseline)
    {
        using var panel = Capture();
        string evidence = Environment.GetEnvironmentVariable("RUNNER_TEMP")!;
        before.Save(Path.Combine(evidence, "v3-ime-" + label + "-before-candidates.png"));
        panel.Save(Path.Combine(evidence, "v3-ime-" + label + "-candidate-panel.png"));
        int previousInk = 0, ink = 0, changedInk = 0, referenceInk = 0, matched = 0;
        for (int y = 0; y < panel.Height; y++)
        for (int x = 0; x < panel.Width; x++)
        {
            var pixel = panel.GetPixel(x, y);
            var previous = before.GetPixel(x, y);
            if (IsInk(previous)) previousInk++;
            if (IsInk(pixel)) ink++;
            if ((IsInk(previous) || IsInk(pixel)) && (Math.Abs(pixel.R - previous.R) > 30
                || Math.Abs(pixel.G - previous.G) > 30 || Math.Abs(pixel.B - previous.B) > 30)) changedInk++;
            if (compareBaseline && baseline != null && IsInk(baseline.GetPixel(x, y)))
            {
                referenceInk++;
                var expected = baseline.GetPixel(x, y);
                if (Math.Abs(pixel.R - expected.R) <= 30 && Math.Abs(pixel.G - expected.G) <= 30
                    && Math.Abs(pixel.B - expected.B) <= 30) matched++;
            }
        }
        Console.WriteLine($"{label}: rendered candidate panel beforeInk={previousInk}; ink={ink}; changedInk={changedInk}; referenceInk={referenceInk}; matched={matched}");
        // Learned predictive suggestions can already be visible before Space.
        // Require actual glyph changes into the conversion panel, not blankness.
        if (changedInk < 200 || ink < 500) return false;
        // Windowed and fullscreen fixtures can place the same candidate panel
        // at different coordinates. Establish the off-state reference for each
        // geometry, then compare the on-state against that exact reference.
        if (!compareBaseline)
        {
            baseline?.Dispose();
            baseline = (Bitmap)panel.Clone();
            return true;
        }
        return referenceInk > 500 && matched >= referenceInk * .95
            && ink >= referenceInk * .95 && ink <= referenceInk * 1.05;
    }
    private static bool IsInk(Color value) => Math.Min(value.R, Math.Min(value.G, value.B)) < 180;
}
