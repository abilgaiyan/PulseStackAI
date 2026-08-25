namespace PulseStack.Abstractions.Workflows.Conditions;

/// <summary>
/// Declarative reference to a named runtime condition implementation.
/// </summary>
public sealed record NamedConditionDefinition : ConditionDefinition
{
    public required string Name { get; init; }
}
