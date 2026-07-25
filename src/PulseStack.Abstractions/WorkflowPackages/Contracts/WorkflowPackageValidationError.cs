using PulseStack.Abstractions.WorkflowPackages;

namespace PulseStack.Abstractions.WorkflowPackages.Contracts;
public sealed record WorkflowPackageValidationError(
    string Code,
    string Message);