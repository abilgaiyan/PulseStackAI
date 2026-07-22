using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Abstractions.Persistence.Validation;
using PulseStack.Core.Persistence.Validation.Rules;

namespace PulseStack.Core.Persistence.Validation;

public sealed class WorkflowValidator : IWorkflowValidator
{
    public ValueTask<WorkflowValidationResult> ValidateAsync(
        WorkflowDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var context = new WorkflowValidationContext(cancellationToken);

        WorkflowIdentityRule.Validate(document, context);
        WorkflowStructureRule.Validate(document, context);

        return ValueTask.FromResult(context.CreateResult());
    }

}