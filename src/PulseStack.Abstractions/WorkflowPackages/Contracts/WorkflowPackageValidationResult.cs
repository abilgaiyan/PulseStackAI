using PulseStack.Abstractions.WorkflowPackages;

namespace PulseStack.Abstractions.WorkflowPackages.Contracts;
public sealed record WorkflowPackageValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public IReadOnlyCollection<WorkflowPackageValidationError> Errors { get; init; }
        = [];
}