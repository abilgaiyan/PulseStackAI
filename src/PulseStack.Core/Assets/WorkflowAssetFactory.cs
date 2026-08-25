using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Assets;

public sealed class WorkflowAssetFactory
{
    public WorkflowAsset Create(WorkflowAssetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);

        var id = AssetId.New();

        return new WorkflowAsset(
            id,
            new AssetUrn($"urn:pulsestack:workflow:{id}"),
            options);
    }
}
