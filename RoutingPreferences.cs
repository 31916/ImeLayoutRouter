using System.IO;

enum ApplicationRoutingMode { Automatic, Disabled }

sealed record ApplicationRule(string ExecutablePath, ApplicationRoutingMode Mode);

sealed record RoutingPreferences
{
    public ApplicationRoutingMode DefaultMode { get; init; } = ApplicationRoutingMode.Automatic;
    public ApplicationRule[] ApplicationRules { get; init; } = [];
    // Opt in: installing/upgrading must not take over existing shortcuts.
    public bool HotkeysEnabled { get; init; }
    public int PauseKey { get; init; } = 0x77; // Ctrl+Alt+F8
    public int TargetKey { get; init; } = 0x78; // Ctrl+Alt+F9
    public int RestoreKey { get; init; } = 0x79; // Ctrl+Alt+F10

    public ApplicationRoutingMode Resolve(string? executablePath)
    {
        // An inaccessible process cannot be proved to be outside an exclusion.
        if (executablePath == null && ApplicationRules.Length != 0)
            return ApplicationRoutingMode.Disabled;
        return ApplicationRules.FirstOrDefault(r => string.Equals(r.ExecutablePath,
            executablePath, StringComparison.OrdinalIgnoreCase))?.Mode ?? DefaultMode;
    }

    public bool IsValid() => Enum.IsDefined(DefaultMode) && ApplicationRules != null
        && ApplicationRules.Length <= 256
        && ApplicationRules.All(r => r != null && Enum.IsDefined(r.Mode) && IsExecutablePath(r.ExecutablePath))
        && ApplicationRules.Select(r => r.ExecutablePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() == ApplicationRules.Length
        && new[] { PauseKey, TargetKey, RestoreKey }.All(k => k >= 0x75 && k <= 0x7A) // F6..F11 (F12 is reserved)
        && new[] { PauseKey, TargetKey, RestoreKey }.Distinct().Count() == 3;

    internal static bool IsExecutablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || path.Contains('*') || path.Contains('?')) return false;
        try { return Path.IsPathFullyQualified(path) && string.Equals(path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }
}

static class UiText
{
    public static string T(string japanese, string english) =>
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? japanese : english;
}
