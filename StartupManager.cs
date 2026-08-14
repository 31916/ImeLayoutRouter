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
        using RegistryKey? key =
            Registry.CurrentUser.OpenSubKey(
                RunKeyPath,
                true
            );

        if (key == null)
        {
            throw new InvalidOperationException(
                "Windows startup settings could not be opened."
            );
        }

        if (enabled)
        {
            key.SetValue(
                ValueName,
                GetStartupCommand(),
                RegistryValueKind.String
            );
        }
        else
        {
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