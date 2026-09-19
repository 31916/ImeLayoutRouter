using System;

sealed class AppSettings
{
    public int Version { get; init; } = 3;
    public bool RouteAllSupportedImes { get; init; }
    public RoutingPreferences Preferences { get; init; } = new();

    public SourceProfileSettings Source { get; init; } =
        new SourceProfileSettings();

    public TargetProfileSettings Target { get; init; } =
        new TargetProfileSettings();
}

sealed class SourceProfileSettings
{
    public ushort LanguageId { get; init; }

    public Guid Clsid { get; init; }

    public Guid ProfileGuid { get; init; }
}

sealed class TargetProfileSettings
{
    public ushort LanguageId { get; init; }

    public long Hkl { get; init; }
}
