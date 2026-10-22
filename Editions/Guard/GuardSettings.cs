using System.Text.Json;

internal sealed record GuardSettings
{
    public int Version { get; init; } = 1;
    public bool Enabled { get; init; } = true;
    internal static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ImeLayoutRouter", "settings-v3.json");
    internal static GuardSettings? Load()
    {
        try { return FromJson(File.ReadAllText(SettingsPath)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }
    internal static GuardSettings? FromJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Version", out var version)
                || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) || number != 1
                || !root.TryGetProperty("Enabled", out var enabled)
                || enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return null;
            return new GuardSettings { Enabled = enabled.GetBoolean() };
        }
        catch (JsonException) { return null; }
    }
    internal void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        string temporary = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); File.Move(temporary, SettingsPath, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
