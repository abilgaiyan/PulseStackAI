namespace PulseStack.Abstractions.Workflows.Values;

/// <summary>
/// Reads a named value from the workflow context state bag.
/// </summary>
public sealed record ContextItemValueDefinition : WorkflowValueDefinition
{
    public required string Key { get; init; }
}
