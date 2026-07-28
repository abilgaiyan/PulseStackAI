namespace PulseStack.Abstractions.WorkflowPackages.Identity;

public sealed record WorkflowPackageIdentity(
    WorkflowPackageId Id,
    string Version = "1.0.0");
    