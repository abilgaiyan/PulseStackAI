namespace PulseStack.Abstractions.WorkflowPackages;

public sealed record WorkflowPackageManifest
{
    public string PackageFormatVersion { get; init; } = "1.0";

    public string MinimumRuntimeVersion { get; init; } = "0.8.0";

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string EntryWorkflow { get; init; } = "workflow.json";
}
