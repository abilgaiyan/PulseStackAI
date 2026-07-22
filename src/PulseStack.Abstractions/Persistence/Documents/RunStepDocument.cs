using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Persistence.Documents;

public sealed record RunStepDocument : WorkflowStepDocument
{
    /// <summary>
    /// Agent reference used to reconstruct the workflow.
    /// Logical agent identifier resolved by
    /// IAgentResolver during workflow reconstruction.
    /// </summary>
    public required string AgentReference { get; init; }
}
