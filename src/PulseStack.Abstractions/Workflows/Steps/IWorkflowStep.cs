namespace PulseStack.Abstractions.Workflows.Steps;

/// <summary>
/// Represents the smallest meaningful unit of work within a workflow.
/// A Workflow Step describes work. It never performs work.
/// </summary>
public interface IWorkflowStep
{
    /// <summary>
    /// Human-readable name of the step.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Child workflow steps.
    /// Simple steps return an empty collection.
    /// Composite steps return their nested workflow.
    /// </summary>
    IReadOnlyList<IWorkflowStep> Children { get; }
}
