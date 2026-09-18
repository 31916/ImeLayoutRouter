sealed class RoutingConfiguration
{
    public bool RouteAllSupportedImes { get; }
    public System.Collections.Generic.IReadOnlyList<InputProfile> Sources { get; }

    public bool IsSourceLayout(System.IntPtr layout) => Sources.Any(
        source => source.LanguageId == (layout.ToInt64() & 0xFFFF));
    public InputProfile Source { get; init; }

    public InputProfile Target { get; init; }

    public RoutingConfiguration(
        InputProfile source,
        InputProfile target,
        System.Collections.Generic.IEnumerable<InputProfile>? allSources = null
    )
    {
        Source = source;
        Target = target;
        RouteAllSupportedImes = allSources != null;
        Sources = (allSources ?? new[] { source }).ToArray();
    }
}
