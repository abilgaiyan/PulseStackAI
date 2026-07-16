using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Persistence.Documents;

/// <summary>
/// Base document for all workflow steps.
/// </summary>
public abstract record WorkflowStepDocument
{
    public required WorkflowStepId Id { get; init; }

    public required string Kind { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<WorkflowStepDocument> Children { get; init; }
        = [];
}