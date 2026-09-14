using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Runtime.Realization.Resolution;

namespace PulseStack.Core.Runtime.Realization.Application;

/// <summary>
/// Resolves declarative assets exclusively from one fixed AI Asset graph snapshot.
/// </summary>
public sealed class GraphBackedAssetResolver : IAssetResolver
{
    private readonly IReadOnlyDictionary<AssetDefinitionKey, IAsset> _assets;

    public GraphBackedAssetResolver(AIAssetGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        _assets = graph.Nodes.ToDictionary(
            static node => node.DefinitionKey,
            static node => node.Asset);
    }

    public ValueTask<IAsset?> ResolveAsync(
        AssetReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(reference);

        if (!IsSupportedAssetType(reference.Type)
            || reference.Id.IsEmpty
            || reference.Urn is null
            || string.IsNullOrWhiteSpace(reference.Urn.Value)
            || reference.Version is null
            || string.IsNullOrWhiteSpace(reference.Version.Value))
        {
            return ValueTask.FromResult<IAsset?>(null);
        }

        if (!_assets.TryGetValue(AssetDefinitionKey.From(reference), out var asset))
        {
            return ValueTask.FromResult<IAsset?>(null);
        }

        return ValueTask.FromResult<IAsset?>(
            string.Equals(
                asset.Urn.Value,
                reference.Urn.Value,
                StringComparison.Ordinal)
                ? asset
                : null);
    }

    private static bool IsSupportedAssetType(AssetType type) =>
        type is AssetType.Project
            or AssetType.Library
            or AssetType.Package
            or AssetType.Workflow
            or AssetType.Agent
            or AssetType.Prompt
            or AssetType.Tool
            or AssetType.Knowledge
            or AssetType.Memory
            or AssetType.Policy
            or AssetType.Model;
}
