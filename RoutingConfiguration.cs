sealed class RoutingConfiguration
{
    public InputProfile Source { get; init; }

    public InputProfile Target { get; init; }

    public RoutingConfiguration(
        InputProfile source,
        InputProfile target
    )
    {
        Source = source;
        Target = target;
    }
}