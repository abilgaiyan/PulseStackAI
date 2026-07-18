using PulseStack.Abstractions.Persistence.Documents;

namespace PulseStack.Abstractions.Persistence.Validation;

public interface IWorkflowValidator
{
    ValueTask<WorkflowValidationResult> ValidateAsync(
        WorkflowDocument document,
        CancellationToken cancellationToken = default);
}