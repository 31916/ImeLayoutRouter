using System.Runtime.InteropServices;

// Test-only display setup on disposable VMs. Windows exposes no documented
// scaling setter; the two private CCD requests below are used only to arrange
// tests. Success still requires actual GetDpiForWindow evidence, never the
// setter's return value alone. No application binary includes this file.
// Request layout reference: lihas/windows-DPI-scaling-sample (public domain).
internal sealed class VmDpi : IDisposable
{
    private readonly List<ScaleSet> originals = [];
    internal void Set(string device, int percent)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted"
            || (Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") != "31916/ImeLayoutRouter"
                && Environment.MachineName != "V3-TEST"))
            throw new InvalidOperationException("DPI setup is restricted to disposable project VMs");
        int index = percent switch { 100 => 0, 150 => 2, 200 => 4, _ => throw new ArgumentOutOfRangeException(nameof(percent)) };
        if (GetDisplayConfigBufferSizes(2, out uint count, out uint modes) != 0 || count is 0 or > 16 || modes > 256)
            throw new InvalidOperationException("Active display topology unavailable");
        var paths = new DisplayPath[count];
        nint modeData = Marshal.AllocHGlobal(checked((int)modes * 64));
        try
        {
            if (QueryDisplayConfig(2, ref count, paths, ref modes, modeData, 0) != 0)
                throw new InvalidOperationException("Active display topology changed during DPI setup");
            foreach (var path in paths.Take((int)count))
            {
                var name = new SourceName { Info = new Info { Type = 1, Size = (uint)Marshal.SizeOf<SourceName>(), Low = path.Source.Low, High = path.Source.High, Id = path.Source.Id } };
                if (ReadName(ref name) != 0 || name.Name != device) continue;
                var scale = new ScaleGet { Info = name.Info with { Type = unchecked((uint)-3), Size = (uint)Marshal.SizeOf<ScaleGet>() } };
                int result = ReadScale(ref scale);
                if (result != 0) throw new InvalidOperationException("VM display does not support DPI configuration: " + result);
                int desired = scale.Min + index;
                if (desired > scale.Max) throw new InvalidOperationException("Requested DPI exceeds this test display's available scaling");
                var change = new ScaleSet { Info = scale.Info with { Type = unchecked((uint)-4), Size = (uint)Marshal.SizeOf<ScaleSet>() }, Value = desired };
                if (!originals.Any(s => s.Info.Low == change.Info.Low && s.Info.High == change.Info.High && s.Info.Id == change.Info.Id))
                    originals.Add(change with { Value = scale.Current });
                result = WriteScale(ref change);
                if (result != 0) throw new InvalidOperationException("Disposable DPI configuration failed: " + result);
                Console.WriteLine($"Requested actual VM display scale: {device}, {percent}%");
                return;
            }
            throw new InvalidOperationException("Requested test display source missing");
        }
        finally { Marshal.FreeHGlobal(modeData); }
    }
    public void Dispose()
    {
        foreach (var saved in originals)
        {
            var change = saved;
            int result = WriteScale(ref change);
            if (result != 0) throw new InvalidOperationException("Could not restore disposable display DPI: " + result);
        }
        originals.Clear();
    }
    [StructLayout(LayoutKind.Sequential)] private record struct Info { internal uint Type, Size, Low; internal int High; internal uint Id; }
    [StructLayout(LayoutKind.Sequential)] private struct Source { internal uint Low; internal int High; internal uint Id, Mode, Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct Target { internal uint Low; internal int High; internal uint Id, Mode, Technology, Rotation, Scaling, RateNumerator, RateDenominator, ScanOrdering, Available, Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct DisplayPath { internal Source Source; internal Target Target; internal uint Flags; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct SourceName { internal Info Info; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string? Name; }
    [StructLayout(LayoutKind.Sequential)] private struct ScaleGet { internal Info Info; internal int Min, Current, Max; }
    [StructLayout(LayoutKind.Sequential)] private record struct ScaleSet { internal Info Info; internal int Value; }
    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint paths, [Out] DisplayPath[] data, ref uint modes, nint modeData, nint topology);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int ReadName(ref SourceName name);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int ReadScale(ref ScaleGet scale);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigSetDeviceInfo")] private static extern int WriteScale(ref ScaleSet scale);
}
