namespace PulseStack.Abstractions.WorkflowPackages;

public sealed record WorkflowPackageMetadata
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    public string License { get; init; } = string.Empty;

    public IReadOnlyList<string> Tags { get; init; } = [];
}
