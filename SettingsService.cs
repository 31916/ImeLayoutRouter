using System.IO;
using System.Text.Json;

static class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string GetSettingsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImeLayoutRouter", "settings.json");

    internal static AppSettings ToSettings(RoutingConfiguration configuration) => new()
    {
        RouteAllSupportedImes = configuration.RouteAllSupportedImes,
        Preferences = configuration.Preferences,
        Source = new SourceProfileSettings { LanguageId = configuration.Source.LanguageId,
            Clsid = configuration.Source.Clsid, ProfileGuid = configuration.Source.ProfileGuid },
        Target = new TargetProfileSettings { LanguageId = configuration.Target.LanguageId, Hkl = configuration.Target.Hkl.ToInt64() }
    };

    public static void Save(RoutingConfiguration configuration)
    {
        if (!configuration.Preferences.IsValid()) throw new ArgumentException("Invalid routing preferences.");
        string path = GetSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string json = JsonSerializer.Serialize(ToSettings(configuration), JsonOptions);
        // A crash during saving must not truncate a working configuration.
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, path, true);
    }

    public static RoutingConfiguration? Load()
    {
        try
        {
            if (!File.Exists(GetSettingsPath())) return null;
            var candidates = TsfProfileEnumerator.GetSelectableProfiles();
            return FromJson(File.ReadAllText(GetSettingsPath()), candidates.Sources, candidates.Targets);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    // Profile resolution is separate from disk/COM so migrations can be replayed.
    internal static RoutingConfiguration? FromJson(string json, IReadOnlyList<InputProfile> sources, IReadOnlyList<InputProfile> targets)
    {
        AppSettings? settings;
        try { settings = JsonSerializer.Deserialize<AppSettings>(json); }
        catch (JsonException) { return null; }
        if (settings == null || settings.Version is not (1 or 2 or 3)
            || settings.Source == null || settings.Target == null || settings.Preferences == null
            || !settings.Preferences.IsValid()) return null;
        InputProfile? source = sources.FirstOrDefault(p => p.LanguageId == settings.Source.LanguageId
            && p.Clsid == settings.Source.Clsid && p.ProfileGuid == settings.Source.ProfileGuid);
        InputProfile? target = targets.FirstOrDefault(p => p.LanguageId == settings.Target.LanguageId
            && p.Hkl.ToInt64() == settings.Target.Hkl);
        if (source == null && settings.RouteAllSupportedImes) source = sources.FirstOrDefault();
        if (source == null || target == null) return null;
        return new RoutingConfiguration(source, target, settings.RouteAllSupportedImes ? sources : null, settings.Preferences);
    }
}
