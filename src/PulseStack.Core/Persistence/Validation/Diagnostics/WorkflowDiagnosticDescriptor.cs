using PulseStack.Abstractions.Persistence.Validation;
namespace PulseStack.Core.Persistence.Validation.Diagnostics;

public sealed record WorkflowDiagnosticDescriptor(
    string Code,
    WorkflowDiagnosticCategory Category,
    string DefaultMessage)
{
    public WorkflowValidationError Create()
        => new(Code, DefaultMessage);
}