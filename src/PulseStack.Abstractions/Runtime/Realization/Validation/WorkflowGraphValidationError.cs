namespace PulseStack.Abstractions.Runtime.Realization.Validation;

public sealed record WorkflowGraphValidationError
{
    private readonly IReadOnlyList<AgentGraphValidationError> agentErrors;

    public WorkflowGraphValidationError(
        string code,
        string message,
        string path,
        IEnumerable<AgentGraphValidationError>? agentErrors = null)
    {
        Code = code;
        Message = message;
        Path = path;
        this.agentErrors = Array.AsReadOnly(
            agentErrors?.ToArray()
            ?? Array.Empty<AgentGraphValidationError>());
    }

    public string Code { get; }

    public string Message { get; }

    public string Path { get; }

    public IReadOnlyList<AgentGraphValidationError> AgentErrors
        => agentErrors;
}
