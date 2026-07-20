using PulseStack.Abstractions.Persistence.Validation;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Core.Persistence.Validation;

internal sealed class WorkflowValidationContext
{
    public WorkflowValidationContext(
        CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
    }

    public CancellationToken CancellationToken { get; }

    public List<WorkflowValidationError> Errors { get; } = [];

    public HashSet<WorkflowStepId> VisitedStepIds { get; } = [];

    public void AddError(
        WorkflowDiagnosticDescriptor descriptor)
    {
        Errors.Add(descriptor.Create());
    }

    public WorkflowValidationResult CreateResult()
        => Errors.Count == 0
            ? WorkflowValidationResult.Success()
            : WorkflowValidationResult.Failure(Errors.ToArray());

    public void ThrowIfCancellationRequested()
        => CancellationToken.ThrowIfCancellationRequested();
}