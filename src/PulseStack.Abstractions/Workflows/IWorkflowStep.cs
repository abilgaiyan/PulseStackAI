using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Workflows;

public interface IWorkflowStep
{
    /// <summary>
    /// Stable identity of this workflow step.
    /// Generated during workflow construction and preserved for persistence.
    /// </summary>
    WorkflowStepId Id { get; }

    /// <summary>
    /// Human-readable name of the step.
    /// Used for diagnostics, documentation, and visual tooling.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Child workflow steps.
    /// Simple steps return an empty collection.
    /// Composite steps return their nested workflow.
    /// </summary>
    IReadOnlyList<IWorkflowStep> Children { get; }
}