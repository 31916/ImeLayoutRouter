using System.Runtime.InteropServices;

// Changes only the two QXL adapters belonging to the disposable nested VM.
// These are actual Windows monitor objects, not mocked rectangle arguments.
internal static class VmDisplays
{
    internal static void Configure(bool multiple)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted"
            || Environment.MachineName != "V3-TEST"
            || Environment.GetEnvironmentVariable("GUARD_TEST_VM_ENDPOINT") != "http://10.0.2.2:8000")
            throw new InvalidOperationException("Display changes require the isolated V3 QEMU guest");
        if (multiple)
        {
            // Activate a real extended path before setting its geometry. A
            // disabled WDDM source need not accept a legacy mode-set directly.
            // Windows chooses supported initial modes; exact geometry and DPI
            // are still independently required below and by the input fixture.
            int extended = SetDisplayConfig(0, 0, 0, 0, 0x84);
            Console.WriteLine("VM extended display activation: " + extended);
            if (extended != 0) throw new InvalidOperationException("Windows could not activate the extended QXL topology: " + extended);
            for (int i = 0; i < 20; i++) { Native.PumpMessages(); Thread.Sleep(100); }
        }
        var devices = new List<Device>();
        for (uint index = 0; index < 16; index++)
        {
            var device = new Device { Size = (uint)Marshal.SizeOf<Device>() };
            if (!EnumDisplayDevicesW(null, index, ref device, 0)) break;
            Console.WriteLine($"VM display adapter: {device.Name}; {device.Description}; flags={device.Flags:X}");
            if (device.Description?.Contains("QXL", StringComparison.OrdinalIgnoreCase) == true) devices.Add(device);
        }
        if (devices.Count != 2) throw new InvalidOperationException("Exactly two signed QXL display adapters are required");
        devices.Sort((a, b) => ((b.Flags & 4) != 0).CompareTo((a.Flags & 4) != 0));
        for (int i = 0; i < devices.Count; i++)
        {
            var mode = new Mode { Size = (ushort)Marshal.SizeOf<Mode>() };
            if (!EnumDisplaySettingsExW(devices[i].Name!, -1, ref mode, 0))
            {
                mode = new Mode { Size = (ushort)Marshal.SizeOf<Mode>() };
                if (!EnumDisplaySettingsExW(devices[i].Name!, 0, ref mode, 0))
                    throw new InvalidOperationException("QXL exposes no display mode");
            }
            mode.Fields = 0x20 | 0x40000 | 0x80000 | 0x100000;
            mode.Bits = 32;
            mode.Width = i == 0 ? 1024u : multiple ? 1280u : 0u;
            mode.Height = i == 0 ? 768u : multiple ? 1024u : 0u;
            mode.X = i == 0 || !multiple ? 0 : -1280;
            mode.Y = 0;
            Console.WriteLine($"VM requested display mode: {devices[i].Name}; {mode.Width}x{mode.Height}; position={mode.X},{mode.Y}; size={mode.Size}; extra={mode.Extra}");
            int result = ChangeDisplaySettingsExW(devices[i].Name, ref mode, 0, 0x10000001u | (i == 0 ? 0x10u : 0), 0);
            if (result != 0) throw new InvalidOperationException($"QXL topology configuration failed: {devices[i].Name}; {mode.Width}x{mode.Height}; result={result}");
        }
        if (ApplyDisplaySettings(null, 0, 0, 0, 0) != 0) throw new InvalidOperationException("QXL topology apply failed");
        long deadline = Environment.TickCount64 + 10000;
        do { Native.PumpMessages(); Thread.Sleep(100); }
        while (Screen.AllScreens.Length != (multiple ? 2 : 1) && Environment.TickCount64 < deadline);
        var screens = Screen.AllScreens;
        foreach (var screen in screens) Console.WriteLine($"Actual Windows monitor: {screen.DeviceName}; bounds={screen.Bounds}; primary={screen.Primary}");
        if (screens.Length != (multiple ? 2 : 1) || Screen.PrimaryScreen!.Bounds != new Rectangle(0, 0, 1024, 768)
            || (multiple && !screens.Any(s => s.Bounds == new Rectangle(-1280, 0, 1280, 1024))))
            throw new InvalidOperationException("Windows did not expose the requested real monitor topology");
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Device
    {
        internal uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string? Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string? Description;
        internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string? Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string? Key;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct Mode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string? Device;
        internal ushort Spec, Driver, Size, Extra;
        internal uint Fields;
        internal int X, Y;
        internal uint Orientation, FixedOutput;
        internal short Color, Duplex, YResolution, TtOption, Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string? Form;
        internal ushort LogPixels;
        internal uint Bits, Width, Height, Flags, Frequency, IcmMethod, IcmIntent, Media, Dither, Reserved1, Reserved2, PanWidth, PanHeight;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplayDevicesW(string? device, uint index, ref Device info, uint flags);
    [DllImport("user32.dll")] private static extern int SetDisplayConfig(uint paths, nint pathData, uint modes, nint modeData, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool EnumDisplaySettingsExW(string device, int index, ref Mode mode, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int ChangeDisplaySettingsExW(string? device, ref Mode mode, nint window, uint flags, nint param);
    [DllImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW", CharSet = CharSet.Unicode)] private static extern int ApplyDisplaySettings(string? device, nint mode, nint window, uint flags, nint param);
}
