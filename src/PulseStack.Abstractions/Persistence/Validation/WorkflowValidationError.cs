namespace PulseStack.Abstractions.Persistence.Validation;

public sealed record WorkflowValidationError(
    string Code,
    string Message)
{
    public static WorkflowValidationError From(
        WorkflowDiagnosticDescriptor descriptor)
        => new(
            descriptor.Code,
            descriptor.DefaultMessage);
}