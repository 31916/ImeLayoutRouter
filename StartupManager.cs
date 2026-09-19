using System;
using Microsoft.Win32;

static class StartupManager
{
    private const string RunKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string ValueName =
        "ImeLayoutRouter";

    public static bool IsEnabled()
    {
        using RegistryKey? key =
            Registry.CurrentUser.OpenSubKey(
                RunKeyPath,
                false
            );

        string? currentCommand =
            key?.GetValue(ValueName) as string;

        if (string.IsNullOrWhiteSpace(currentCommand))
        {
            return false;
        }

        return string.Equals(
            currentCommand,
            GetStartupCommand(),
            StringComparison.OrdinalIgnoreCase
        );
    }

    public static void SetEnabled(
        bool enabled
    )
    {
        using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (key == null)
        {
            throw new InvalidOperationException(
                "Windows startup settings could not be opened."
            );
        }

        SetEnabled(key, enabled, GetStartupCommand());
    }

    internal static void SetEnabled(RegistryKey key, bool enabled, string command)
    {
        if (enabled)
        {
            key.SetValue(
                ValueName,
                command,
                RegistryValueKind.String
            );
        }
        else if (string.Equals(key.GetValue(ValueName) as string, command, StringComparison.OrdinalIgnoreCase))
        {
            // Both editions share one startup slot. Disabling this edition must
            // not remove another edition's (or the old prototype's) registration.
            key.DeleteValue(
                ValueName,
                false
            );
        }
    }

    private static string GetStartupCommand()
    {
        string? executablePath =
            Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException(
                "Application executable path could not be determined."
            );
        }

        return $"\"{executablePath}\"";
    }
}
