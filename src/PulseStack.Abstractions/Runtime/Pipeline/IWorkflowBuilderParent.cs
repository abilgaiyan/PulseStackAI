namespace PulseStack.Abstractions.Runtime.Pipeline;

/// <summary>
/// Abstraction that allows nested builders (ParallelBuilder, SwitchBuilder, etc.)
/// to return control back to their parent builder after completing a sub-workflow.
/// This is the foundation of the Workflow Language.
/// </summary>
public interface IWorkflowBuilderParent<TParent>
    where TParent : IWorkflowBuilderParent<TParent>
{
    /// <summary>
    /// Adds a completed node and returns the parent builder for fluent continuation.
    /// </summary>
    TParent AddNode(IPipelineNode node);
}
