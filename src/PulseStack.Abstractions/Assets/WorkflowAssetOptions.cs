using PulseStack.Abstractions.Workflows.Definitions;

namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative configuration for a reusable Workflow Asset.
/// </summary>
public sealed record WorkflowAssetOptions
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public IReadOnlyCollection<WorkflowStepDefinition> Steps { get; init; } = [];
}
