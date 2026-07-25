using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.WorkflowPackages.Identity;

namespace PulseStack.Abstractions.WorkflowPackages;

public sealed record WorkflowPackage
{
    public required WorkflowPackageIdentity Identity { get; init; }

    public required WorkflowPackageMetadata Metadata { get; init; }

    public required WorkflowPackageManifest Manifest { get; init; }

    public required Workflow Workflow { get; init; }
}
