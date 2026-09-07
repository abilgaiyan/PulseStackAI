using System.Diagnostics.CodeAnalysis;

namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative Workflow Asset describing reusable business flow.
/// </summary>
public sealed record WorkflowAsset : Asset
{
    [SetsRequiredMembers]
    internal WorkflowAsset(
        AssetId id,
        AssetUrn urn,
        WorkflowAssetOptions options)
        : base(AssetType.Workflow)
    {
        ArgumentNullException.ThrowIfNull(options);

        var normalized = options with
        {
            Steps = options.Steps.ToArray()
        };

        Id = id;
        Urn = urn;
        Version = AssetVersion.Initial;
        Metadata = new AssetMetadata
        {
            Name = normalized.Name,
            Description = normalized.Description,
            Tags = []
        };
        Lifecycle = AssetLifecycle.Draft;
        Options = normalized;
        References = WorkflowReferenceProjection.Create(normalized.Steps);
    }

    public WorkflowAssetOptions Options { get; }
}
