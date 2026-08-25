using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Workflows.Definitions;

/// <summary>
/// Declarative Run step referencing an Agent Asset.
/// </summary>
public sealed record RunStepDefinition : WorkflowStepDefinition
{
    public required AssetReference Agent { get; init; }
}
