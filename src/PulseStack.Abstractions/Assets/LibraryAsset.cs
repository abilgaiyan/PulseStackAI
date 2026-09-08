using System.Diagnostics.CodeAnalysis;

namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative Library Asset defining one flat reusable collection of AI Asset definitions.
/// </summary>
public sealed record LibraryAsset : Asset
{
    [SetsRequiredMembers]
    internal LibraryAsset(
        AssetId id,
        AssetUrn urn,
        LibraryAssetOptions options,
        IReadOnlyCollection<AssetDependency>? dependencies = null)
        : base(AssetType.Library)
    {
        ArgumentNullException.ThrowIfNull(options);

        var members = options.Members?.ToArray() ?? [];
        var dependencySnapshot = dependencies?.ToArray() ?? [];
        var references = LibraryReferenceProjection.Create(members);

        ValidateDependencies(members, dependencySnapshot);

        var normalized = options with
        {
            Members = members
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

    public LibraryAssetOptions Options { get; }

    private static void ValidateDependencies(
        IReadOnlyCollection<AssetReference> members,
        IReadOnlyCollection<AssetDependency> dependencies)
    {
        var memberKeys = members
            .Select(AssetDefinitionKey.From)
            .ToHashSet();

        foreach (var dependency in dependencies)
        {
            ArgumentNullException.ThrowIfNull(dependency);
            ArgumentNullException.ThrowIfNull(dependency.Reference);

            if (memberKeys.Contains(AssetDefinitionKey.From(dependency.Reference)))
            {
                throw new InvalidOperationException(
                    "Library members cannot also be declared as external dependencies.");
            }
        }
    }
}
