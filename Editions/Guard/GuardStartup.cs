using Microsoft.Win32;

internal static class GuardStartup
{
    internal const string ValueName = "ImeLayoutRouterV3";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal static string Command => "\"" + (Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable.")) + "\"";
    internal static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
    }
    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey) ?? throw new InvalidOperationException("Startup key unavailable.");
        SetEnabled(key, enabled, Command);
    }
    internal static void SetEnabled(RegistryKey key, bool enabled, string command)
    {
        if (enabled) key.SetValue(ValueName, command, RegistryValueKind.String);
        else if (string.Equals(key.GetValue(ValueName) as string, command, StringComparison.OrdinalIgnoreCase))
            key.DeleteValue(ValueName, false);
    }
}
