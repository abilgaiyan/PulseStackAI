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
        Func<ValueTask>? disposeAsync = null)
    {
        this.createProvider = createProvider ?? throw new ArgumentNullException(nameof(createProvider));
        CapabilityProfile = capabilityProfile ?? throw new ArgumentNullException(nameof(capabilityProfile));
        this.recreateAuthorityAsync = recreateAuthorityAsync;
        FailureProof = failureProof;
        this.disposeAsync = disposeAsync;
    }

    public AIAssetCatalogCapabilityProfile CapabilityProfile { get; }

    /// <summary>
    /// Optional provider-specific extension point used to prove ProviderFailure and
    /// InconsistentState classification without teaching the portable suite how a
    /// provider stores data or injects faults.
    /// </summary>
    public AIAssetCatalogProviderFailureProof? FailureProof { get; }

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
