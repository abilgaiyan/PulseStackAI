namespace PulseStack.Abstractions.Runtime.Realization.Validation;

public sealed record AgentGraphValidationError(
    string Code,
    string Message,
    string Path);
