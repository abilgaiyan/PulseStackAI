using PulseStack.Abstractions.WorkflowPackages;
using PulseStack.Abstractions.WorkflowPackages.Identity;

namespace PulseStack.Core.WorkflowPackages.Packaging;

internal sealed record WorkflowPackageManifest
{
    public required WorkflowPackageId PackageId { get; init; }

    public required string PackageVersion { get; init; }

    public required WorkflowPackageMetadata Metadata { get; init; }

    public required string PackageFormatVersion { get; init; }

    public required string MinimumRuntimeVersion { get; init; }

    public required DateTimeOffset BuiltAt { get; init; }

    public required string EntryWorkflow { get; init; }

}