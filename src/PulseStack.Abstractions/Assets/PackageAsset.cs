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
        : base(AssetType.Package)
    {
        ArgumentNullException.ThrowIfNull(options);

        var members = options.Members?.ToArray() ?? [];
        var dependencySnapshot = dependencies?.ToArray() ?? [];
        var references = PackageReferenceProjection.Create(members);

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

    public PackageAssetOptions Options { get; }

    private static void ValidateDependencies(
        IReadOnlyList<AssetReference> members,
        IReadOnlyList<AssetDependency> dependencies)
    {
        var memberKeys = members
            .Select(AssetDefinitionKey.From)
            .ToHashSet();
        var dependenciesByKey = new Dictionary<AssetDefinitionKey, AssetDependency>();

        foreach (var dependency in dependencies)
        {
            ArgumentNullException.ThrowIfNull(dependency);
            ArgumentNullException.ThrowIfNull(dependency.Reference);

            var key = AssetDefinitionKey.From(dependency.Reference);

            if (memberKeys.Contains(key))
            {
                throw new InvalidOperationException(
                    "Package members cannot also be declared as external dependencies.");
            }

            if (dependenciesByKey.TryGetValue(key, out var existing))
            {
                if (!string.Equals(
                        existing.Reference.Urn.Value,
                        dependency.Reference.Urn.Value,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Package dependency reference '{key}' has conflicting URNs.");
                }

                if (existing.Required != dependency.Required)
                {
                    throw new InvalidOperationException(
                        $"Package dependency reference '{key}' has conflicting Required values.");
                }

                throw new InvalidOperationException(
                    $"Package dependency reference '{key}' is duplicated.");
            }

            dependenciesByKey.Add(key, dependency);
        }
    }
}
