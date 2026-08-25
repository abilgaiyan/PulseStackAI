namespace PulseStack.Abstractions.Workflows.Values;

/// <summary>
/// Supplies a literal value directly from the Workflow definition.
/// </summary>
public sealed record LiteralValueDefinition : WorkflowValueDefinition
{
    public object? Value { get; init; }
}
