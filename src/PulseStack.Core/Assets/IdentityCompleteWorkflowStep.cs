using PulseStack.Abstractions.Workflows.Definitions;

namespace PulseStack.Core.Assets;

/// <summary>
/// Represents a declarative workflow-step subtree whose identity was supplied
/// explicitly for every workflow step in the subtree.
/// </summary>
public sealed class IdentityCompleteWorkflowStep
{
    internal IdentityCompleteWorkflowStep(WorkflowStepDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <summary>
    /// Gets the identity-complete declarative workflow-step definition.
    /// </summary>
    public WorkflowStepDefinition Definition { get; }
}
