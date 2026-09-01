namespace PulseStack.Abstractions.Runtime.Realization.Validation;

public sealed record AgentGraphValidationResult
{
    private readonly IReadOnlyList<AgentGraphValidationError> errors;

    public AgentGraphValidationResult(
        IEnumerable<AgentGraphValidationError>? errors = null)
    {
        this.errors = Array.AsReadOnly(
            errors?.ToArray() ?? Array.Empty<AgentGraphValidationError>());
    }

    public bool IsValid => errors.Count == 0;

    public IReadOnlyList<AgentGraphValidationError> Errors => errors;

    public static AgentGraphValidationResult Success()
        => new();

    public static AgentGraphValidationResult Failure(
        params AgentGraphValidationError[] errors)
        => new(errors);
}
