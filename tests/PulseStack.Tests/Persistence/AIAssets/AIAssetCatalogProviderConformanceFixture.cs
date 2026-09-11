using PulseStack.Abstractions.Persistence.AIAssets.Catalog;

namespace PulseStack.Tests.Persistence.AIAssets;

/// <summary>
/// Provider-neutral lifetime wrapper for one isolated logical catalog namespace.
/// Concrete provider slices must be able to create independent provider participants
/// that observe and mutate this same namespace authority.
/// </summary>
public sealed class AIAssetCatalogProviderConformanceFixture : IAsyncDisposable
{
    private readonly Func<IAIAssetCatalogProvider> createProvider;
    private readonly Func<ValueTask>? recreateAuthorityAsync;
    private readonly Func<ValueTask>? disposeAsync;

    public AIAssetCatalogProviderConformanceFixture(
        Func<IAIAssetCatalogProvider> createProvider,
        AIAssetCatalogCapabilityProfile capabilityProfile,
        Func<ValueTask>? recreateAuthorityAsync = null,
        AIAssetCatalogProviderFailureProof? failureProof = null,
        AIAssetCatalogProviderTokenProof? tokenProof = null,
        Func<ValueTask>? disposeAsync = null)
    {
        this.createProvider = createProvider ?? throw new ArgumentNullException(nameof(createProvider));
        CapabilityProfile = capabilityProfile ?? throw new ArgumentNullException(nameof(capabilityProfile));
        this.recreateAuthorityAsync = recreateAuthorityAsync;
        FailureProof = failureProof;
        TokenProof = tokenProof;
        this.disposeAsync = disposeAsync;
    }

    public AIAssetCatalogCapabilityProfile CapabilityProfile { get; }

    /// <summary>
    /// Optional provider-specific extension point used to prove ProviderFailure and
    /// InconsistentState classification without teaching the portable suite how a
    /// provider stores data or injects faults.
    /// </summary>
    public AIAssetCatalogProviderFailureProof? FailureProof { get; }

    /// <summary>
    /// Optional provider-specific instrumentation proving that the exact caller token
    /// reaches each provider operation. Pre-cancellation remains mandatory independently.
    /// </summary>
    public AIAssetCatalogProviderTokenProof? TokenProof { get; }

    public IAIAssetCatalogProvider CreateProvider() =>
        createProvider() ?? throw new InvalidOperationException("The conformance fixture returned a null provider participant.");

    /// <summary>
    /// Recreates the authority using the same logical namespace. Durable fixtures must
    /// supply this hook so restart retention can be proven. Transient fixtures may omit it.
    /// </summary>
    public ValueTask RecreateAuthorityAsync()
    {
        if (recreateAuthorityAsync is null)
        {
            throw new InvalidOperationException("This fixture does not expose authority recreation.");
        }

        return recreateAuthorityAsync();
    }

    public bool SupportsAuthorityRecreation => recreateAuthorityAsync is not null;

    public ValueTask DisposeAsync() =>
        disposeAsync is null ? ValueTask.CompletedTask : disposeAsync();
}

/// <summary>
/// Provider-specific hooks for producing portable failure classifications.
/// The callbacks must operate against the same fixture namespace authority.
/// </summary>
public sealed record AIAssetCatalogProviderFailureProof(
    Func<IAIAssetCatalogProvider, ValueTask> AssertProviderFailureAsync,
    Func<IAIAssetCatalogProvider, ValueTask> AssertInconsistentStateAsync);

/// <summary>
/// Provider-specific instrumentation for proving caller-token identity propagation.
/// </summary>
public sealed record AIAssetCatalogProviderTokenProof(
    Func<IAIAssetCatalogProvider, CancellationToken, ValueTask> AssertExactLookupTokenAsync,
    Func<IAIAssetCatalogProvider, CancellationToken, ValueTask> AssertLineageLookupTokenAsync,
    Func<IAIAssetCatalogProvider, CancellationToken, ValueTask> AssertPublicationTokenAsync);
