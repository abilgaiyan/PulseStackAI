using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Persistence.Documents;

public sealed record RunStepDocument : WorkflowStepDocument
{
    /// <summary>
    /// Agent reference used to reconstruct the workflow.
    /// </summary>
    public required string AgentReference { get; init; }
}
