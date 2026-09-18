using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Assets;

public sealed class WorkflowAssetFactory
{
    public WorkflowAsset Create(WorkflowAssetOptions options) =>
        Create(AssetId.New(), options);

    public WorkflowAsset Create(AssetId id, WorkflowAssetOptions options)
    {
        id.EnsureValid();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);

        return new WorkflowAsset(
            id,
            new AssetUrn($"urn:pulsestack:workflow:{id}"),
            options);
    }
}
