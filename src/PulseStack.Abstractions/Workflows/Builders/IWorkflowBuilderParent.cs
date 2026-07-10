using  PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Abstractions.Workflows.Builders;

/// <summary>
/// Abstraction that allows nested builders (ParallelBuilder, SwitchBuilder, etc.)
/// to return control back to their parent builder after completing a sub-workflow.
/// This is the foundation of the Workflow Language.
/// </summary>
public interface IWorkflowBuilderParent<TParent>
    where TParent : IWorkflowBuilderParent<TParent>
{
    /// <summary>
    /// Adds a completed step and returns the parent builder for fluent continuation.
    /// </summary>
    TParent AddStep(IWorkflowStep step);
}
