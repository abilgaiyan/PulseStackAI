namespace PulseStack.Abstractions.WorkflowPackages.Validation;

public sealed record WorkflowPackageDiagnostic(
    string Code,
    string Message);