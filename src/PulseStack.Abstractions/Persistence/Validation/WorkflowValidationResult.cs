namespace PulseStack.Abstractions.Persistence.Validation;

public sealed record WorkflowValidationResult(
    bool IsValid,
    IReadOnlyList<WorkflowValidationError> Errors)
{
    public static WorkflowValidationResult Success()
        => new(true, []);

    public static WorkflowValidationResult Failure(
        params WorkflowValidationError[] errors)
        => new(false, errors);
}
