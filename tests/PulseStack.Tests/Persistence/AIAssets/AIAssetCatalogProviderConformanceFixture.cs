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
    private readonly Func<ValueTask>? restartAuthorityAsync;
    private readonly Func<ValueTask>? disposeAsync;

    public AIAssetCatalogProviderConformanceFixture(
        Func<IAIAssetCatalogProvider> createProvider,
        AIAssetCatalogCapabilityProfile capabilityProfile,
        AIAssetCatalogProviderFailureScenario failureScenario,
        AIAssetCatalogProviderTokenObservation tokenObservation,
        Func<ValueTask>? restartAuthorityAsync = null,
        Func<ValueTask>? disposeAsync = null)
    {
        this.createProvider = createProvider ?? throw new ArgumentNullException(nameof(createProvider));
        CapabilityProfile = capabilityProfile ?? throw new ArgumentNullException(nameof(capabilityProfile));

        if (!Enum.IsDefined(CapabilityProfile.Durability))
        {
            throw new ArgumentException(
                $"Unsupported catalog durability value '{CapabilityProfile.Durability}'.",
                nameof(capabilityProfile));
        }

        FailureScenario = failureScenario ?? throw new ArgumentNullException(nameof(failureScenario));
        TokenObservation = tokenObservation ?? throw new ArgumentNullException(nameof(tokenObservation));
        this.restartAuthorityAsync = restartAuthorityAsync;
        this.disposeAsync = disposeAsync;
    }

    public AIAssetCatalogCapabilityProfile CapabilityProfile { get; }

    /// <summary>
    /// Required provider-specific fault/corruption arrangement that returns the exception
    /// observed from the portable provider boundary. The shared suite owns classification assertions.
    /// </summary>
    public AIAssetCatalogProviderFailureScenario FailureScenario { get; }

    /// <summary>
    /// Required provider-specific instrumentation exposing only the token actually observed
    /// by each provider operation. The expected token is never supplied to these callbacks.
    /// </summary>
    public AIAssetCatalogProviderTokenObservation TokenObservation { get; }

    /// <summary>
    /// Creates another provider participant attached to the currently live logical namespace.
    /// This proves provider-instance recreation only; it is not a process-restart boundary.
    /// </summary>
    public IAIAssetCatalogProvider CreateProvider() =>
        createProvider() ?? throw new InvalidOperationException("The conformance fixture returned a null provider participant.");

    /// <summary>
    /// Simulates a process restart for the same logical namespace by discarding all
    /// process-lifetime provider state, caches, and coordinators before reopening from
    /// durable authority. Durable fixtures must supply this hook. Transient fixtures may omit it.
    /// </summary>
    public ValueTask RestartAuthorityAsync()
    {
        if (restartAuthorityAsync is null)
        {
            throw new InvalidOperationException("This fixture does not expose a process-restart proof boundary.");
        }

        return restartAuthorityAsync();
    }

    public bool SupportsProcessRestartProof => restartAuthorityAsync is not null;

    public ValueTask DisposeAsync() =>
        disposeAsync is null ? ValueTask.CompletedTask : disposeAsync();
}

/// <summary>
/// Provider-specific arrangements for producing portable failure observations.
/// The callbacks must operate against the same fixture namespace authority and return
/// the exception observed at the provider boundary without asserting its classification.
/// </summary>
public sealed record AIAssetCatalogProviderFailureScenario(
    Func<IAIAssetCatalogProvider, ValueTask<Exception>> ObserveProviderFailureAsync,
    Func<IAIAssetCatalogProvider, ValueTask<Exception>> ObserveInconsistentStateAsync);

/// <summary>
/// Provider-specific read-only instrumentation for observing cancellation tokens received
/// by provider operations. Portable token-equality assertions remain in the shared suite.
/// </summary>
public sealed record AIAssetCatalogProviderTokenObservation(
    Func<CancellationToken?> ReadExactLookupToken,
    Func<CancellationToken?> ReadLineageLookupToken,
    Func<CancellationToken?> ReadPublicationToken);
