using System.Diagnostics.CodeAnalysis;

namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative Project Asset defining the semantic membership of one intelligent application.
/// </summary>
public sealed record ProjectAsset : Asset
{
    [SetsRequiredMembers]
    internal ProjectAsset(
        AssetId id,
        AssetUrn urn,
        ProjectAssetOptions options,
        IReadOnlyCollection<AssetDependency>? dependencies = null)
        : base(AssetType.Project)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.EntryWorkflow);

        var ownedAssets = options.OwnedAssets?.ToArray() ?? [];
        var dependencySnapshot = dependencies?.ToArray() ?? [];
        var references = ProjectReferenceProjection.Create(
            options.EntryWorkflow,
            ownedAssets);

        ValidateDependencies(ownedAssets, dependencySnapshot);

        var normalized = options with
        {
            OwnedAssets = ownedAssets
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
        References = references;
        Dependencies = dependencySnapshot;
    }

    public ProjectAssetOptions Options { get; }

    private static void ValidateDependencies(
        IReadOnlyCollection<AssetReference> ownedAssets,
        IReadOnlyCollection<AssetDependency> dependencies)
    {
        var ownedKeys = ownedAssets
            .Select(AssetDefinitionKey.From)
            .ToHashSet();

        foreach (var dependency in dependencies)
        {
            ArgumentNullException.ThrowIfNull(dependency);
            ArgumentNullException.ThrowIfNull(dependency.Reference);

            if (dependency.Reference.Type == AssetType.Project)
            {
                throw new InvalidOperationException(
                    "Project cannot depend on another Project Asset.");
            }

            if (ownedKeys.Contains(AssetDefinitionKey.From(dependency.Reference)))
            {
                throw new InvalidOperationException(
                    "Project owned Assets cannot also be declared as external dependencies.");
            }
        }
    }
}
