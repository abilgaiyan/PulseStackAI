namespace PulseStack.Abstractions.Persistence.Validation;

public sealed record WorkflowValidationError(
    string Code,
    string Message,
    string? Path = null);