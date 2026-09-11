using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;

namespace PulseStack.Abstractions.Persistence.AIAssets.Catalog;

/// <summary>
/// Describes one exact definition published in an AI Asset catalog.
/// </summary>
public sealed record CatalogRecord
{
    public CatalogRecord(AssetDefinitionKey definitionKey, AssetUrn urn)
    {
        AIAssetStorageContract.EnsureValidKey(definitionKey);
        ArgumentNullException.ThrowIfNull(urn);
        if (string.IsNullOrWhiteSpace(urn.Value))
        {
            throw new ArgumentException("The catalog record URN must not be empty or whitespace.", nameof(urn));
        }

        DefinitionKey = definitionKey;
        Urn = urn;
    }

    public AssetDefinitionKey DefinitionKey { get; }
    public AssetUrn Urn { get; }
}

/// <summary>
/// Describes one published Asset lineage and the exact versions currently published for it.
/// PublishedVersions is an unordered exact-membership set; it defines no version ordering policy.
/// </summary>
public sealed record CatalogLineage
{
    private readonly IReadOnlySet<AssetVersion> publishedVersions;

    public CatalogLineage(
        AssetType type,
        AssetId id,
        AssetUrn urn,
        IEnumerable<AssetVersion> publishedVersions)
    {
        var validationKey = new AssetDefinitionKey(type, id, AssetVersion.Initial);
        AIAssetStorageContract.EnsureValidKey(validationKey);
        ArgumentNullException.ThrowIfNull(urn);
        ArgumentNullException.ThrowIfNull(publishedVersions);

        if (string.IsNullOrWhiteSpace(urn.Value))
        {
            throw new ArgumentException("The catalog lineage URN must not be empty or whitespace.", nameof(urn));
        }

        var versions = publishedVersions.ToHashSet();
        if (versions.Count == 0)
        {
            throw new ArgumentException("A catalog lineage must contain at least one published version.", nameof(publishedVersions));
        }

        if (versions.Any(version => version is null || string.IsNullOrWhiteSpace(version.Value)))
        {
            throw new ArgumentException("Published versions must not be null, empty, or whitespace.", nameof(publishedVersions));
        }

        Type = type;
        Id = id;
        Urn = urn;
        this.publishedVersions = versions;
    }

    public AssetType Type { get; }
    public AssetId Id { get; }
    public AssetUrn Urn { get; }
    public IReadOnlySet<AssetVersion> PublishedVersions => publishedVersions;
}

/// <summary>
/// Declares whether an authority is transient or survives ordinary process restart.
/// </summary>
public enum AIAssetAuthorityDurability
{
    Transient,
    Durable
}

/// <summary>
/// Composition evidence describing the selected MS-009.7 storage authority.
/// This metadata does not change the MS-009.7 operation contract.
/// </summary>
public sealed record AIAssetStorageCapabilityProfile(AIAssetAuthorityDurability Durability);

/// <summary>
/// Composition evidence describing the selected persistent catalog authority.
/// </summary>
public sealed record AIAssetCatalogCapabilityProfile(AIAssetAuthorityDurability Durability);
