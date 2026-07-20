namespace PulseStack.Abstractions.Persistence.Validation;

public sealed record WorkflowDiagnosticDescriptor(
    string Code,
    WorkflowDiagnosticCategory Category,
    string DefaultMessage)
{
    public WorkflowValidationError Create()
        => new(Code, DefaultMessage);
}