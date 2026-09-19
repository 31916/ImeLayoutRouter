sealed class RoutingConfiguration
{
    public bool RouteAllSupportedImes { get; }
    public System.Collections.Generic.IReadOnlyList<InputProfile> Sources { get; }

    public bool IsSourceLayout(System.IntPtr layout) => Sources.Any(
        source => EditionPolicy.SupportsSource(source.LanguageId) && source.LanguageId == (layout.ToInt64() & 0xFFFF));
    public InputProfile Source { get; init; }

    public InputProfile Target { get; init; }
    public RoutingPreferences Preferences { get; }

    public RoutingConfiguration(
        InputProfile source,
        InputProfile target,
        System.Collections.Generic.IEnumerable<InputProfile>? allSources = null,
        RoutingPreferences? preferences = null
    )
    {
        Source = source;
        Target = target;
        RouteAllSupportedImes = !EditionPolicy.IsSimple && allSources != null;
        Sources = (RouteAllSupportedImes ? allSources! : new[] { source }).Where(s => EditionPolicy.SupportsSource(s.LanguageId)).ToArray();
        Preferences = EditionPolicy.IsSimple ? new RoutingPreferences() : preferences ?? new RoutingPreferences();
    }
}
