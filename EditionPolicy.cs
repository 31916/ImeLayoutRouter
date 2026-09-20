static class EditionPolicy
{
#if SIMPLE_EDITION
    public static bool IsSimple => true;
    public const string Label = "V1 日本語簡易版";
    public const string Version = "1.1.0";
    public const string SettingsFile = "settings-v1.json";
    public static bool SupportsSource(ushort languageId) => languageId == 0x0411;
#else
    public static bool IsSimple => false;
    public const string Label = "V2 Full edition";
    public const string Version = "2.0.0";
    public const string SettingsFile = "settings-v2.json";
    public static bool SupportsSource(ushort languageId) => ImeLanguage.IsSupported(languageId);
#endif
}
