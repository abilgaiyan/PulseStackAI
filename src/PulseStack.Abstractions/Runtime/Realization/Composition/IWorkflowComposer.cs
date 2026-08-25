using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Runtime.Realization.Composition;

/// <summary>
/// Composes a declarative Workflow Asset into an executable Workflow.
/// </summary>
public interface IWorkflowComposer
{
    Task<Workflow> ComposeAsync(
        WorkflowAsset workflow,
        CancellationToken cancellationToken = default);
}
