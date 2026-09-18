namespace PulseStack.Core.Assets;

/// <summary>
/// Declarative Workflow Asset options whose root workflow-step subtrees were authored
/// through the explicit identity-complete authoring contract.
/// </summary>
public sealed record IdentityCompleteWorkflowAssetOptions
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public IReadOnlyCollection<IdentityCompleteWorkflowStep> Steps { get; init; } = [];
}
