namespace PulseStack.Abstractions.Runtime.Realization.Validation;

public sealed record WorkflowGraphValidationResult
{
    private readonly IReadOnlyList<WorkflowGraphValidationError> errors;

    public WorkflowGraphValidationResult(
        IEnumerable<WorkflowGraphValidationError>? errors = null)
    {
        this.errors = Array.AsReadOnly(
            errors?.ToArray()
            ?? Array.Empty<WorkflowGraphValidationError>());
    }

    public bool IsValid => errors.Count == 0;

    public IReadOnlyList<WorkflowGraphValidationError> Errors => errors;

    public static WorkflowGraphValidationResult Success() => new();

    public static WorkflowGraphValidationResult Failure(
        params WorkflowGraphValidationError[] errors)
        => new(errors);
}
