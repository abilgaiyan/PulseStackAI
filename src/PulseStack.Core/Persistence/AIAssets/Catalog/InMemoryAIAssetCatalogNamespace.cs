using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;

namespace PulseStack.Core.Persistence.AIAssets.Catalog;

/// <summary>
/// Represents one logical transient in-memory AI Asset catalog namespace shared by one or more provider instances.
/// </summary>
public sealed class InMemoryAIAssetCatalogNamespace
{
    internal object SyncRoot { get; } = new();

    internal Dictionary<AssetDefinitionKey, CatalogRecord> ExactRecords { get; } = new();

    internal Dictionary<(AssetType Type, AssetId Id), AssetUrn> UrnByLineage { get; } = new();

    internal Dictionary<AssetUrn, InMemoryCatalogLineageState> LineagesByUrn { get; } = new();

    internal InMemoryAIAssetCatalogTestHooks TestHooks { get; } = new();
}

internal sealed class InMemoryCatalogLineageState
{
    public InMemoryCatalogLineageState(AssetType type, AssetId id, AssetUrn urn, AssetVersion version)
    {
        Type = type;
        Id = id;
        Urn = urn;
        Versions = [version];
    }

    public AssetType Type { get; }

    public AssetId Id { get; }

    public AssetUrn Urn { get; }

    public HashSet<AssetVersion> Versions { get; }
}

/// <summary>
/// Internal observability/fault hooks used only by the provider conformance binding.
/// These hooks do not manufacture inconsistent-state outcomes; inconsistency proof mutates
/// the namespace authority itself and exercises ordinary provider paths.
/// </summary>
internal sealed class InMemoryAIAssetCatalogTestHooks
{
    public CancellationToken? LastExactLookupToken { get; set; }

    public CancellationToken? LastLineageLookupToken { get; set; }

    public CancellationToken? LastPublicationToken { get; set; }

    public Exception? NextProviderFailure { get; set; }
}
