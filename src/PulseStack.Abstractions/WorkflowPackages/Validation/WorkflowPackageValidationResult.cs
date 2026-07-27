namespace PulseStack.Abstractions.WorkflowPackages.Validation;

public sealed class WorkflowPackageValidationResult
{
    private readonly List<WorkflowPackageValidationError> _errors = [];

    public IReadOnlyList<WorkflowPackageValidationError> Errors
        => _errors;

    public bool IsValid
        => _errors.Count == 0;

    public void Add(
        WorkflowPackageValidationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        _errors.Add(error);
    }

    public void AddRange(
        IEnumerable<WorkflowPackageValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        _errors.AddRange(errors);
    }
}