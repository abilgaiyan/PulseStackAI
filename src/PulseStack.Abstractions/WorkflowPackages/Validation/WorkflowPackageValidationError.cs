namespace PulseStack.Abstractions.WorkflowPackages.Validation;

public sealed record WorkflowPackageValidationError(
    string Code,
    string Message);