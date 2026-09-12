using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Persistence.AIAssets.Catalog;

public abstract record ExactCatalogLookupResult
{
    private ExactCatalogLookupResult() { }

    public sealed record Found : ExactCatalogLookupResult
    {
        public Found(CatalogRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            Record = record;
        }

        public CatalogRecord Record { get; }
    }

    public sealed record NotFound : ExactCatalogLookupResult;
}

public abstract record CatalogLineageLookupResult
{
    private CatalogLineageLookupResult() { }

    public sealed record Found : CatalogLineageLookupResult
    {
        public Found(CatalogLineage lineage)
        {
            ArgumentNullException.ThrowIfNull(lineage);
            Lineage = lineage;
        }

        public CatalogLineage Lineage { get; }
    }

    public sealed record NotFound : CatalogLineageLookupResult;
}

public enum CatalogPublicationResult
{
    Created,
    AlreadyPresent,
    Conflict
}

public enum AIAssetPublicationResult
{
    Published,
    AlreadyPublished,
    DefinitionNotStored,
    IdentityConflict
}

public abstract record AIAssetResolutionResult
{
    private AIAssetResolutionResult() { }

    public sealed record Resolved : AIAssetResolutionResult
    {
        public Resolved(IAsset asset)
        {
            ArgumentNullException.ThrowIfNull(asset);
            Asset = asset;
        }

        public IAsset Asset { get; }
    }

    public sealed record LineageNotPublished : AIAssetResolutionResult;
    public sealed record DefinitionNotPublished : AIAssetResolutionResult;
    public sealed record ReferenceMismatch : AIAssetResolutionResult;
}
