using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Runtime.Realization.Validation;

public interface IWorkflowGraphValidator
{
    ValueTask<WorkflowGraphValidationResult> ValidateAsync(
        WorkflowAsset workflow,
        CancellationToken cancellationToken = default);
}
