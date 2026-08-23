namespace PulseStack.Abstractions.Assets;

public sealed record AssetMetadata
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Author { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? Category { get; init; }
}
