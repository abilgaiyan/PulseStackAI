namespace PulseStack.Abstractions.Workflows;

/// <summary>
/// Represents the intrinsic business definition of a workflow.
///
/// A workflow definition describes what a workflow is,
/// independent of persistence, runtime execution,
/// or repository concerns.
/// </summary>
public sealed record WorkflowDefinition
{
    public WorkflowDefinition(
        string name,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Description = description;
    }

    /// <summary>
    /// Human-readable workflow name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Optional workflow description.
    /// </summary>
    public string? Description { get; }
}