using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Persistence.AIAssets.Catalog;

/// <summary>
/// Classifies failures owned by persistent catalog composition, providers, or observed catalog authority.
/// </summary>
public enum AIAssetCatalogFailureCategory
{
    CompositionConfiguration,
    ProviderFailure,
    InconsistentState
}

/// <summary>
/// Safe diagnostic context for persistent catalog operations. Serialized asset content is never carried here.
/// </summary>
public sealed record AIAssetCatalogDiagnosticContext(
    string Operation,
    AssetDefinitionKey? DefinitionKey = null,
    AssetUrn? Urn = null);

public sealed class AIAssetCatalogException : Exception
{
    public AIAssetCatalogException(
        AIAssetCatalogFailureCategory category,
        string message,
        AIAssetCatalogDiagnosticContext? context = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Category = category;
        Context = context;
    }

    public AIAssetCatalogFailureCategory Category { get; }
    public AIAssetCatalogDiagnosticContext? Context { get; }
}

/// <summary>
/// Classifies operation-neutral failures observed only after catalog authority and MS-009.7 loading interact.
/// </summary>
public enum AIAssetCatalogBoundaryFailureCategory
{
    PublishedDefinitionUnavailable,
    CatalogAssetIdentityMismatch
}

public sealed class AIAssetCatalogBoundaryException : Exception
{
    public AIAssetCatalogBoundaryException(
        AIAssetCatalogBoundaryFailureCategory category,
        string message,
        AIAssetCatalogDiagnosticContext? context = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Category = category;
        Context = context;
    }

    public AIAssetCatalogBoundaryFailureCategory Category { get; }
    public AIAssetCatalogDiagnosticContext? Context { get; }
}
