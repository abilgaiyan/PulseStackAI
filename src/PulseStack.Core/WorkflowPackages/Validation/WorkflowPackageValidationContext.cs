using PulseStack.Abstractions.WorkflowPackages.Validation;

namespace PulseStack.Core.WorkflowPackages.Validation;

internal sealed class WorkflowPackageValidationContext
{
    private readonly List<WorkflowPackageValidationError> _errors = [];

    public WorkflowPackageValidationContext(
        CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
    }

    public CancellationToken CancellationToken { get; }

    public IReadOnlyList<WorkflowPackageValidationError> Errors
        => _errors;

    public bool HasErrors
        => _errors.Count != 0;

    public void Add(
        WorkflowPackageDiagnostic descriptor,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _errors.Add(
            new WorkflowPackageValidationError(
                descriptor.Code,
                message ?? descriptor.Message));
    }

    public WorkflowPackageValidationResult ToResult()
    {
        var result = new WorkflowPackageValidationResult();

        result.AddRange(_errors);

        return result;
    }
}