using System.Diagnostics.CodeAnalysis;

namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative Package Asset defining one versioned distribution boundary.
/// </summary>
public sealed record PackageAsset : Asset
{
    [SetsRequiredMembers]
    internal PackageAsset(
        AssetId id,
        AssetUrn urn,
        PackageAssetOptions options,
        IReadOnlyList<AssetDependency>? dependencies = null)
        : this(
            id,
            urn,
            AssetVersion.Initial,
            options,
            dependencies)
    {
    }

    [SetsRequiredMembers]
    internal PackageAsset(
        AssetId id,
        AssetUrn urn,
        AssetVersion version,
        PackageAssetOptions options,
        IReadOnlyList<AssetDependency>? dependencies = null)
        : base(AssetType.Package)
    {
        ArgumentNullException.ThrowIfNull(options);

        var members = options.Members?.ToArray() ?? [];
        var dependencySnapshot = dependencies?.ToArray() ?? [];
        var packageKey = new AssetDefinitionKey(
            AssetType.Package,
            id,
            version);
        var references = PackageReferenceProjection.Create(
            packageKey,
            members,
            dependencySnapshot);

        var normalized = options with
        {
            Members = members
        };

        Id = id;
        Urn = urn;
        Version = version;
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

    public PackageAssetOptions Options { get; }
}
